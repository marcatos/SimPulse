#if WINDOWS_TRAY
using System.Diagnostics;
using System.Windows.Forms;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using SimPulse.Bridge.Core.Application;
using SimPulse.Bridge.Core.Ports;

namespace SimPulse.Bridge.Tray;

public sealed class NotifyIconPairingUx : IPairingUx, IDisposable
{
    private const string Component = "NotifyIconPairingUx";
    private const int PinBalloonMs = 15_000;
    private const int StatusBalloonMs = 5_000;

    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<NotifyIconPairingUx> _logger;
    private readonly SynchronizationContext? _syncContext;
    private readonly Icon _iconImage;
    private readonly ContextMenuStrip _menu;
    private readonly NotifyIcon _icon;
    private readonly CancellationTokenRegistration _stoppingRegistration;
    private string? _lastPin;
    private DateTimeOffset? _lastExpiresAtUtc;
    private int _disposed;

    public NotifyIconPairingUx(IHostApplicationLifetime lifetime, ILogger<NotifyIconPairingUx> logger)
    {
        Stopwatch started = Stopwatch.StartNew();
        _lifetime = lifetime;
        _logger = logger;
        _iconImage = (Icon)SystemIcons.Application.Clone();
        _menu = CreateMenu();
        _icon = CreateIcon();
        _syncContext = SynchronizationContext.Current;
        _stoppingRegistration = lifetime.ApplicationStopping.Register(Dispose);
        _logger.LogInformation(
            "NotifyIcon pairing UX ready. ElapsedMs={ElapsedMs} Component={Component}",
            started.ElapsedMilliseconds,
            Component);
    }

    public event Action? PairNewDeviceRequested;

    public event Action? ShowCurrentPinRequested;

    public void ShowPin(string pin, DateTimeOffset expiresAtUtc)
    {
        Stopwatch step = Stopwatch.StartNew();
        _lastPin = pin;
        _lastExpiresAtUtc = expiresAtUtc;
        string text = TrayPairingUxText.FormatPinDisplay(pin, expiresAtUtc);
        RunOnUi(() => ShowBalloon(text, PinBalloonMs, updateTooltip: true));
        _logger.LogInformation(
            "Pairing PIN is visible in tray/console. ExpiresAtUtc={ExpiresAtUtc} ElapsedMs={ElapsedMs} Component={Component}",
            expiresAtUtc,
            step.ElapsedMilliseconds,
            Component);
    }

    public void ShowStatus(string message)
    {
        Stopwatch step = Stopwatch.StartNew();
        RunOnUi(() => ShowBalloon(message, StatusBalloonMs, updateTooltip: false));
        _logger.LogInformation(
            "{Message} ElapsedMs={ElapsedMs} Component={Component}",
            message,
            step.ElapsedMilliseconds,
            Component);
    }

    public void RedisplayLastPin()
    {
        if (_lastPin is null || _lastExpiresAtUtc is null)
        {
            ShowStatus("No pairing PIN is available.");
            return;
        }

        Stopwatch step = Stopwatch.StartNew();
        string text = TrayPairingUxText.FormatPinDisplay(_lastPin, _lastExpiresAtUtc.Value);
        RunOnUi(() => ShowBalloon(text, PinBalloonMs, updateTooltip: true));
        _logger.LogInformation(
            "Current pairing PIN redisplayed. ElapsedMs={ElapsedMs} Component={Component}",
            step.ElapsedMilliseconds,
            Component);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        Stopwatch stopping = Stopwatch.StartNew();
        _stoppingRegistration.Dispose();
        RunOnUi(DisposeIcon);
        _logger.LogInformation(
            "NotifyIcon pairing UX disposed. ElapsedMs={ElapsedMs} Component={Component}",
            stopping.ElapsedMilliseconds,
            Component);
    }

    private ContextMenuStrip CreateMenu()
    {
        ContextMenuStrip menu = new();
        menu.Items.Add(TrayPairingUxText.ShowCurrentPin, image: null, OnShowCurrentPin);
        menu.Items.Add(TrayPairingUxText.PairNewDevice, image: null, OnPairNewDevice);
        menu.Items.Add(TrayPairingUxText.Exit, image: null, OnExit);
        return menu;
    }

    private NotifyIcon CreateIcon()
    {
        return new NotifyIcon
        {
            Text = TrayPairingUxText.IconText,
            Icon = _iconImage,
            ContextMenuStrip = _menu,
            Visible = true,
        };
    }

    private void ShowBalloon(string text, int timeoutMs, bool updateTooltip)
    {
        if (updateTooltip)
        {
            _icon.Text = text.Length <= TrayPairingUxText.NotifyIconTextLimit
                ? text
                : text[..TrayPairingUxText.NotifyIconTextLimit];
        }

        _icon.BalloonTipTitle = TrayPairingUxText.IconText;
        _icon.BalloonTipText = text;
        _icon.BalloonTipIcon = ToolTipIcon.Info;
        _icon.ShowBalloonTip(timeoutMs);
    }

    private void OnShowCurrentPin(object? sender, EventArgs e)
    {
        try
        {
            _logger.LogInformation("Show current PIN requested. Component={Component}", Component);
            ShowCurrentPinRequested?.Invoke();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Show current PIN menu handler failed. Component={Component}", Component);
        }
    }

    private void OnPairNewDevice(object? sender, EventArgs e)
    {
        try
        {
            _logger.LogInformation("Pair new device requested. Component={Component}", Component);
            PairNewDeviceRequested?.Invoke();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Pair new device menu handler failed. Component={Component}", Component);
        }
    }

    private void OnExit(object? sender, EventArgs e)
    {
        _logger.LogInformation("Exit requested. Component={Component}", Component);
        _lifetime.StopApplication();
    }

    private void DisposeIcon()
    {
        _icon.Visible = false;
        _icon.Dispose();
        _menu.Dispose();
        _iconImage.Dispose();
    }

    private void RunOnUi(Action action)
    {
        if (_syncContext is null || ReferenceEquals(_syncContext, SynchronizationContext.Current))
        {
            action();
            return;
        }

        _syncContext.Send(_ => action(), state: null);
    }
}
#endif
