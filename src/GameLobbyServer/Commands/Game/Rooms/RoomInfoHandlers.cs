using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Domain.Games;
using Mgo2Server.Shared.Interfaces;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.GameLobbyServer.Commands.Game.Rooms;

/// <summary>Edits the details of the caller's own room in place.</summary>
/// <param name="gameService">Service that owns the rooms.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class HostInGameInfoHandler(
    GameService gameService,
    SessionHelper sessionHelper) : ICommandHandler
{
    // The stance offset differs from the settings blob's: here it is the word
    // the settings blob uses for the dedicated flag.
    private const int NameOffset = 0x00;
    private const int CommentOffset = 0x10;
    private const int PasswordFlagOffset = 0x90;
    private const int PasswordOffset = 0x91;
    private const int StanceOffset = 0xa1;

    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        var game = session.GameIdentifier is { } gameIdentifier
            ? await gameService.FindByIdAsync(gameIdentifier, cancellationToken)
            : null;

        if (game is not null && session.CharacterIdentifier is { } characterIdentifier && game.HostIdentifier == characterIdentifier)
        {
            var bytes = packet.Payload;
            var name = StringUtility.ReadFixedString(bytes, NameOffset, 16);
            var comment = ReadField(bytes, CommentOffset, 128);
            var passwordEnabled = bytes.Length > PasswordFlagOffset && bytes[PasswordFlagOffset] != 0;
            var password = passwordEnabled ? ReadField(bytes, PasswordOffset, 16) : string.Empty;
            var stance = bytes.Length > StanceOffset ? bytes[StanceOffset] : 0;

            await gameService.UpdateAsync(game.Identifier, room =>
            {
                room.Name = name.Length > 0 ? name : room.Name;
                room.Comment = comment;
                room.Password = password;
                room.Stance = stance;
            }, cancellationToken);
        }

        await sessionHelper.SendResultAsync(session, CommandConstants.HostInGameInfoResult, ErrorCodeConstants.ResultNone, cancellationToken);
    }

    private static string ReadField(byte[] bytes, int offset, int maximum) =>
        offset >= bytes.Length ? string.Empty : StringUtility.ReadFixedString(bytes, offset, Math.Min(maximum, bytes.Length - offset));
}
