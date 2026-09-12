using Microsoft.AspNetCore.Http;

namespace Mgo2Server.Http.Contracts;

/// <summary>Fields the client posts to the login endpoint.</summary>
public sealed class LoginForm
{
    /// <summary>Reads the form the client posted.</summary>
    /// <param name="form">Form of the request, empty when none was posted.</param>
    public static LoginForm From(IFormCollection form) => new()
    {
        Name = Read(form, "name"),
        Passwd = Read(form, "passwd"),
        Product = Read(form, "product"),
        Lang = Read(form, "lang"),
        Tz = Read(form, "tz"),
        Disk = Read(form, "disk"),
        Ps3 = Read(form, "ps3"),
        Stime = Read(form, "stime"),
        Seed = Read(form, "seed"),
    };

    /// <summary>Reads one field of the form.</summary>
    /// <param name="form">Form of the request.</param>
    /// <param name="fieldName">Name of the field.</param>
    internal static string? Read(IFormCollection form, string fieldName) =>
        form.TryGetValue(fieldName, out var value) ? value.ToString() : null;

    /// <summary>Login name of the account.</summary>
    public string? Name { get; set; }

    /// <summary>MD5 hash of the password.</summary>
    public string? Passwd { get; set; }

    /// <summary>Product identifier the client reports.</summary>
    public string? Product { get; set; }

    /// <summary>Language code of the client.</summary>
    public string? Lang { get; set; }

    /// <summary>Timezone offset of the client, in minutes.</summary>
    public string? Tz { get; set; }

    /// <summary>Disk flag of the client.</summary>
    public string? Disk { get; set; }

    /// <summary>Platform flag of the client.</summary>
    public string? Ps3 { get; set; }

    /// <summary>Server time the client presents.</summary>
    public string? Stime { get; set; }

    /// <summary>Random seed the client derives its session field from.</summary>
    public string? Seed { get; set; }
}
