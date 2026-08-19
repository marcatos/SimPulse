using System.Security.Cryptography;

using SimPulse.Bridge.Core.Application;

namespace SimPulse.Bridge.Core.Tests;

public sealed class ReconnectTokenTests
{
    [Fact]
    public void CreateRaw_is_32_bytes()
    {
        byte[] raw = ReconnectToken.CreateRaw();
        Assert.Equal(32, raw.Length);
    }

    [Fact]
    public void ToHex_is_64_lowercase()
    {
        byte[] raw = Enumerable.Repeat((byte)0xAB, 32).ToArray();
        string hex = ReconnectToken.ToHex(raw);
        Assert.Equal(64, hex.Length);
        Assert.Equal(hex, hex.ToLowerInvariant());
        Assert.DoesNotContain(":", hex, StringComparison.Ordinal);
    }

    [Fact]
    public void MatchesStoredHash_true_for_same_raw_bytes()
    {
        byte[] raw = Enumerable.Repeat((byte)0x11, 32).ToArray();
        string hex = ReconnectToken.ToHex(raw);
        string hash = ReconnectToken.Sha256Hex(raw);
        Assert.True(ReconnectToken.MatchesStoredHash(hash, hex));
    }

    [Fact]
    public void MatchesStoredHash_false_for_wrong_token_uppercase_hex_or_legacy_null()
    {
        byte[] raw = Enumerable.Repeat((byte)0xAB, 32).ToArray();
        string hex = ReconnectToken.ToHex(raw);
        string hash = ReconnectToken.Sha256Hex(raw);
        Assert.False(ReconnectToken.MatchesStoredHash(hash, hex[..^1] + "0"));
        Assert.False(ReconnectToken.MatchesStoredHash(hash, hex.ToUpperInvariant()));
        Assert.False(ReconnectToken.MatchesStoredHash(null, hex));
        Assert.False(ReconnectToken.MatchesStoredHash(hash, null));
    }

    [Fact]
    public void Sha256Hex_hashes_raw_bytes_not_utf8_hex_string()
    {
        byte[] raw = Enumerable.Repeat((byte)0x11, 32).ToArray();
        string hex = ReconnectToken.ToHex(raw);
        string ofRaw = ReconnectToken.Sha256Hex(raw);
        string ofUtf8Hex = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(hex))).ToLowerInvariant();
        Assert.NotEqual(ofRaw, ofUtf8Hex);
    }
}
