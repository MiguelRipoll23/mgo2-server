namespace Mgo2Server.Http.Contracts;

/// <summary>Reply of the health endpoint.</summary>
/// <param name="Status">Status word, always <c>ok</c> while the API answers.</param>
/// <param name="Timestamp">Time the reply was produced at.</param>
public sealed record HealthContract(string Status, DateTimeOffset Timestamp);
