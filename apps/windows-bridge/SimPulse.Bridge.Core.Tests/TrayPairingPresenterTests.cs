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
        _ = new TrayPairingPresenter(coordinator, ux);
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
        TrayPairingPresenter presenter = new(coordinator, ux);
        DateTimeOffset expiresAt = TrustedAt.Add(PairingCoordinator.WindowDuration);

        presenter.OnWindowOpened(FirstPin, expiresAt);

        Assert.Equal(FirstPin, ux.LastPin);
        Assert.Equal(expiresAt, ux.LastExpiresAtUtc);
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
