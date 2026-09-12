using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Domain.Authentication;
using Mgo2Server.Shared.Interfaces;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.AccountLobbyServer.Commands;

/// <summary>
/// Validates the session field a client presents when it enters an account
/// lobby. The payload is the claimed account identifier followed by the
/// sixteen-byte session field the client derived from its login token.
/// </summary>
/// <param name="sessionService">Service that owns the login sessions.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
/// <param name="logger">Logger of this handler.</param>
public sealed class CheckSessionHandler(
    SessionService sessionService,
    SessionHelper sessionHelper,
    ILogger<CheckSessionHandler> logger) : ICommandHandler
{
    /// <summary>Length of the session field the client derives from its login token.</summary>
    private const int SessionFieldLength = 16;

    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        if (packet.Payload.Length < 4 + SessionFieldLength)
        {
            logger.LogInformation(
                "[tcp][account] check session: payload too short ({Length} bytes)",
                packet.Payload.Length);
            await sessionHelper.SendResultAsync(session, 0x3004, ErrorCodeConstants.ResultInvalidSession, cancellationToken);
            return;
        }

        var reader = new PacketReader(packet.Payload);
        var claimedUserIdentifier = reader.ReadUInt32();
        var sessionField = CryptoUtility.StoredSessionFieldFromWire(reader.ReadBytes(SessionFieldLength));

        // The client derived this from its login token, so the session row
        // already holds the same value and it is matched directly. Nothing is
        // decoded back into a token.
        var storedSession = await sessionService.FindByTokenAsync(sessionField, cancellationToken);

        if (storedSession is null)
        {
            logger.LogInformation("[tcp][account] check session: no account holds the presented session");
            await sessionHelper.SendResultAsync(session, 0x3004, ErrorCodeConstants.ResultInvalidSession, cancellationToken);
            return;
        }

        // The session is real, but it has to belong to whoever the client says
        // it is; otherwise a leaked token would let any identifier be claimed.
        if (storedSession.UserIdentifier != claimedUserIdentifier)
        {
            logger.LogInformation(
                "[tcp][account] check session: session belongs to user {Owner}, client claimed {Claimed}",
                storedSession.UserIdentifier,
                claimedUserIdentifier);
            await sessionHelper.SendResultAsync(session, 0x3004, ErrorCodeConstants.ResultInvalidSession, cancellationToken);
            return;
        }

        // Entering an account lobby means choosing a character, so a stale
        // selection is meaningless here; the character choice arrives with the
        // gameplay lobby's check-session.
        session.UserIdentifier = storedSession.UserIdentifier;
        session.CharacterIdentifier = null;

        await sessionHelper.SendResultAsync(session, 0x3004, ErrorCodeConstants.ResultNone, cancellationToken);
    }
}
