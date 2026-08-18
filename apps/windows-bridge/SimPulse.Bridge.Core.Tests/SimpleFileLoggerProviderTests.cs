using Microsoft.Extensions.Logging;

using SimPulse.Bridge.Core.Adapters;

namespace SimPulse.Bridge.Core.Tests;

public sealed class SimpleFileLoggerProviderTests
{
    private static readonly DateTimeOffset LoggedAt = DateTimeOffset.Parse("2026-08-18T10:00:00Z");

    [Fact]
    public void Writes_information_line_to_daily_rolling_file()
    {
        string directory = CreateTempLogDirectory();
        try
        {
            using SimpleFileLoggerProvider provider = new(directory, new FixedClock(LoggedAt));
            ILogger logger = provider.CreateLogger("Bridge.Test");

            logger.LogInformation("Bridge host starting. Component={Component}", "Worker");

            string path = Path.Combine(directory, "bridge-20260818.log");
            Assert.True(File.Exists(path));
            string content = File.ReadAllText(path);
            Assert.Contains("Information", content, StringComparison.Ordinal);
            Assert.Contains("Bridge host starting", content, StringComparison.Ordinal);
            Assert.DoesNotContain("Pin=", content, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Rolls_to_a_new_file_when_the_utc_date_changes()
    {
        string directory = CreateTempLogDirectory();
        MutableClock clock = new(LoggedAt);
        try
        {
            using SimpleFileLoggerProvider provider = new(directory, clock);
            ILogger logger = provider.CreateLogger("Bridge.Test");
            logger.LogInformation("day-one");
            clock.UtcNow = LoggedAt.AddDays(1);
            logger.LogInformation("day-two");

            Assert.True(File.Exists(Path.Combine(directory, "bridge-20260818.log")));
            Assert.True(File.Exists(Path.Combine(directory, "bridge-20260819.log")));
            Assert.Contains("day-one", File.ReadAllText(Path.Combine(directory, "bridge-20260818.log")), StringComparison.Ordinal);
            Assert.Contains("day-two", File.ReadAllText(Path.Combine(directory, "bridge-20260819.log")), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Write_failure_disables_file_logging_without_throwing()
    {
        string directory = CreateTempLogDirectory();
        string blockedLogPath = Path.Combine(directory, "bridge-20260818.log");
        Directory.CreateDirectory(blockedLogPath);
        try
        {
            using SimpleFileLoggerProvider provider = new(directory, new FixedClock(LoggedAt));
            ILogger logger = provider.CreateLogger("Bridge.Test");

            logger.LogInformation("first-write-fails");
            Directory.Delete(blockedLogPath);
            logger.LogInformation("second-write-must-stay-disabled");

            Assert.False(File.Exists(blockedLogPath));
            Assert.False(Directory.Exists(blockedLogPath));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static string CreateTempLogDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "SimPulseTests", "logs", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
