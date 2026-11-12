namespace Mgo2Server.GameplayServer.Rooms;

/// <summary>
/// The lookup half of the roster: which member one endpoint names.
/// </summary>
/// <remarks>
/// Split out of <c>RoomRosterService.cs</c> to keep both files inside the
/// repository's size limit. The room's members and the gate that guards them are
/// the service's own; this file only reads them.
/// </remarks>
public sealed partial class RoomRosterService
{
    /// <summary>
    /// The member filed under an endpoint, when the roster has one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The lookup is by the endpoint the peer's profile arrived from, which is
    /// the key <see cref="Register"/> files it under; an endpoint with no entry
    /// is a peer that has not sent a profile yet, or one the reaper has already
    /// taken off the roster.
    /// </para>
    /// <para>
    /// <b>The gameplay channel is a port of its own.</b> A peer files under the
    /// endpoint its profile arrived from and writes its hits from the gameplay
    /// session's, so an exact comparison named no shooter for a client's own
    /// hits: the kill was credited to slot zero, which is this host's seat, and
    /// the client drew it on `server-99999`'s row. The address is the part that
    /// does not move between the two channels
    /// (<see cref="RosterEndpointUtils.HostOf"/>). Two members on one address are
    /// not guessed between; the hit is credited to nobody rather than to a
    /// stranger, which is the rule the kill feed already follows.
    /// </para>
    /// </remarks>
    /// <param name="remoteAddress">Endpoint formatted as "address:port".</param>
    /// <returns>The member, or <c>null</c>.</returns>
    public RosterMember? Find(string remoteAddress)
    {
        lock (gate)
        {
            if (members.Find(member => member.RemoteAddress == remoteAddress) is { } exact)
            {
                return exact;
            }

            var address = RosterEndpointUtils.HostOf(remoteAddress);
            if (address.Length == 0)
            {
                return null;
            }

            var sameAddress = members.FindAll(
                member => RosterEndpointUtils.HostOf(member.RemoteAddress) == address);
            return sameAddress.Count == 1 ? sameAddress[0] : null;
        }
    }
}
