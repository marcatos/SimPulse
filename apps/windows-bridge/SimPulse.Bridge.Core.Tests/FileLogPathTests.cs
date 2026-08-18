using SimPulse.Bridge.Core.Application;

namespace SimPulse.Bridge.Core.Tests;

public sealed class FileLogPathTests
{
    [Fact]
    public void Uses_local_app_data_simpulse_logs_by_default()
    {
        string path = FileLogPath.ResolveDirectory(
            configuredDirectory: null,
            localAppData: @"C:\Users\me\AppData\Local",
            userProfile: @"C:\Users\me");

        Assert.Equal(Path.Combine(@"C:\Users\me\AppData\Local", "SimPulse", "logs"), path);
    }

    [Fact]
    public void Falls_back_to_user_profile_when_local_app_data_missing()
    {
        string path = FileLogPath.ResolveDirectory(
            configuredDirectory: null,
            localAppData: null,
            userProfile: "/home/me");

        Assert.Equal(Path.Combine("/home/me", "SimPulse", "logs"), path);
    }

    [Fact]
    public void Env_directory_overrides_defaults()
    {
        string path = FileLogPath.ResolveDirectory(
            configuredDirectory: "/tmp/custom-logs",
            localAppData: @"C:\Users\me\AppData\Local",
            userProfile: @"C:\Users\me");

        Assert.Equal("/tmp/custom-logs", path);
    }

    [Fact]
    public void File_logging_is_enabled_unless_env_is_zero()
    {
        Assert.True(FileLogPath.IsEnabled(fileEnv: null));
        Assert.True(FileLogPath.IsEnabled(fileEnv: "1"));
        Assert.False(FileLogPath.IsEnabled(fileEnv: "0"));
    }
}
