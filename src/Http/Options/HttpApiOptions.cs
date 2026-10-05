namespace Mgo2Server.Http.Options;

/// <summary>
/// Options that describe how the HTTP API runs. They are read from the flat
/// upper-case environment variables named in the setup documentation.
/// </summary>
public sealed class HttpApiOptions
{
    /// <summary>
    /// Directory holding the policy file, the patch files and the help texts.
    /// Resolved from the application base so it is found whether the API runs
    /// from a publish folder (Docker) or a build output (the run scripts).
    /// </summary>
    public string StaticDirectory { get; set; } = Path.Combine(AppContext.BaseDirectory, "Static");

    /// <summary>File name of the policy document inside <see cref="StaticDirectory"/>.</summary>
    public string LocalPolicyFileName { get; set; } = "policy.txt";

    /// <summary>Subdirectory of <see cref="StaticDirectory"/> the patch files are served from.</summary>
    public string LocalLauncherDirectoryName { get; set; } = "files";

    /// <summary>Subdirectory of <see cref="StaticDirectory"/> the help files are stored in.</summary>
    public string LocalHelpDirectoryName { get; set; } = "help";

    /// <summary>Path of the policy document inside <see cref="StaticDirectory"/>.</summary>
    public string LocalPolicyPath => Path.Combine(StaticDirectory, LocalPolicyFileName);

    /// <summary>Path of the launcher files directory inside <see cref="StaticDirectory"/>.</summary>
    public string LocalLauncherPath => Path.Combine(StaticDirectory, LocalLauncherDirectoryName);

    /// <summary>Path of the help directory inside <see cref="StaticDirectory"/>.</summary>
    public string LocalHelpPath => Path.Combine(StaticDirectory, LocalHelpDirectoryName);
}
