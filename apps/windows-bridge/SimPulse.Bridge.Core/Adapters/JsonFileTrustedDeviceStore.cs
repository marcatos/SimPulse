using System.Diagnostics;
using System.Text.Json;

using Microsoft.Extensions.Logging;

using SimPulse.Bridge.Core.Application;
using SimPulse.Bridge.Core.Ports;

namespace SimPulse.Bridge.Core.Adapters;

public sealed class JsonFileTrustedDeviceStore : ITrustedDeviceStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly string _path;
    private readonly ILogger<JsonFileTrustedDeviceStore> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public JsonFileTrustedDeviceStore(string path, ILogger<JsonFileTrustedDeviceStore> logger)
    {
        _path = path;
        _logger = logger;
    }

    public async Task<IReadOnlyList<TrustedDevice>> ListAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return LoadUnlocked();
        }
        finally
        {
            _gate.Release();
        }
    }

    public Task TrustAsync(
        string deviceId,
        DateTimeOffset trustedAtUtc,
        string reconnectTokenSha256,
        CancellationToken cancellationToken)
    {
        return MutateAsync(
            devices =>
            {
                devices.RemoveAll(d => string.Equals(d.DeviceId, deviceId, StringComparison.Ordinal));
                devices.Add(new TrustedDevice(
                    deviceId,
                    trustedAtUtc,
                    Revoked: false,
                    reconnectTokenSha256));
            },
            "Trust",
            cancellationToken);
    }

    public Task RevokeAsync(string deviceId, CancellationToken cancellationToken)
    {
        return MutateAsync(
            devices =>
            {
                int index = devices.FindIndex(d => string.Equals(d.DeviceId, deviceId, StringComparison.Ordinal));
                if (index >= 0)
                {
                    devices[index] = devices[index] with { Revoked = true };
                }
            },
            "Revoke",
            cancellationToken);
    }

    public async Task<bool> AuthorizeReconnectAsync(
        string deviceId,
        string? reconnectTokenHex,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<TrustedDevice> devices = await ListAsync(cancellationToken);
        TrustedDevice? device = devices.FirstOrDefault(
            d => string.Equals(d.DeviceId, deviceId, StringComparison.Ordinal));
        return device is not null
            && !device.Revoked
            && ReconnectToken.MatchesStoredHash(device.ReconnectTokenSha256, reconnectTokenHex);
    }

    private async Task MutateAsync(
        Action<List<TrustedDevice>> mutate,
        string operation,
        CancellationToken cancellationToken)
    {
        Stopwatch started = Stopwatch.StartNew();
        _logger.LogInformation(
            "Trusted-device store {Operation} starting. Component={Component}",
            operation,
            "JsonFileTrustedDeviceStore");

        await _gate.WaitAsync(cancellationToken);
        try
        {
            List<TrustedDevice> devices = LoadUnlocked();
            mutate(devices);
            SaveUnlocked(devices);
            _logger.LogInformation(
                "Trusted-device store {Operation} finished. Count={Count} ElapsedMs={ElapsedMs}",
                operation,
                devices.Count,
                started.ElapsedMilliseconds);
        }
        finally
        {
            _gate.Release();
        }
    }

    private List<TrustedDevice> LoadUnlocked()
    {
        if (!File.Exists(_path))
        {
            return [];
        }

        string json = File.ReadAllText(_path);
        return JsonSerializer.Deserialize<List<TrustedDevice>>(json, JsonOptions) ?? [];
    }

    private void SaveUnlocked(List<TrustedDevice> devices)
    {
        string? directory = Path.GetDirectoryName(Path.GetFullPath(_path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string tempPath = _path + ".tmp";
        string json = JsonSerializer.Serialize(devices, JsonOptions);
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, _path, overwrite: true);
    }
}
