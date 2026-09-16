using Mgo2Server.Shared.Constants;

namespace Mgo2Server.Shared.Options;

/// <summary>
/// Options that describe how a server reaches the internal coordination
/// endpoint of the HTTP API. A gameplay lobby needs nothing but this address to
/// take part in the coordination.
/// </summary>
public sealed class CoordinationOptions
{
    /// <summary>
    /// URL of the internal gRPC endpoint of the HTTP API. It defaults to a
    /// deployment that runs every server on the same machine, which is what the
    /// run scripts start; a container deployment names the HTTP API service.
    /// </summary>
    public string ServerUrl { get; set; } = $"http://localhost:{PortConstants.InternalGrpcPort}";

    /// <summary>
    /// Time to wait before the connection is attempted again after it could not
    /// be established or was lost.
    /// </summary>
    public TimeSpan ReconnectInterval { get; set; } = TimeSpan.FromMinutes(1);
}
