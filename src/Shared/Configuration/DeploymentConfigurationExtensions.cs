using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace Mgo2Server.Shared.Configuration;

/// <summary>
/// Loads the configuration of the deployment into the shared configuration of
/// every server. The deployment carries two settings that belong to it rather
/// than to the servers, the JWT secret and the private address the clients are
/// told to connect to, and the install scripts write them into a
/// deployment.json next to the compose file. A deployment mounts the file next
/// to the appsettings.json it also mounts, and the servers add it to their
/// configuration after their own sources, so a value there overrides
/// appsettings.json and is still overridden by a real environment variable,
/// which keeps the per-container identity of compose.yaml the strongest voice.
/// An edited file is applied by restarting the container, exactly like the
/// mounted appsettings.json.
/// </summary>
public static class DeploymentConfigurationExtensions
{
    /// <summary>Name of the file a deployment mounts its settings in.</summary>
    public const string DeploymentFileName = "deployment.json";

    /// <summary>
    /// Adds deployment.json from the application directory to the sources of
    /// the builder, when the deployment mounts one. A value of the file beats
    /// appsettings.json and loses against a real environment variable of the
    /// same name.
    /// </summary>
    /// <param name="builder">Builder to add the file to.</param>
    /// <returns>The same builder, so the call can be chained.</returns>
    /// <exception cref="InvalidOperationException">The file is not a JSON object of scalar settings.</exception>
    public static IConfigurationBuilder AddDeploymentJsonFile(this IConfigurationBuilder builder)
    {
        // The file lives next to the application, which is where the compose
        // mounts it, and a clone without one simply runs on its defaults.
        var path = Path.Combine(AppContext.BaseDirectory, DeploymentFileName);
        if (!File.Exists(path))
        {
            return builder;
        }

        // The provider writes every top-level property under its own path, so
        // an upper-case setting name lands exactly where every server reads
        // its settings from.
        return builder.AddJsonFile(path, optional: false, reloadOnChange: false);
    }
}
