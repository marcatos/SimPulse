using System.Security.Cryptography;

namespace SimPulse.Bridge.Core.Application;

public static class ReconnectToken
{
    public const int RawLength = 32;

    public static byte[] CreateRaw()
    {
        byte[] raw = new byte[RawLength];
        RandomNumberGenerator.Fill(raw);
        return raw;
    }

    public static string ToHex(ReadOnlySpan<byte> raw)
    {
        return Convert.ToHexString(raw).ToLowerInvariant();
    }

    public static bool TryParseHex(string? hex, out byte[] raw)
    {
        raw = [];
        if (hex is null || hex.Length != RawLength * 2 || hex.Any(c => c is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
        {
            return false;
        }

        raw = Convert.FromHexString(hex);
        return true;
    }

    public static string Sha256Hex(ReadOnlySpan<byte> raw)
    {
        return ToHex(SHA256.HashData(raw));
    }

    public static bool MatchesStoredHash(string? storedSha256Hex, string? reconnectTokenHex)
    {
        if (string.IsNullOrEmpty(storedSha256Hex) || !TryParseHex(reconnectTokenHex, out byte[] raw))
        {
            return false;
        }

        try
        {
            byte[] storedHash = Convert.FromHexString(storedSha256Hex);
            if (storedHash.Length != SHA256.HashSizeInBytes)
            {
                return false;
            }

            byte[] candidateHash = SHA256.HashData(raw);
            return CryptographicOperations.FixedTimeEquals(storedHash, candidateHash);
        }
        catch (FormatException)
        {
            return false;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(raw);
        }
    }
}
