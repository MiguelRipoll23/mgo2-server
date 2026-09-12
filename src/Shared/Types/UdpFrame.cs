namespace Mgo2Server.Shared.Types;

/// <summary>
/// A fully decoded peer-to-peer frame: what a wire datagram means.
/// </summary>
/// <param name="Counter">Frame counter with the compression marker masked off.</param>
/// <param name="Compressed">Whether the content region is a raw LZSS stream.</param>
/// <param name="Content">The content region, decompressed when the marker was set.</param>
/// <param name="Messages">Messages parsed from the content region.</param>
/// <param name="Decoded">The raw decoded datagram, with the chain and scramble removed.</param>
public sealed record UdpFrame(
    ushort Counter,
    bool Compressed,
    byte[] Content,
    IReadOnlyList<UdpMessage> Messages,
    byte[] Decoded);
