using Microsoft.Extensions.Logging.Abstractions;

using SimPulse.Bridge.Core.Adapters;
using SimPulse.Bridge.Core.Application;
using SimPulse.Bridge.Core.Ports;

namespace SimPulse.Bridge.Core.Tests;

public sealed class JsonFileTrustedDeviceStoreTests
{
    [Fact]
    public async Task Round_trips_trusted_device_across_store_instances()
    {
        using TempStoreFile file = new();
        DateTimeOffset trustedAt = DateTimeOffset.Parse("2026-08-18T10:00:00Z");
        byte[] raw = Enumerable.Repeat((byte)0x11, 32).ToArray();
        string tokenHex = ReconnectToken.ToHex(raw);
        string hash = ReconnectToken.Sha256Hex(raw);

        JsonFileTrustedDeviceStore first = new(file.Path, NullLogger<JsonFileTrustedDeviceStore>.Instance);
        await first.TrustAsync("phone-1", trustedAt, hash, CancellationToken.None);
        Assert.True(await first.AuthorizeReconnectAsync("phone-1", tokenHex, CancellationToken.None));

        JsonFileTrustedDeviceStore reloaded = new(file.Path, NullLogger<JsonFileTrustedDeviceStore>.Instance);
        Assert.True(await reloaded.AuthorizeReconnectAsync("phone-1", tokenHex, CancellationToken.None));
        IReadOnlyList<TrustedDevice> listed = await reloaded.ListAsync(CancellationToken.None);
        Assert.Single(listed);
        Assert.Equal("phone-1", listed[0].DeviceId);
        Assert.Equal(trustedAt, listed[0].TrustedAtUtc);
        Assert.False(listed[0].Revoked);
        Assert.Equal(hash, listed[0].ReconnectTokenSha256);

        string persisted = File.ReadAllText(file.Path);
        Assert.Contains("\"reconnectTokenSha256\"", persisted, StringComparison.Ordinal);
        Assert.Contains(hash, persisted, StringComparison.Ordinal);
        Assert.DoesNotContain(tokenHex, persisted, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Revoked_device_is_not_trusted_after_reload()
    {
        using TempStoreFile file = new();
        DateTimeOffset trustedAt = DateTimeOffset.Parse("2026-08-18T10:00:00Z");
        byte[] raw = Enumerable.Repeat((byte)0x22, 32).ToArray();
        string tokenHex = ReconnectToken.ToHex(raw);
        string hash = ReconnectToken.Sha256Hex(raw);

        JsonFileTrustedDeviceStore first = new(file.Path, NullLogger<JsonFileTrustedDeviceStore>.Instance);
        await first.TrustAsync("phone-1", trustedAt, hash, CancellationToken.None);
        await first.RevokeAsync("phone-1", CancellationToken.None);
        Assert.False(await first.AuthorizeReconnectAsync("phone-1", tokenHex, CancellationToken.None));

        JsonFileTrustedDeviceStore reloaded = new(file.Path, NullLogger<JsonFileTrustedDeviceStore>.Instance);
        Assert.False(await reloaded.AuthorizeReconnectAsync("phone-1", tokenHex, CancellationToken.None));
        IReadOnlyList<TrustedDevice> listed = await reloaded.ListAsync(CancellationToken.None);
        Assert.True(listed[0].Revoked);
    }

    [Fact]
    public async Task Missing_file_starts_empty()
    {
        using TempStoreFile file = new();
        File.Delete(file.Path);

        JsonFileTrustedDeviceStore store = new(file.Path, NullLogger<JsonFileTrustedDeviceStore>.Instance);
        Assert.False(await store.AuthorizeReconnectAsync("missing", null, CancellationToken.None));
        Assert.Empty(await store.ListAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Legacy_json_without_hash_is_not_authorized()
    {
        using TempStoreFile file = new();
        File.WriteAllText(file.Path, """
            [{"deviceId":"phone-old","trustedAtUtc":"2026-08-18T10:00:00+00:00","revoked":false}]
            """);
        JsonFileTrustedDeviceStore store = new(file.Path, NullLogger<JsonFileTrustedDeviceStore>.Instance);
        string anyToken = ReconnectToken.ToHex(Enumerable.Repeat((byte)0x22, 32).ToArray());
        Assert.False(await store.AuthorizeReconnectAsync("phone-old", anyToken, CancellationToken.None));
    }

    private sealed class TempStoreFile : IDisposable
    {
        public TempStoreFile()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "simpulse-trusted-" + Guid.NewGuid().ToString("N") + ".json");
        }

        public string Path { get; }

        public void Dispose()
        {
            TryDelete(Path);
            TryDelete(Path + ".tmp");
            TryDelete(Path + ".bak");
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (IOException)
            {
            }
        }
    }
}
