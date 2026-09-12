namespace Mgo2Server.Shared.Constants;

/// <summary>
/// Key material of the peer-to-peer channel cipher.
/// </summary>
public static class UdpCryptoKeyConstants
{
    /// <summary>
    /// Multiplier of the linear congruential generator driving the header
    /// scramble positions and the XOR chain seed.
    /// </summary>
    public const uint LinearCongruentialMultiplier = 0x5d588b65;

    /// <summary>Chain key of frames sent before the session key is established.</summary>
    public const uint PreHandshakeKey = 0x87103c2f;

    /// <summary>Magic value every handshake must carry.</summary>
    public const uint ModuleMagic = 0x4d258ab7;

    /// <summary>
    /// Constant key of the self-verifying ten-byte tail digest. Session-keyed
    /// frames use the session key exclusive-ORed with this value instead.
    /// </summary>
    public const uint TailDigestKey = 0x2b58de69;
}
