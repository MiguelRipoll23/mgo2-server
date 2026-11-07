namespace Mgo2Server.Shared.Domain.Authentication;

/// <summary>
/// Outcome of one login attempt: whether the credentials matched an account, the
/// body the gate client is answered with, and the account it matched. The reply a
/// client receives is the only place the outcome is expressed, and a caller that
/// follows a login reads the account off this value rather than out of the reply.
/// </summary>
/// <param name="Succeeded">Whether the credentials matched an account.</param>
/// <param name="Reply">Body of the login response.</param>
/// <param name="AccountIdentifier">Account the credentials matched, or <c>null</c>.</param>
public readonly record struct LoginAttempt(bool Succeeded, string Reply, int? AccountIdentifier);
