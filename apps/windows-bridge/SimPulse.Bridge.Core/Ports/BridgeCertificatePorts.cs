using System.Security.Cryptography.X509Certificates;

namespace SimPulse.Bridge.Core.Ports;

public interface IBridgeCertificateSource
{
    /// <summary>Certificate used for TLS listen. Caller must not log private key material.</summary>
    X509Certificate2 GetOrCreate();

    /// <summary>Lowercase hex SHA-256 of the certificate DER, without colons.</summary>
    string Sha256FingerprintHex { get; }
}
