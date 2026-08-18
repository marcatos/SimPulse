using SimPulse.Bridge.Core.Application;

namespace SimPulse.Bridge.Core.Tests;

public sealed class TrayPairingUxTextTests
{
    private static readonly DateTimeOffset ExpiresAt = DateTimeOffset.Parse("2026-08-18T10:05:00Z");

    [Fact]
    public void Menu_labels_match_required_tray_commands()
    {
        Assert.Equal("SimPulse Bridge", TrayPairingUxText.IconText);
        Assert.Equal("Pair new device", TrayPairingUxText.PairNewDevice);
        Assert.Equal("Exit", TrayPairingUxText.Exit);
    }

    [Fact]
    public void Pin_display_includes_pin_and_expiry()
    {
        string text = TrayPairingUxText.FormatPinDisplay("123456", ExpiresAt);

        Assert.Contains("123456", text, StringComparison.Ordinal);
        Assert.Contains("2026-08-18 10:05:00Z", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Pin_display_fits_notifyicon_tooltip_limit()
    {
        string text = TrayPairingUxText.FormatPinDisplay("123456", ExpiresAt);

        Assert.True(text.Length <= TrayPairingUxText.NotifyIconTextLimit);
    }
}
