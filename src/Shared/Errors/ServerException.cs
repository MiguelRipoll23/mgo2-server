namespace Mgo2Server.Shared.Errors;

/// <summary>
/// An error that maps to a described failure code and an HTTP status, raised by
/// the domain services and translated to a response by the transport layers.
/// </summary>
public sealed class ServerException : Exception
{
    /// <summary>Creates an exception with a failure code, a message and a status.</summary>
    /// <param name="code">Machine-readable failure code.</param>
    /// <param name="message">Human-readable failure description.</param>
    /// <param name="statusCode">HTTP status that describes the failure.</param>
    public ServerException(string code, string message, int statusCode)
        : base(message)
    {
        Code = code;
        StatusCode = statusCode;
    }

    /// <summary>Machine-readable failure code.</summary>
    public string Code { get; }

    /// <summary>HTTP status that describes the failure.</summary>
    public int StatusCode { get; }
}
