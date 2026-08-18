#if WINDOWS_TRAY
using System.Diagnostics;
#endif
using Microsoft.Extensions.Logging;

using SimPulse.Bridge;
using SimPulse.Bridge.Core.Adapters;
using SimPulse.Bridge.Core.Adapters.Iracing;
using SimPulse.Bridge.Core.Application;
using SimPulse.Bridge.Core.Ports;
#if WINDOWS_TRAY
using SimPulse.Bridge.Tray;
#endif

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

string logLevelValue = Environment.GetEnvironmentVariable("SIMPULSE_LOG_LEVEL") ?? "Information";
if (!Enum.TryParse(logLevelValue, ignoreCase: true, out LogLevel logLevel))
{
    logLevel = LogLevel.Information;
}

builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options =>
{
    options.TimestampFormat = "HH:mm:ss.fff ";
    options.SingleLine = true;
});
builder.Logging.SetMinimumLevel(logLevel);

string? fileLogDirectory = null;
if (FileLogPath.IsEnabled(Environment.GetEnvironmentVariable(FileLogPath.EnabledEnv)))
{
    fileLogDirectory = FileLogPath.ResolveDirectory(
        Environment.GetEnvironmentVariable(FileLogPath.DirectoryEnv),
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
    builder.Logging.AddProvider(new SimpleFileLoggerProvider(fileLogDirectory, new SystemClock(), logLevel));
}

builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSingleton<ITrustedDeviceStore>(CreateTrustedDeviceStore);
builder.Services.AddSingleton<IPairingPinGenerator, PairingPinGenerator>();
builder.Services.AddSingleton<PairingCoordinator>();
RegisterPairingUx(builder.Services);
builder.Services.AddSingleton<TrayPairingPresenter>();
builder.Services.AddSingleton<IClientSessionHub, ClientSessionHub>();
builder.Services.AddSingleton<IIracingSharedMemory>(sp =>
    new WindowsIracingSharedMemory(sp.GetRequiredService<ILogger<WindowsIracingSharedMemory>>()));
builder.Services.AddSingleton<ISimulatorAdapter>(sp =>
{
    string? fixturePath = Environment.GetEnvironmentVariable("SIMPULSE_FIXTURE_PATH");
    if (!string.IsNullOrWhiteSpace(fixturePath))
    {
        return new FixtureSimulatorAdapter(fixturePath);
    }

    return new IRacingAdapter(
        sp.GetRequiredService<IIracingSharedMemory>(),
        sp.GetRequiredService<IClock>(),
        sp.GetRequiredService<ILogger<IRacingAdapter>>());
});
builder.Services.AddSingleton<IBridgeTransport>(sp =>
{
    (string host, int port) = ReadBindOptions();
    PairingCoordinator pairing = sp.GetRequiredService<PairingCoordinator>();
    return new HttpListenerWebSocketTransport(
        host,
        port,
        sp.GetRequiredService<IClientSessionHub>(),
        sp.GetRequiredService<IClock>(),
        sp.GetRequiredService<ILogger<HttpListenerWebSocketTransport>>(),
        pairing.HandleAsync,
        pairing.Unregister);
});
builder.Services.AddSingleton<BridgeRuntime>();
builder.Services.AddHostedService<Worker>();

IHost host = builder.Build();
ILogger startupLogger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Program");
if (fileLogDirectory is not null)
{
    startupLogger.LogInformation(
        "File logging enabled. Directory={Directory} Component={Component}",
        fileLogDirectory,
        "Program");
}

StartPairingUx(host, startupLogger);
host.Run();

static void RegisterPairingUx(IServiceCollection services)
{
#if WINDOWS_TRAY
    services.AddSingleton<PairingUxHolder>();
    services.AddSingleton<IPairingUx>(sp =>
        sp.GetRequiredService<PairingUxHolder>().Instance
        ?? throw new InvalidOperationException("Pairing UX was not started."));
#else
    services.AddSingleton<IPairingUx, ConsolePairingUx>();
#endif
}

static void StartPairingUx(IHost host, ILogger logger)
{
    if (!ShouldUseTray())
    {
#if WINDOWS_TRAY
        host.Services.GetRequiredService<PairingUxHolder>().Instance =
            ActivatorUtilities.CreateInstance<ConsolePairingUx>(host.Services);
#endif
        logger.LogInformation("Pairing UX is console. Component={Component}", "Program");
        return;
    }

#if WINDOWS_TRAY
    PairingUxHolder holder = host.Services.GetRequiredService<PairingUxHolder>();
    Stopwatch started = Stopwatch.StartNew();
    TrayStartAttempt attempt = TrayMessageLoop.TryStart(host, TrayStartupPolicy.ReadyTimeout);
    if (TrayStartupPolicy.ShouldFallBackToConsole(attempt.Outcome))
    {
        logger.LogError(
            attempt.Exception,
            "Tray pairing UX failed ({Outcome}); falling back to console. ElapsedMs={ElapsedMs} Component={Component}",
            attempt.Outcome,
            started.ElapsedMilliseconds,
            "Program");
        holder.Instance = ActivatorUtilities.CreateInstance<ConsolePairingUx>(host.Services);
        return;
    }

    holder.Instance = attempt.Ux;
    logger.LogInformation(
        "Pairing UX is Windows tray. ElapsedMs={ElapsedMs} Component={Component}",
        started.ElapsedMilliseconds,
        "Program");
#endif
}

static bool ShouldUseTray()
{
#if WINDOWS_TRAY
    return PairingUxMode.UseTray(
        windowsTrayBuild: true,
        Environment.UserInteractive,
        Environment.GetEnvironmentVariable("SIMPULSE_BRIDGE_TRAY"));
#else
    return false;
#endif
}

static ITrustedDeviceStore CreateTrustedDeviceStore(IServiceProvider services)
{
    string? path = Environment.GetEnvironmentVariable("SIMPULSE_TRUSTED_DEVICES_PATH");
    if (!string.IsNullOrWhiteSpace(path))
    {
        return new JsonFileTrustedDeviceStore(
            path,
            services.GetRequiredService<ILogger<JsonFileTrustedDeviceStore>>());
    }

    return new InMemoryTrustedDeviceStore();
}

static (string Host, int Port) ReadBindOptions()
{
    string host = Environment.GetEnvironmentVariable("SIMPULSE_BRIDGE_HOST")
        ?? HttpListenerWebSocketTransport.DefaultHost;
    if (!int.TryParse(Environment.GetEnvironmentVariable("SIMPULSE_BRIDGE_PORT"), out int port))
    {
        port = HttpListenerWebSocketTransport.DefaultPort;
    }

    return (host, port);
}
