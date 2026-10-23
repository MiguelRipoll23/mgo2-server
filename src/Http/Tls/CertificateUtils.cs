using System.Security.Cryptography.X509Certificates;

namespace Mgo2Server.Http.Tls;

/// <summary>
/// Reads the certificate pair the server serves HTTPS from.
/// </summary>
public static class CertificateUtils
{
    /// <summary>
    /// Loads the PEM certificate and key the deployment mounted.
    /// </summary>
    /// <param name="certificatePath">PEM file holding the server certificate.</param>
    /// <param name="keyPath">PEM file holding the certificate's private key.</param>
    /// <returns>
    /// The pair as one object, or <see langword="null"/> when neither path is
    /// configured — which leaves the server on its plaintext port alone.
    /// </returns>
    /// <exception cref="FileNotFoundException">
    /// A path is configured but the file is not there. That is a broken mount
    /// rather than a server to run without HTTPS, and starting anyway would
    /// serve the console's port while silently answering nothing on the other.
    /// </exception>
    public static X509Certificate2? LoadFromPem(string? certificatePath, string? keyPath)
    {
        if (string.IsNullOrWhiteSpace(certificatePath) || string.IsNullOrWhiteSpace(keyPath))
        {
            return null;
        }

        if (!File.Exists(certificatePath))
        {
            throw new FileNotFoundException(
                $"The TLS certificate {certificatePath} is configured but not mounted.",
                certificatePath);
        }

        if (!File.Exists(keyPath))
        {
            throw new FileNotFoundException(
                $"The TLS private key {keyPath} is configured but not mounted.",
                keyPath);
        }

        var pair = X509Certificate2.CreateFromPemFile(certificatePath, keyPath);

        // A key read from a PEM file is ephemeral, and SChannel will not use one
        // it never imported. Round-tripping through PKCS#12 gives Windows a key
        // it accepts, and the deployment is Linux where the pair is used as read.
        return OperatingSystem.IsWindows()
            ? X509CertificateLoader.LoadPkcs12(pair.Export(X509ContentType.Pfx), null)
            : pair;
    }
}
