namespace LifeSyncTaskClient.Models;

public sealed class WatchListEntry
{
    public string TaskId { get; set; } = string.Empty;
    public DateTimeOffset AddedAt { get; set; }
}
