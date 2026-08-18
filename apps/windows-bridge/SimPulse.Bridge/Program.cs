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
StartTrayMessageLoopIfNeeded(host);
host.Run();

static void RegisterPairingUx(IServiceCollection services)
{
    if (!ShouldUseTray())
    {
        services.AddSingleton<IPairingUx, ConsolePairingUx>();
        return;
    }

#if WINDOWS_TRAY
    services.AddSingleton<TrayPairingUxHolder>();
    services.AddSingleton<IPairingUx>(sp =>
        sp.GetRequiredService<TrayPairingUxHolder>().Instance
        ?? throw new InvalidOperationException("Tray pairing UX was not started."));
#endif
}

static void StartTrayMessageLoopIfNeeded(IHost host)
{
    ILogger logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Program");
    if (!ShouldUseTray())
    {
        logger.LogInformation("Pairing UX is console. Component={Component}", "Program");
        return;
    }

#if WINDOWS_TRAY
    TrayPairingUxHolder holder = host.Services.GetRequiredService<TrayPairingUxHolder>();
    holder.Instance = TrayMessageLoop.Start(host);
    logger.LogInformation("Pairing UX is Windows tray. Component={Component}", "Program");
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
