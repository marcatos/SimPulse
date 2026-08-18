using Microsoft.Extensions.Logging;

using SimPulse.Bridge;
using SimPulse.Bridge.Core.Adapters;
using SimPulse.Bridge.Core.Application;
using SimPulse.Bridge.Core.Ports;

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
builder.Services.AddSingleton<ITrustedDeviceStore, InMemoryTrustedDeviceStore>();
builder.Services.AddSingleton<IClientSessionHub, ClientSessionHub>();
builder.Services.AddSingleton<ISimulatorAdapter>(_ =>
{
    string? fixturePath = Environment.GetEnvironmentVariable("SIMPULSE_FIXTURE_PATH");
    if (!string.IsNullOrWhiteSpace(fixturePath))
    {
        return new FixtureSimulatorAdapter(fixturePath);
    }

    return new IRacingAdapter();
});
builder.Services.AddSingleton<IBridgeTransport>(sp =>
{
    (string host, int port) = ReadBindOptions();
    return new HttpListenerWebSocketTransport(
        host,
        port,
        sp.GetRequiredService<IClientSessionHub>(),
        sp.GetRequiredService<IClock>(),
        sp.GetRequiredService<ILogger<HttpListenerWebSocketTransport>>());
});
builder.Services.AddSingleton<BridgeRuntime>();
builder.Services.AddHostedService<Worker>();

IHost host = builder.Build();
host.Run();

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
