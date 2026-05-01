using System.IO;

namespace LifeSyncTaskClient.Services;

public static class AppLogger
{
    private const string TimestampFormat = "yyyy-MM-dd HH:mm:ss";
    private const string InfoLogFilePrefix = "lifesync-info-";
    private const string WarningErrorLogFilePrefix = "lifesync-warning-error-";
    private const string LogFileExtension = ".log";

    public static void Info(string message)
    {
        Write("INFO", message, AppPaths.CurrentInfoLogPath);
    }

    public static void Warning(string message)
    {
        Write("WARNING", message, AppPaths.CurrentWarningErrorLogPath);
    }

    public static void Error(string message, Exception exception)
    {
        Write("ERROR", $"{message}: {exception}", AppPaths.CurrentWarningErrorLogPath);
    }

    public static void PruneOldFiles(int retentionDays)
    {
        try
        {
            if (!Directory.Exists(AppPaths.LogDirectory))
            {
                return;
            }

            retentionDays = Math.Max(1, retentionDays);
            var cutoffDate = DateTime.Today.AddDays(-retentionDays);
            var deletedCount = 0;

            foreach (var path in Directory.EnumerateFiles(AppPaths.LogDirectory, $"lifesync-*{LogFileExtension}"))
            {
                if (TryGetLogDate(path, out var logDate) && logDate < cutoffDate)
                {
                    File.Delete(path);
                    deletedCount++;
                }
            }

            Info($"Pruned {deletedCount} log file(s) older than {cutoffDate:yyyy-MM-dd}; retentionDays={retentionDays}");
        }
        catch
        {
            // Logging cleanup must never block app startup.
        }
    }

    private static void Write(string level, string message, string logPath)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.LogDirectory);
            var line = $"{DateTime.Now.ToString(TimestampFormat)} [{level}] {message}{Environment.NewLine}";
            File.AppendAllText(logPath, line);
        }
        catch
        {
            // Logging must never break the app workflow.
        }
    }

    private static bool TryGetLogDate(string path, out DateTime logDate)
    {
        var fileName = Path.GetFileNameWithoutExtension(path);
        logDate = default;

        var dateText = fileName.StartsWith(InfoLogFilePrefix, StringComparison.OrdinalIgnoreCase)
            ? fileName[InfoLogFilePrefix.Length..]
            : fileName.StartsWith(WarningErrorLogFilePrefix, StringComparison.OrdinalIgnoreCase)
                ? fileName[WarningErrorLogFilePrefix.Length..]
                : string.Empty;

        if (string.IsNullOrWhiteSpace(dateText))
        {
            return false;
        }

        return DateTime.TryParseExact(
                dateText,
                "yyyy-MM-dd",
                null,
                System.Globalization.DateTimeStyles.None,
                out logDate);
    }
}
