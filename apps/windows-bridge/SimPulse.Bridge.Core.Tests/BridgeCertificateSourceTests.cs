using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;

using Microsoft.Extensions.Logging.Abstractions;

using SimPulse.Bridge.Core.Adapters;

namespace SimPulse.Bridge.Core.Tests;

public sealed class BridgeCertificateSourceTests
{
    private static readonly Regex LowerHex64 = new("^[0-9a-f]{64}$", RegexOptions.Compiled);

    [Fact]
    public void GetOrCreate_creates_pfx_when_missing_and_second_call_keeps_same_fingerprint()
    {
        using TempCertDirectory directory = new();
        FileBridgeCertificateSource source = CreateSource(certDirectory: directory.Path);

        X509Certificate2 first = source.GetOrCreate();
        string expectedPath = Path.Combine(directory.Path, "bridge-dev.pfx");
        Assert.True(File.Exists(expectedPath));

        string firstFingerprint = source.Sha256FingerprintHex;
        Assert.Equal(ComputeFingerprint(first), firstFingerprint);

        X509Certificate2 second = source.GetOrCreate();
        Assert.Equal(firstFingerprint, source.Sha256FingerprintHex);
        Assert.Equal(first.Thumbprint, second.Thumbprint);
    }

    [Fact]
    public void GetOrCreate_restricts_pfx_to_current_user()
    {
        using TempCertDirectory directory = new();
        FileBridgeCertificateSource source = CreateSource(certDirectory: directory.Path);
        string expectedPath = Path.Combine(directory.Path, "bridge-dev.pfx");

        source.GetOrCreate();

        if (OperatingSystem.IsWindows())
        {
            AssertWindowsCurrentUserOnlyAcl(expectedPath);
        }
        else if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            UnixFileMode mode = File.GetUnixFileMode(expectedPath);
            UnixFileMode nonOwnerPermissions =
                UnixFileMode.GroupRead |
                UnixFileMode.GroupWrite |
                UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead |
                UnixFileMode.OtherWrite |
                UnixFileMode.OtherExecute;

            Assert.Equal(UnixFileMode.None, mode & nonOwnerPermissions);
            Assert.Equal(
                UnixFileMode.UserRead | UnixFileMode.UserWrite,
                mode & (UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute));
        }
    }

    [Fact]
    public async Task Concurrent_GetOrCreate_uses_the_same_persisted_certificate()
    {
        using TempCertDirectory directory = new();
        FileBridgeCertificateSource firstSource = CreateSource(certDirectory: directory.Path);
        FileBridgeCertificateSource secondSource = CreateSource(certDirectory: directory.Path);
        using Barrier start = new(participantCount: 2);

        Task<X509Certificate2> firstTask = Task.Run(() =>
        {
            start.SignalAndWait();
            return firstSource.GetOrCreate();
        });
        Task<X509Certificate2> secondTask = Task.Run(() =>
        {
            start.SignalAndWait();
            return secondSource.GetOrCreate();
        });

        await Task.WhenAll(firstTask, secondTask);

        Assert.Equal(firstSource.Sha256FingerprintHex, secondSource.Sha256FingerprintHex);
        FileBridgeCertificateSource diskSource = CreateSource(certDirectory: directory.Path);
        diskSource.GetOrCreate();
        Assert.Equal(firstSource.Sha256FingerprintHex, diskSource.Sha256FingerprintHex);
    }

    [SupportedOSPlatform("windows")]
    private static void AssertWindowsCurrentUserOnlyAcl(string path)
    {
        FileSecurity security = new FileInfo(path).GetAccessControl();
        SecurityIdentifier currentUser = WindowsIdentity.GetCurrent().User
            ?? throw new InvalidOperationException("Current Windows user has no security identifier.");
        AuthorizationRuleCollection rules = security.GetAccessRules(
            includeExplicit: true,
            includeInherited: true,
            typeof(SecurityIdentifier));

        Assert.True(security.AreAccessRulesProtected);
        Assert.All(
            rules.OfType<FileSystemAccessRule>().Where(rule => rule.AccessControlType == AccessControlType.Allow),
            rule => Assert.Equal(currentUser, rule.IdentityReference));
    }

    [Fact]
    public void New_instance_on_same_path_reloads_same_fingerprint()
    {
        using TempCertDirectory directory = new();
        FileBridgeCertificateSource firstSource = CreateSource(certDirectory: directory.Path);
        string fingerprint = firstSource.GetOrCreate().RawData.Length > 0
            ? firstSource.Sha256FingerprintHex
            : throw new InvalidOperationException("Expected certificate to be created.");

        FileBridgeCertificateSource reloaded = CreateSource(certDirectory: directory.Path);
        Assert.Equal(fingerprint, reloaded.Sha256FingerprintHex);
        Assert.Equal(fingerprint, ComputeFingerprint(reloaded.GetOrCreate()));
    }

    [Fact]
    public void GetOrCreate_loads_from_explicit_path()
    {
        using TempCertDirectory directory = new();
        string explicitPath = Path.Combine(directory.Path, "custom-bridge.pfx");

        FileBridgeCertificateSource creator = CreateSource(certDirectory: directory.Path);
        X509Certificate2 created = creator.GetOrCreate();
        string expectedFingerprint = ComputeFingerprint(created);
        File.WriteAllBytes(explicitPath, created.Export(X509ContentType.Pfx));

        FileBridgeCertificateSource loaded = CreateSource(
            certPath: explicitPath,
            certDirectory: directory.Path);
        X509Certificate2 fromPath = loaded.GetOrCreate();

        Assert.Equal(expectedFingerprint, loaded.Sha256FingerprintHex);
        Assert.Equal(expectedFingerprint, ComputeFingerprint(fromPath));
    }

    [Fact]
    public void Sha256FingerprintHex_is_64_lowercase_hex_chars()
    {
        using TempCertDirectory directory = new();
        FileBridgeCertificateSource source = CreateSource(certDirectory: directory.Path);

        source.GetOrCreate();
        string fingerprint = source.Sha256FingerprintHex;

        Assert.Equal(64, fingerprint.Length);
        Assert.Matches(LowerHex64, fingerprint);
    }

    private static FileBridgeCertificateSource CreateSource(
        string? certPath = null,
        string? certPassword = null,
        string? certDirectory = null)
    {
        return new FileBridgeCertificateSource(
            certPath,
            certPassword,
            certDirectory ?? throw new ArgumentNullException(nameof(certDirectory)),
            NullLogger.Instance);
    }

    private static string ComputeFingerprint(X509Certificate2 certificate)
    {
        return Convert.ToHexString(SHA256.HashData(certificate.RawData)).ToLowerInvariant();
    }

    private sealed class TempCertDirectory : IDisposable
    {
        public TempCertDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "simpulse-bridge-certs-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                {
                    Directory.Delete(Path, recursive: true);
                }
            }
            catch (IOException)
            {
            }
        }
    }
}
