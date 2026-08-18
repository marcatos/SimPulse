using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using SimPulse.Bridge.Core.Adapters;
using SimPulse.Bridge.Core.Application;
using SimPulse.Bridge.Core.Ports;
using SimPulse.Protocol;

namespace SimPulse.Bridge.Core.Tests;

public sealed class TrayPairingPresenterTests
{
    private const string FirstPin = "111111";
    private const string SecondPin = "222222";
    private static readonly DateTimeOffset TrustedAt = DateTimeOffset.Parse("2026-08-18T10:00:00Z");

    [Fact]
    public async Task Pair_new_device_opens_a_new_window_after_successful_pair()
    {
        PairingCoordinator coordinator = new(
            new InMemoryTrustedDeviceStore(),
            new FixedClock(TrustedAt),
            new SequencePinGenerator(FirstPin, SecondPin),
            NullLogger<PairingCoordinator>.Instance);
        PairingWindowInfo firstWindow = coordinator.BeginPairingWindow();
        FakeClientConnection firstDevice = new() { DeviceId = "phone-1" };
        await coordinator.HandleAsync(
            firstDevice,
            PairingEnvelope("phone-1", firstWindow.Pin),
            CancellationToken.None);
        Assert.True(firstDevice.IsTrusted);

        FakePairingUx ux = new();
        _ = new TrayPairingPresenter(coordinator, ux, NullLogger<TrayPairingPresenter>.Instance);
        ux.RaisePairNewDevice();

        Assert.NotNull(ux.LastPin);
        Assert.NotEqual(firstWindow.Pin, ux.LastPin);
        Assert.Equal(SecondPin, ux.LastPin);
        Assert.Equal(TrustedAt.Add(PairingCoordinator.WindowDuration), ux.LastExpiresAtUtc);

        FakeClientConnection secondDevice = new() { DeviceId = "phone-2" };
        await coordinator.HandleAsync(
            secondDevice,
            PairingEnvelope("phone-2", ux.LastPin!),
            CancellationToken.None);

        Assert.True(secondDevice.IsTrusted);
    }

    [Fact]
    public void OnWindowOpened_shows_pin_without_opening_another_window()
    {
        PairingCoordinator coordinator = new(
            new InMemoryTrustedDeviceStore(),
            new FixedClock(TrustedAt),
            new SequencePinGenerator(FirstPin),
            NullLogger<PairingCoordinator>.Instance);
        FakePairingUx ux = new();
        TrayPairingPresenter presenter = new(coordinator, ux, NullLogger<TrayPairingPresenter>.Instance);
        DateTimeOffset expiresAt = TrustedAt.Add(PairingCoordinator.WindowDuration);

        presenter.OnWindowOpened(FirstPin, expiresAt);

        Assert.Equal(FirstPin, ux.LastPin);
        Assert.Equal(expiresAt, ux.LastExpiresAtUtc);
        Assert.Equal(1, ux.ShowPinCount);
    }

    [Fact]
    public void Show_current_pin_redisplays_last_pin_without_opening_a_new_window()
    {
        PairingCoordinator coordinator = new(
            new InMemoryTrustedDeviceStore(),
            new FixedClock(TrustedAt),
            new SequencePinGenerator(FirstPin, SecondPin),
            NullLogger<PairingCoordinator>.Instance);
        FakePairingUx ux = new();
        TrayPairingPresenter presenter = new(coordinator, ux, NullLogger<TrayPairingPresenter>.Instance);
        PairingWindowInfo firstWindow = coordinator.BeginPairingWindow();
        presenter.OnWindowOpened(firstWindow.Pin, firstWindow.ExpiresAtUtc);

        ux.RaiseShowCurrentPin();

        Assert.Equal(FirstPin, ux.LastRedisplayedPin);
        Assert.Equal(1, ux.ShowPinCount);
        Assert.Equal(1, ux.RedisplayCount);
        Assert.Equal(FirstPin, ux.LastPin);

        ux.RaisePairNewDevice();

        Assert.Equal(SecondPin, ux.LastPin);
        Assert.Equal(2, ux.ShowPinCount);
    }

    [Fact]
    public async Task Show_current_pin_after_successful_pair_does_not_redisplay_consumed_pin()
    {
        PairingCoordinator coordinator = new(
            new InMemoryTrustedDeviceStore(),
            new FixedClock(TrustedAt),
            new SequencePinGenerator(FirstPin),
            NullLogger<PairingCoordinator>.Instance);
        PairingWindowInfo window = coordinator.BeginPairingWindow();
        FakePairingUx ux = new();
        TrayPairingPresenter presenter = new(coordinator, ux, NullLogger<TrayPairingPresenter>.Instance);
        presenter.OnWindowOpened(window.Pin, window.ExpiresAtUtc);
        FakeClientConnection device = new() { DeviceId = "phone-1" };
        await coordinator.HandleAsync(
            device,
            PairingEnvelope("phone-1", window.Pin),
            CancellationToken.None);
        Assert.True(device.IsTrusted);

        ux.RaiseShowCurrentPin();

        Assert.Null(ux.LastRedisplayedPin);
        Assert.Null(ux.LastPin);
        Assert.Equal("pairing window closed", ux.LastStatus);
        Assert.Equal(0, ux.RedisplayCount);
    }

    [Fact]
    public void Show_current_pin_after_expiry_reports_window_closed()
    {
        MutableClock clock = new(TrustedAt);
        PairingCoordinator coordinator = new(
            new InMemoryTrustedDeviceStore(),
            clock,
            new SequencePinGenerator(FirstPin),
            NullLogger<PairingCoordinator>.Instance);
        PairingWindowInfo window = coordinator.BeginPairingWindow();
        FakePairingUx ux = new();
        TrayPairingPresenter presenter = new(coordinator, ux, NullLogger<TrayPairingPresenter>.Instance);
        presenter.OnWindowOpened(window.Pin, window.ExpiresAtUtc);

        clock.UtcNow = TrustedAt.Add(PairingCoordinator.WindowDuration).AddSeconds(1);
        ux.RaiseShowCurrentPin();

        Assert.Null(ux.LastRedisplayedPin);
        Assert.Null(ux.LastPin);
        Assert.Equal("pairing window closed", ux.LastStatus);
        Assert.Equal(0, ux.RedisplayCount);
    }

    [Fact]
    public async Task Show_current_pin_after_lockout_reports_window_closed()
    {
        PairingCoordinator coordinator = new(
            new InMemoryTrustedDeviceStore(),
            new FixedClock(TrustedAt),
            new SequencePinGenerator(FirstPin),
            NullLogger<PairingCoordinator>.Instance);
        PairingWindowInfo window = coordinator.BeginPairingWindow();
        FakePairingUx ux = new();
        TrayPairingPresenter presenter = new(coordinator, ux, NullLogger<TrayPairingPresenter>.Instance);
        presenter.OnWindowOpened(window.Pin, window.ExpiresAtUtc);
        for (int i = 0; i < PairingCoordinator.MaxFailedAttempts; i++)
        {
            FakeClientConnection failed = new() { DeviceId = $"phone-bad-{i}" };
            await coordinator.HandleAsync(
                failed,
                PairingEnvelope(failed.DeviceId!, "000000"),
                CancellationToken.None);
        }

        ux.RaiseShowCurrentPin();

        Assert.Null(ux.LastRedisplayedPin);
        Assert.Null(ux.LastPin);
        Assert.Equal("pairing window closed", ux.LastStatus);
        Assert.Equal(0, ux.RedisplayCount);
    }

    [Fact]
    public void Pair_new_device_logs_error_and_does_not_throw_when_window_open_fails()
    {
        PairingCoordinator coordinator = new(
            new InMemoryTrustedDeviceStore(),
            new FixedClock(TrustedAt),
            new ThrowingPinGenerator(),
            NullLogger<PairingCoordinator>.Instance);
        FakePairingUx ux = new();
        ListLogger<TrayPairingPresenter> logger = new();
        _ = new TrayPairingPresenter(coordinator, ux, logger);

        ux.RaisePairNewDevice();

        Assert.Null(ux.LastPin);
        Assert.Contains(
            logger.Entries,
            e => e.Level == LogLevel.Error
                && e.Message.Contains("Pair new device", StringComparison.Ordinal));
    }

    private static MessageEnvelope PairingEnvelope(string deviceId, string pin)
    {
        PairingRequestMessage request = new(deviceId, pin);
        string json = EnvelopeCodec.Serialize(MessageTypes.PairingRequest, request, TrustedAt, "pair-1");
        return EnvelopeCodec.Deserialize(json);
    }
}

internal sealed class SequencePinGenerator : IPairingPinGenerator
{
    private readonly Queue<string> _pins;

    public SequencePinGenerator(params string[] pins)
    {
        _pins = new Queue<string>(pins);
    }

    public string Generate() => _pins.Dequeue();
}

internal sealed class ThrowingPinGenerator : IPairingPinGenerator
{
    public string Generate() => throw new InvalidOperationException("pin generation failed");
}
