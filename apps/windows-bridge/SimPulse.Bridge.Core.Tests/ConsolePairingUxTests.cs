using Microsoft.Extensions.Logging;

using SimPulse.Bridge.Core.Adapters;

namespace SimPulse.Bridge.Core.Tests;

public sealed class ConsolePairingUxTests
{
    private static readonly DateTimeOffset ExpiresAt = DateTimeOffset.Parse("2026-08-18T10:05:00Z");

    [Fact]
    public void ShowPin_logs_visibility_without_pin()
    {
        ListLogger<ConsolePairingUx> logger = new();
        ConsolePairingUx ux = new(logger);

        ux.ShowPin("123456", ExpiresAt);

        Assert.Contains(
            logger.Entries,
            e => e.Level == LogLevel.Information
                && e.Message.Contains("ExpiresAtUtc", StringComparison.Ordinal)
                && e.Message.Contains("Pairing PIN is visible", StringComparison.Ordinal));
        Assert.DoesNotContain(
            logger.Entries,
            e => e.Message.Contains("123456", StringComparison.Ordinal)
                || e.Message.Contains("Pin=", StringComparison.Ordinal));
    }

    [Fact]
    public void ShowStatus_logs_message_at_information()
    {
        ListLogger<ConsolePairingUx> logger = new();
        ConsolePairingUx ux = new(logger);

        ux.ShowStatus("Waiting for device");

        Assert.Contains(
            logger.Entries,
            e => e.Level == LogLevel.Information
                && e.Message.Contains("Waiting for device", StringComparison.Ordinal));
    }

    [Fact]
    public void RequestPairNewDevice_raises_pair_new_device_event()
    {
        ListLogger<ConsolePairingUx> logger = new();
        ConsolePairingUx ux = new(logger);
        bool raised = false;
        ux.PairNewDeviceRequested += () => raised = true;

        ux.RequestPairNewDevice();

        Assert.True(raised);
    }

    [Fact]
    public void RedisplayLastPin_does_not_log_pin()
    {
        ListLogger<ConsolePairingUx> logger = new();
        ConsolePairingUx ux = new(logger);
        ux.ShowPin("123456", ExpiresAt);
        logger.Entries.Clear();

        ux.RedisplayLastPin();

        Assert.Contains(
            logger.Entries,
            e => e.Level == LogLevel.Information
                && e.Message.Contains("redisplayed", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            logger.Entries,
            e => e.Message.Contains("123456", StringComparison.Ordinal)
                || e.Message.Contains("Pin=", StringComparison.Ordinal));
    }
}
