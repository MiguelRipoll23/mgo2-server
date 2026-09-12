using Mgo2Server.Shared.Domain.Characters;
using Mgo2Server.Shared.Tcp;
using Mgo2Server.Shared.Types;
using Microsoft.Extensions.DependencyInjection;

namespace Mgo2Server.AccountLobbyServer;

/// <summary>
/// Serves character creation, deletion and selection. Its port comes from the
/// lobby row of type account.
/// </summary>
public sealed class AccountServer(IServiceProvider serviceProvider, int port)
    : TcpServerBase(serviceProvider, port)
{
    /// <inheritdoc />
    protected override ServerType ServerType => ServerType.Account;

    /// <inheritdoc />
    protected override string LogPrefix => "tcp:account";

    /// <summary>
    /// Releases the lobby the character was parked in, so an account that
    /// disconnects without a lobby notice does not keep it.
    /// </summary>
    /// <param name="session">Session that ended.</param>
    protected override void OnSessionDestroyed(TcpSession session)
    {
        if (session.CharacterIdentifier is null)
        {
            return;
        }

        var characterService = Services.GetRequiredService<CharacterService>();
        _ = Task.Run(async () =>
        {
            try
            {
                await characterService.SetLobbyAsync(session.CharacterIdentifier.Value, null);
            }
            catch (Exception exception)
            {
                Logger.LogError(exception, "[{LogPrefix}] setLobby on disconnect failed", LogPrefix);
            }
        });
    }
}
