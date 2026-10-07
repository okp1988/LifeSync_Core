namespace LifeSyncTaskClient.Models;

public sealed class HistoryTaskSummary
{
    public string TaskKey { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string Task { get; init; } = string.Empty;
    public int CompletionCount { get; init; }
    public DateTime? LastCompletedDate { get; init; }

    public string TaskDisplay => $"{Task} ({Category} / {Type})";
    public string LastCompletedDateDisplay => LastCompletedDate?.ToString("dd MMM yyyy") ?? "-";

    public bool MatchesSearch(string searchText)
    {
        var search = searchText.Trim();
        return search.Length == 0
            || Task.Contains(search, StringComparison.OrdinalIgnoreCase)
            || Category.Contains(search, StringComparison.OrdinalIgnoreCase)
            || Type.Contains(search, StringComparison.OrdinalIgnoreCase);
    }

    public static string GetTaskKey(CompletionHistoryRecord record)
    {
        if (!string.IsNullOrWhiteSpace(record.TaskId))
        {
            return $"id:{record.TaskId}";
        }

        // Legacy records have no stable ID; keep equally named tasks in different categories separate.
        return $"legacy:{record.Category.Length}:{record.Category}{record.Type.Length}:{record.Type}{record.Task.Length}:{record.Task}";
    }

    public static IEnumerable<HistoryTaskSummary> Build(IEnumerable<CompletionHistoryRecord> records)
    {
        return records
            .GroupBy(GetTaskKey, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var latest = group.OrderByDescending(record => record.RecordedAt).First();
                var completions = group.Where(record => record.State != CompletionHistoryStates.Undone).ToList();
                return new HistoryTaskSummary
                {
                    TaskKey = group.Key,
                    Category = latest.Category,
                    Type = latest.Type,
                    Task = latest.Task,
                    CompletionCount = completions.Count,
                    LastCompletedDate = completions.Count == 0 ? null : completions.Max(record => record.CompletedDate)
                };
            })
            .OrderByDescending(summary => summary.LastCompletedDate)
            .ThenBy(summary => summary.Task, StringComparer.OrdinalIgnoreCase)
            .ThenBy(summary => summary.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(summary => summary.Type, StringComparer.OrdinalIgnoreCase);
    }
}
