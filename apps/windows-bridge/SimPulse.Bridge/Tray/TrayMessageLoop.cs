#if WINDOWS_TRAY
using System.Diagnostics;
using System.Windows.Forms;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SimPulse.Bridge.Tray;

internal sealed class TrayPairingUxHolder
{
    public NotifyIconPairingUx? Instance { get; set; }
}

internal static class TrayMessageLoop
{
    private const string Component = "TrayMessageLoop";

    public static NotifyIconPairingUx Start(IHost host)
    {
        TaskCompletionSource<NotifyIconPairingUx> ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Thread thread = new(() => Run(host, ready))
        {
            Name = "SimPulse.Bridge.Tray",
            IsBackground = true,
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return ready.Task.GetAwaiter().GetResult();
    }

    private static void Run(IHost host, TaskCompletionSource<NotifyIconPairingUx> ready)
    {
        Stopwatch started = Stopwatch.StartNew();
        ILogger logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger(Component);
        try
        {
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            IHostApplicationLifetime lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
            lifetime.ApplicationStopping.Register(ExitMessageLoop);

            NotifyIconPairingUx ux = new(
                lifetime,
                host.Services.GetRequiredService<ILogger<NotifyIconPairingUx>>());
            ready.SetResult(ux);
            logger.LogInformation(
                "Tray STA message loop starting. ElapsedMs={ElapsedMs} Component={Component}",
                started.ElapsedMilliseconds,
                Component);
            Application.Run();
            logger.LogInformation(
                "Tray STA message loop stopped. ElapsedMs={ElapsedMs} Component={Component}",
                started.ElapsedMilliseconds,
                Component);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Tray STA message loop failed. ElapsedMs={ElapsedMs} Component={Component}",
                started.ElapsedMilliseconds,
                Component);
            ready.TrySetException(ex);
        }
    }

    private static void ExitMessageLoop()
    {
        if (Application.MessageLoop)
        {
            Application.Exit();
        }
    }
}
#endif
