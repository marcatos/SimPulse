using System.Diagnostics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Microsoft.Extensions.Logging;

using SimPulse.Bridge.Core.Ports;

namespace SimPulse.Bridge.Core.Adapters;

public sealed class FileBridgeCertificateSource : IBridgeCertificateSource
{
    private const string DefaultFileName = "bridge-dev.pfx";
    private const string SubjectName = "CN=SimPulse Bridge";
    private const int ValidityDays = 825;

    private readonly string? _certPath;
    private readonly string? _certPassword;
    private readonly string _certDirectory;
    private readonly ILogger _logger;
    private readonly object _gate = new();

    private X509Certificate2? _certificate;
    private string? _fingerprintHex;

    public FileBridgeCertificateSource(
        string? certPath,
        string? certPassword,
        string certDirectory,
        ILogger logger)
    {
        _certPath = string.IsNullOrWhiteSpace(certPath) ? null : certPath;
        _certPassword = certPassword;
        _certDirectory = certDirectory;
        _logger = logger;
    }

    public string Sha256FingerprintHex
    {
        get
        {
            EnsureLoaded();
            return _fingerprintHex!;
        }
    }

    public X509Certificate2 GetOrCreate()
    {
        EnsureLoaded();
        return _certificate!;
    }

    private void EnsureLoaded()
    {
        if (_certificate is not null)
        {
            return;
        }

        lock (_gate)
        {
            if (_certificate is not null)
            {
                return;
            }

            Stopwatch started = Stopwatch.StartNew();
            string resolvedPath = ResolvePath();
            _logger.LogInformation(
                "Bridge certificate load starting. Path={Path} Component={Component}",
                resolvedPath,
                nameof(FileBridgeCertificateSource));

            if (File.Exists(resolvedPath))
            {
                _certificate = LoadFromFile(resolvedPath);
                _logger.LogInformation(
                    "Bridge certificate loaded from file. Path={Path} ElapsedMs={ElapsedMs}",
                    resolvedPath,
                    started.ElapsedMilliseconds);
            }
            else
            {
                if (_certPath is not null)
                {
                    throw new FileNotFoundException(
                        "Configured bridge certificate file was not found.",
                        resolvedPath);
                }

                _certificate = CreateAndPersist(resolvedPath);
                _logger.LogInformation(
                    "Bridge self-signed certificate created. Path={Path} ElapsedMs={ElapsedMs}",
                    resolvedPath,
                    started.ElapsedMilliseconds);
            }

            _fingerprintHex = ComputeFingerprintHex(_certificate);
            _logger.LogInformation(
                "Bridge certificate ready. TlsCertSha256={TlsCertSha256} ElapsedMs={ElapsedMs}",
                _fingerprintHex,
                started.ElapsedMilliseconds);
        }
    }

    private string ResolvePath()
    {
        if (_certPath is not null)
        {
            return _certPath;
        }

        return Path.Combine(_certDirectory, DefaultFileName);
    }

    private X509Certificate2 LoadFromFile(string path)
    {
        string password = _certPassword ?? string.Empty;
        return new X509Certificate2(
            path,
            password,
            X509KeyStorageFlags.Exportable | X509KeyStorageFlags.UserKeySet);
    }

    private X509Certificate2 CreateAndPersist(string path)
    {
        string? directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using RSA rsa = RSA.Create(2048);
        CertificateRequest request = new(
            SubjectName,
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(certificateAuthority: false, hasPathLengthConstraint: false, pathLengthConstraint: 0, critical: false));
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
                critical: false));
        request.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension(
                new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") },
                critical: false));

        DateTimeOffset notBefore = DateTimeOffset.UtcNow.AddMinutes(-5);
        DateTimeOffset notAfter = notBefore.AddDays(ValidityDays);
        using X509Certificate2 created = request.CreateSelfSigned(notBefore, notAfter);

        string password = _certPassword ?? string.Empty;
        byte[] pfxBytes = created.Export(X509ContentType.Pfx, password);
        File.WriteAllBytes(path, pfxBytes);

        return new X509Certificate2(
            pfxBytes,
            password,
            X509KeyStorageFlags.Exportable | X509KeyStorageFlags.UserKeySet);
    }

    private static string ComputeFingerprintHex(X509Certificate2 certificate)
    {
        return Convert.ToHexString(SHA256.HashData(certificate.RawData)).ToLowerInvariant();
    }
}
