namespace LifeSyncTaskClient.Models;

public sealed class AppConfig
{
    public string GoogleAppsScriptUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public int LogRetentionDays { get; set; } = 30;
}
