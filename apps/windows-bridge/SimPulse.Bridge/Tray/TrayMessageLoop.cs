#if WINDOWS_TRAY
using System.Diagnostics;
using System.Windows.Forms;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using SimPulse.Bridge.Core.Application;
using SimPulse.Bridge.Core.Ports;

namespace SimPulse.Bridge.Tray;

internal sealed class PairingUxHolder
{
    public IPairingUx? Instance { get; set; }
}

internal readonly record struct TrayStartAttempt(
    NotifyIconPairingUx? Ux,
    TrayStartupOutcome Outcome,
    Exception? Exception);

internal static class TrayMessageLoop
{
    private const string Component = "TrayMessageLoop";

    public static TrayStartAttempt TryStart(IHost host, TimeSpan timeout)
    {
        TrayStartGate gate = new();
        Thread thread = new(() => Run(host, gate))
        {
            Name = "SimPulse.Bridge.Tray",
            IsBackground = true,
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        TrayStartupOutcome outcome = TrayStartupPolicy.WaitForReady(gate.Ready.Task, timeout);
        if (outcome == TrayStartupOutcome.TimedOut && gate.Ready.Task.IsCompletedSuccessfully)
        {
            outcome = TrayStartupOutcome.Ready;
        }

        if (outcome != TrayStartupOutcome.Ready)
        {
            Interlocked.Exchange(ref gate.Claimed, 1);
            return new TrayStartAttempt(null, outcome, gate.Ready.Task.Exception?.GetBaseException());
        }

        return new TrayStartAttempt(gate.Ready.Task.GetAwaiter().GetResult(), outcome, null);
    }

    private static void Run(IHost host, TrayStartGate gate)
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
            if (Interlocked.CompareExchange(ref gate.Claimed, 1, 0) != 0)
            {
                ux.Dispose();
                logger.LogWarning(
                    "Tray UX created after timeout and was discarded. ElapsedMs={ElapsedMs} Component={Component}",
                    started.ElapsedMilliseconds,
                    Component);
                return;
            }

            gate.Ready.SetResult(ux);
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
            Interlocked.Exchange(ref gate.Claimed, 1);
            gate.Ready.TrySetException(ex);
        }
    }

    private static void ExitMessageLoop()
    {
        if (Application.MessageLoop)
        {
            Application.Exit();
        }
    }

    private sealed class TrayStartGate
    {
        public int Claimed;
        public readonly TaskCompletionSource<NotifyIconPairingUx> Ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
#endif
