using System.Net.Http;
using System.Text;
using System.Text.Json;
using LifeSyncTaskClient.Models;

namespace LifeSyncTaskClient.Services;

public sealed class GoogleSheetClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient = new();

    public async Task<TaskFetchResponse> GetTaskSnapshotAsync(AppConfig config, CancellationToken cancellationToken)
    {
        var url = BuildUrl(config, "tasks");
        AppLogger.Info($"GET {RedactToken(url.ToString())}");
        using var response = await _httpClient.GetAsync(url, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        AppLogger.Info($"GET status={(int)response.StatusCode}");
        EnsureHttpSuccess(response, body, "retrieving tasks");

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        if (root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty("success", out var success)
            && success.ValueKind == JsonValueKind.False)
        {
            throw new InvalidOperationException(GetError(root, "Google Sheet rejected task retrieval."));
        }

        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("tasks", out var tasks))
        {
            var historyRecords = root.TryGetProperty("historyRecords", out var history)
                ? JsonSerializer.Deserialize<List<CompletionHistoryRecord>>(history.GetRawText(), SerializerOptions) ?? []
                : [];
            var filters = root.TryGetProperty("filters", out var filterElement)
                ? JsonSerializer.Deserialize<List<TaskFilterDefinition>>(filterElement.GetRawText(), SerializerOptions) ?? []
                : [];
            return new TaskFetchResponse
            {
                Success = true,
                Tasks = JsonSerializer.Deserialize<List<SheetTask>>(tasks.GetRawText(), SerializerOptions) ?? [],
                HistoryRecords = historyRecords,
                Filters = filters
            };
        }

        if (root.ValueKind == JsonValueKind.Array)
        {
            return new TaskFetchResponse
            {
                Success = true,
                Tasks = JsonSerializer.Deserialize<List<SheetTask>>(root.GetRawText(), SerializerOptions) ?? []
            };
        }

        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var data))
        {
            return new TaskFetchResponse { Success = true, Tasks = ParseLegacyRows(data).ToList() };
        }

        throw new InvalidOperationException("Google Sheet response does not contain a tasks array.");
    }

    public async Task SaveTaskFiltersAsync(AppConfig config, IEnumerable<TaskFilterDefinition> filters, CancellationToken cancellationToken)
    {
        var request = new { action = "saveFilters", token = config.ApiKey, operationId = Guid.NewGuid().ToString("D"), filters };
        var json = JsonSerializer.Serialize(request, SerializerOptions);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync(config.GoogleAppsScriptUrl, content, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        EnsureHttpSuccess(response, body, "saving filters");
        using var document = JsonDocument.Parse(body);
        if (!document.RootElement.TryGetProperty("success", out var success) || !success.GetBoolean())
        {
            throw new InvalidOperationException(GetError(document.RootElement, "Google Sheet rejected filter changes."));
        }
    }

    public async Task<TaskApiResponse> SendMutationAsync(
        AppConfig config,
        TaskMutation mutation,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(config.GoogleAppsScriptUrl))
        {
            throw new InvalidOperationException("Google Apps Script URL is required.");
        }

        var request = new
        {
            action = mutation.OperationType,
            token = config.ApiKey,
            operationId = mutation.OperationId,
            taskId = mutation.TaskId,
            expectedRevision = mutation.ExpectedRevision,
            payload = mutation.Payload
        };
        var json = JsonSerializer.Serialize(request, SerializerOptions);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        AppLogger.Info($"POST {config.GoogleAppsScriptUrl} action={mutation.OperationType} taskId={mutation.TaskId} operationId={mutation.OperationId}");

        using var response = await _httpClient.PostAsync(config.GoogleAppsScriptUrl, content, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        AppLogger.Info($"POST status={(int)response.StatusCode} response={Truncate(body, 12000)}");
        EnsureHttpSuccess(response, body, mutation.OperationType);

        try
        {
            return JsonSerializer.Deserialize<TaskApiResponse>(body, SerializerOptions)
                ?? new TaskApiResponse { Error = "Empty mutation response." };
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Google Sheet returned invalid mutation JSON: {ex.Message}", ex);
        }
    }

    private static Uri BuildUrl(AppConfig config, string action)
    {
        if (string.IsNullOrWhiteSpace(config.GoogleAppsScriptUrl))
        {
            throw new InvalidOperationException("Google Apps Script URL is required.");
        }

        var separator = config.GoogleAppsScriptUrl.Contains('?') ? '&' : '?';
        return new Uri($"{config.GoogleAppsScriptUrl}{separator}action={Uri.EscapeDataString(action)}&token={Uri.EscapeDataString(config.ApiKey)}");
    }

    private static void EnsureHttpSuccess(HttpResponseMessage response, string body, string operation)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Google Sheet API failed while {operation}. HTTP {(int)response.StatusCode}: {Truncate(body, 1200)}");
        }

        if (body.Contains("<html", StringComparison.OrdinalIgnoreCase)
            && (body.Contains("error", StringComparison.OrdinalIgnoreCase)
                || body.Contains("exception", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"Google Sheet API failed while {operation}: {Truncate(body, 1200)}");
        }
    }

    private static string GetError(JsonElement root, string fallback)
    {
        foreach (var name in new[] { "error", "message", "details" })
        {
            if (root.TryGetProperty(name, out var value) && !string.IsNullOrWhiteSpace(value.ToString()))
            {
                return value.ToString();
            }
        }

        return fallback;
    }

    private static IReadOnlyList<SheetTask> ParseLegacyRows(JsonElement data)
    {
        var result = new List<SheetTask>();
        var rowNumber = 2;
        foreach (var row in data.EnumerateArray())
        {
            if (row.ValueKind != JsonValueKind.Array || row.GetArrayLength() < 5)
            {
                rowNumber++;
                continue;
            }

            var task = new SheetTask
            {
                Category = GetString(row, 0),
                Type = GetString(row, 1),
                Task = GetString(row, 2),
                ExpiredDate = GetDate(row, 3),
                WarningDate = GetDate(row, 4),
                PreviousDate1 = GetDate(row, 6),
                PreviousDate2 = GetDate(row, 7),
                Remark = GetString(row, 8),
                RowNumber = rowNumber
            };
            if (!string.IsNullOrWhiteSpace(task.Category)
                || !string.IsNullOrWhiteSpace(task.Type)
                || !string.IsNullOrWhiteSpace(task.Task))
            {
                result.Add(task);
            }
            rowNumber++;
        }

        return result;
    }

    private static string GetString(JsonElement row, int index)
    {
        if (index >= row.GetArrayLength())
        {
            return string.Empty;
        }

        var value = row[index];
        return value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.ToString();
    }

    private static DateTime? GetDate(JsonElement row, int index)
    {
        return DateTime.TryParse(GetString(row, index), out var date) ? date : null;
    }

    private static string RedactToken(string value)
    {
        var tokenIndex = value.IndexOf("token=", StringComparison.OrdinalIgnoreCase);
        if (tokenIndex < 0)
        {
            return value;
        }

        var end = value.IndexOf('&', tokenIndex);
        return end < 0
            ? value[..tokenIndex] + "token=<redacted>"
            : value[..tokenIndex] + "token=<redacted>" + value[end..];
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength] + "... <truncated>";
    }
}
