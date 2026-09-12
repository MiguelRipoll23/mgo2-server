using System.Buffers.Text;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Mgo2Server.Http.Errors;
using Mgo2Server.Http.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Mgo2Server.Http.Authentication;

/// <summary>
/// Validates the bearer tokens of the API: an HS256 signature over the secret of
/// the deployment, with no issuer, no audience, and an expiry that only has to
/// hold when the token carries one.
/// </summary>
/// <remarks>
/// The framework's bearer handler cannot be used here because it refuses HMAC
/// keys shorter than 256 bits, while a deployment may configure a secret of any
/// length. Tokens signed with such a secret would all be rejected.
/// </remarks>
/// <param name="options">Options of the scheme.</param>
/// <param name="loggerFactory">Factory the logger is created with.</param>
/// <param name="encoder">Encoder used for the challenge response.</param>
/// <param name="apiOptions">Options holding the secret the tokens are signed with.</param>
public sealed class BearerTokenAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory loggerFactory,
    UrlEncoder encoder,
    IOptions<HttpApiOptions> apiOptions)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, loggerFactory, encoder)
{
    /// <summary>Authentication scheme this handler answers.</summary>
    public const string SchemeName = "Bearer";

    /// <summary>Reply of a request that carries no bearer token at all.</summary>
    private const string MissingAuthorizationMessage = "no authorization included in request";

    /// <summary>Reply of a request whose token cannot be trusted.</summary>
    private const string InvalidTokenMessage = "Unauthorized";

    private const string AuthorizationPrefix = "Bearer ";
    private const string SignatureAlgorithm = "HS256";
    private const int TokenSegmentCount = 3;

    private readonly HttpApiOptions apiOptions = apiOptions.Value;

    /// <summary>Reason the last authentication attempt failed.</summary>
    private string? failureMessage;

    /// <inheritdoc />
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();
        if (string.IsNullOrEmpty(header) ||
            !header.StartsWith(AuthorizationPrefix, StringComparison.OrdinalIgnoreCase))
        {
            // No token was presented, so the request is simply anonymous. A
            // failure is reserved for a token that was presented and cannot be
            // trusted; reporting one here would log an authentication failure
            // for every public route the client reaches without a token.
            // The reply an unauthorized route answers is unchanged.
            failureMessage = MissingAuthorizationMessage;
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var token = header[AuthorizationPrefix.Length..].Trim();
        var result = Authenticate(token);
        return Task.FromResult(result);
    }

    /// <inheritdoc />
    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        await Response.WriteAsJsonAsync(
            new FailureResponse("HTTP_ERROR", failureMessage ?? InvalidTokenMessage));
    }

    /// <summary>Verifies one compact token.</summary>
    /// <param name="token">Token the client presented.</param>
    private AuthenticateResult Authenticate(string token)
    {
        var segments = token.Split('.');
        if (segments.Length != TokenSegmentCount ||
            !UsesHmacSha256(segments[0]) ||
            !HasValidSignature(segments) ||
            !IsInsideValidityWindow(segments[1]))
        {
            failureMessage = InvalidTokenMessage;
            return AuthenticateResult.Fail(failureMessage);
        }

        var principal = new ClaimsPrincipal(BuildIdentity(segments[1]));
        return AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName));
    }

    /// <summary>Reads whether the token header names the algorithm the secret signs with.</summary>
    /// <param name="encodedHeader">Encoded header segment of the token.</param>
    private static bool UsesHmacSha256(string encodedHeader)
    {
        var header = DecodeJson(encodedHeader);
        if (header is null)
        {
            return false;
        }

        using (header)
        {
            return header.RootElement.TryGetProperty("alg", out var algorithm) &&
                   algorithm.ValueKind == JsonValueKind.String &&
                   string.Equals(algorithm.GetString(), SignatureAlgorithm, StringComparison.Ordinal);
        }
    }

    /// <summary>Recomputes the signature of the token and compares it with the one it carries.</summary>
    /// <param name="segments">Segments of the token.</param>
    private bool HasValidSignature(string[] segments)
    {
        var key = Encoding.UTF8.GetBytes(apiOptions.JwtSecret);
        var signedContent = Encoding.ASCII.GetBytes($"{segments[0]}.{segments[1]}");
        var expected = HMACSHA256.HashData(key, signedContent);

        byte[] presented;
        try
        {
            presented = Base64Url.DecodeFromChars(segments[2]);
        }
        catch (FormatException)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(expected, presented);
    }

    /// <summary>Checks the validity window the payload declares, when it declares one.</summary>
    /// <param name="encodedPayload">Encoded payload segment of the token.</param>
    private static bool IsInsideValidityWindow(string encodedPayload)
    {
        var payload = DecodeJson(encodedPayload);
        if (payload is null)
        {
            return false;
        }

        using (payload)
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            return !ExceedsDeadline(payload, "exp", expiry => expiry <= now) &&
                   !ExceedsDeadline(payload, "nbf", notBefore => notBefore > now);
        }
    }

    /// <summary>Builds the identity the token claims.</summary>
    /// <param name="encodedPayload">Encoded payload segment of the token.</param>
    private static ClaimsIdentity BuildIdentity(string encodedPayload)
    {
        using var payload = DecodeJson(encodedPayload);
        var claims = payload is null
            ? []
            : payload.RootElement
                .EnumerateObject()
                .Where(property => property.Value.ValueKind is not (JsonValueKind.Object or JsonValueKind.Array or JsonValueKind.Null))
                .Select(property => new Claim(property.Name, property.Value.ToString()));

        return new ClaimsIdentity(claims, SchemeName);
    }

    /// <summary>Reads one timestamp claim and reports whether it is outside the window.</summary>
    /// <param name="payload">Payload of the token.</param>
    /// <param name="claimName">Name of the timestamp claim.</param>
    /// <param name="isOutsideWindow">Predicate that decides whether the timestamp is outside the window.</param>
    private static bool ExceedsDeadline(JsonDocument payload, string claimName, Func<long, bool> isOutsideWindow)
    {
        if (!payload.RootElement.TryGetProperty(claimName, out var claim))
        {
            return false;
        }

        if (claim.ValueKind == JsonValueKind.Number && claim.TryGetInt64(out var seconds))
        {
            return isOutsideWindow(seconds);
        }

        return true;
    }

    /// <summary>Decodes one base64url JSON segment of a token.</summary>
    /// <param name="segment">Segment to decode.</param>
    /// <returns>The document, or <c>null</c> when the segment is not JSON.</returns>
    private static JsonDocument? DecodeJson(string segment)
    {
        try
        {
            return JsonDocument.Parse(Base64Url.DecodeFromChars(segment));
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
            return null;
        }
    }
}
