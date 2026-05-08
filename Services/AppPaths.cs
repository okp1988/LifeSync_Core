using System;
using System.IO;

namespace LifeSyncTaskClient.Services;

public static class AppPaths
{
    public static string BuildDirectory { get; } = AppContext.BaseDirectory;
    public static string DataDirectory { get; } = Path.Combine(BuildDirectory, "data");
    public static string LogDirectory { get; } = Path.Combine(BuildDirectory, "log");

    public static string TaskCachePath { get; } = Path.Combine(DataDirectory, "tasks.json");
    public static string TrackItemsPath { get; } = Path.Combine(DataDirectory, "track-items.json");
    public static string TrackOptionsPath { get; } = Path.Combine(DataDirectory, "track-options.json");
    public static string ConfigPath { get; } = Path.Combine(DataDirectory, "config.json");
    public static string CurrentInfoLogPath => Path.Combine(LogDirectory, $"lifesync-info-{DateTime.Today:yyyy-MM-dd}.log");
    public static string CurrentWarningErrorLogPath => Path.Combine(LogDirectory, $"lifesync-warning-error-{DateTime.Today:yyyy-MM-dd}.log");
}
