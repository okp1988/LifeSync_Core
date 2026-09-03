namespace LifeSyncTaskClient.Models;

public sealed class CheckinSummaryRow
{
    public required string TaskDisplay { get; init; }

    public int? DayPassed { get; init; }

    public string DayPassedDisplay => DayPassed?.ToString() ?? "-";

    public DateTime? NextDate { get; init; }

    public string NextDateDisplay => NextDate?.ToString("dd MMM yyyy") ?? "-";

    public DateTime? LastExecutedDate { get; init; }

    public string LastExecutedDateDisplay => LastExecutedDate?.ToString("dd MMM yyyy") ?? "-";

    public bool IsDueHighlight { get; init; }
}
