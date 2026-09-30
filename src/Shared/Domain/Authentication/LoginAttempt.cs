namespace Mgo2Server.Shared.Domain.Authentication;

/// <summary>
/// Outcome of one login attempt: whether the credentials matched an account, and
/// the body the gate client is answered with. The two are one value because the
/// reply a client receives is the only place the outcome is expressed, and a
/// caller that reports a login has to read it the same way the client does.
/// </summary>
/// <param name="Succeeded">Whether the credentials matched an account.</param>
/// <param name="Reply">Body of the login response.</param>
public readonly record struct LoginAttempt(bool Succeeded, string Reply);
