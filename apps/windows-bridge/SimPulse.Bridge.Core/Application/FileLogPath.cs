namespace SimPulse.Bridge.Core.Application;

public static class FileLogPath
{
    public const string DirectoryEnv = "SIMPULSE_LOG_DIR";
    public const string EnabledEnv = "SIMPULSE_LOG_FILE";

    public static bool IsEnabled(string? fileEnv)
    {
        return !string.Equals(fileEnv, "0", StringComparison.OrdinalIgnoreCase);
    }

    public static string ResolveDirectory(string? configuredDirectory, string? localAppData, string? userProfile)
    {
        if (!string.IsNullOrWhiteSpace(configuredDirectory))
        {
            return configuredDirectory;
        }

        if (!string.IsNullOrWhiteSpace(localAppData))
        {
            return Path.Combine(localAppData, "SimPulse", "logs");
        }

        if (!string.IsNullOrWhiteSpace(userProfile))
        {
            return Path.Combine(userProfile, "SimPulse", "logs");
        }

        return Path.Combine(Path.GetTempPath(), "SimPulse", "logs");
    }
}
