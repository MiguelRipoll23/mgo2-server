namespace Mgo2Server.Http.Options;

/// <summary>
/// Options that describe how the HTTP API runs. They are read from the flat
/// upper-case environment variables named in the setup documentation.
/// </summary>
public sealed class HttpApiOptions
{
    /// <summary>Upstream launcher the patch files are mirrored from.</summary>
    public string LauncherServer { get; set; } = "http://mgo2pc.com";

    /// <summary>
    /// Directory holding the policy file, the mirrored patch files and the home
    /// page. Resolved from the application base so it is found whether the API
    /// runs from a publish folder (Docker) or a build output (the run scripts).
    /// </summary>
    public string StaticDirectory { get; set; } = Path.Combine(AppContext.BaseDirectory, "Static");

    /// <summary>File name of the policy document inside <see cref="StaticDirectory"/>.</summary>
    public string LocalPolicyFileName { get; set; } = "policy.txt";

    /// <summary>Subdirectory of <see cref="StaticDirectory"/> the patch files are cached in.</summary>
    public string LocalLauncherDirectoryName { get; set; } = "files";

    /// <summary>Subdirectory of <see cref="StaticDirectory"/> the help files are stored in.</summary>
    public string LocalHelpDirectoryName { get; set; } = "help";

    /// <summary>How long an upstream fetch may take before it is abandoned.</summary>
    public int UpstreamFetchTimeoutMilliseconds { get; set; } = 3000;

    /// <summary>Secret the bearer tokens are validated with.</summary>
    public string JwtSecret { get; set; } = string.Empty;

    /// <summary>Path of the policy document inside <see cref="StaticDirectory"/>.</summary>
    public string LocalPolicyPath => Path.Combine(StaticDirectory, LocalPolicyFileName);

    /// <summary>Path of the launcher cache directory inside <see cref="StaticDirectory"/>.</summary>
    public string LocalLauncherPath => Path.Combine(StaticDirectory, LocalLauncherDirectoryName);

    /// <summary>Path of the help directory inside <see cref="StaticDirectory"/>.</summary>
    public string LocalHelpPath => Path.Combine(StaticDirectory, LocalHelpDirectoryName);
}
