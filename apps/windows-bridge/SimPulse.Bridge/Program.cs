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
builder.Services.AddSingleton<ISimulatorAdapter>(_ =>
{
    string? fixturePath = Environment.GetEnvironmentVariable("SIMPULSE_FIXTURE_PATH");
    if (!string.IsNullOrWhiteSpace(fixturePath))
    {
        return new FixtureSimulatorAdapter(fixturePath);
    }

    return new IRacingAdapter();
});
builder.Services.AddSingleton<BridgeRuntime>();
builder.Services.AddHostedService<Worker>();

IHost host = builder.Build();
host.Run();
