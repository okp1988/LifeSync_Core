using System.Net.Http;
using System.Linq;
using System.Text.Json;
using LifeSyncTaskClient.Models;

namespace LifeSyncTaskClient.Services;

public sealed class GoogleSheetClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient = new();

    public async Task<IReadOnlyList<SheetTask>> GetTasksAsync(AppConfig config, CancellationToken cancellationToken)
    {
        var url = BuildUrl(config, "tasks");
        AppLogger.Info($"GET {url}");
        using var response = await _httpClient.GetAsync(url, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        AppLogger.Info($"GET status={(int)response.StatusCode}");
        EnsureSuccess(response, body);

        return ParseTasks(body);
    }

    public async Task CompleteAsync(AppConfig config, SheetTask task, DateTime executeDate, string remark, CancellationToken cancellationToken)
    {
        var parameters = new Dictionary<string, string>
        {
            ["action"] = "complete",
            ["token"] = config.ApiKey,
            ["rowid"] = task.RowNumber.ToString(),
            ["executedate"] = executeDate.ToString("yyyy-MM-dd"),
            ["remark"] = remark
        };

        using var content = new FormUrlEncodedContent(parameters);
        var requestBody = await content.ReadAsStringAsync(cancellationToken);
        AppLogger.Info($"POST {config.GoogleAppsScriptUrl}");
        AppLogger.Info($"POST payload={RedactToken(requestBody)}");

        using var response = await _httpClient.PostAsync(config.GoogleAppsScriptUrl, content, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        AppLogger.Info($"POST status={(int)response.StatusCode}");
        AppLogger.Info($"POST response={Truncate(responseBody, 12000)}");
        EnsureSuccess(response, responseBody, "marking complete");
    }

    private static Uri BuildUrl(AppConfig config, string action)
    {
        if (string.IsNullOrWhiteSpace(config.GoogleAppsScriptUrl))
        {
            throw new InvalidOperationException("Google Apps Script URL is required.");
        }

        var separator = config.GoogleAppsScriptUrl.Contains('?') ? '&' : '?';
        var url = $"{config.GoogleAppsScriptUrl}{separator}action={Uri.EscapeDataString(action)}&token={Uri.EscapeDataString(config.ApiKey)}";
        return new Uri(url);
    }

    private static void EnsureSuccess(
        HttpResponseMessage response,
        string body,
        string operation = "calling Google Sheet API")
    {
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Google Sheet API failed while {operation}. HTTP {(int)response.StatusCode}: {body}");
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            return;
        }

        ThrowIfErrorResponse(body, operation);
    }

    private static string RedactToken(string body)
    {
        return string.Join("&", body.Split('&').Select(part =>
            part.StartsWith("token=", StringComparison.OrdinalIgnoreCase)
                ? "token=<redacted>"
                : part));
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength
            ? value
            : value[..maxLength] + "... <truncated>";
    }

    private static void ThrowIfErrorResponse(string body, string operation)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            if (root.TryGetProperty("success", out var successElement)
                && successElement.ValueKind == JsonValueKind.False)
            {
                throw new InvalidOperationException(
                    $"Google Sheet API failed while {operation}: {GetResponseMessage(root)}");
            }

            if (root.TryGetProperty("ok", out var okElement)
                && okElement.ValueKind == JsonValueKind.False)
            {
                throw new InvalidOperationException(
                    $"Google Sheet API failed while {operation}: {GetResponseMessage(root)}");
            }

            if (root.TryGetProperty("error", out var errorElement)
                && errorElement.ValueKind != JsonValueKind.Null
                && !string.IsNullOrWhiteSpace(errorElement.ToString()))
            {
                throw new InvalidOperationException(
                    $"Google Sheet API failed while {operation}: {errorElement}");
            }
        }
        catch (JsonException)
        {
            if (body.Contains("error", StringComparison.OrdinalIgnoreCase)
                || body.Contains("exception", StringComparison.OrdinalIgnoreCase)
                || body.Contains("failed", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Google Sheet API returned an error while {operation}: {ExtractHtmlError(body)}");
            }
        }
    }

    private static string ExtractHtmlError(string body)
    {
        const string marker = "max-width:600px\">";
        var markerIndex = body.IndexOf(marker, StringComparison.OrdinalIgnoreCase);

        if (markerIndex >= 0)
        {
            var start = markerIndex + marker.Length;
            var end = body.IndexOf("</div>", start, StringComparison.OrdinalIgnoreCase);

            if (end > start)
            {
                return body[start..end];
            }
        }

        return Truncate(body, 1200);
    }

    private static string GetResponseMessage(JsonElement root)
    {
        foreach (var propertyName in new[] { "message", "error", "details" })
        {
            if (root.TryGetProperty(propertyName, out var property)
                && property.ValueKind != JsonValueKind.Null
                && !string.IsNullOrWhiteSpace(property.ToString()))
            {
                return property.ToString();
            }
        }

        return root.GetRawText();
    }

    private static IReadOnlyList<SheetTask> ParseTasks(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;

            if (root.ValueKind == JsonValueKind.Array)
            {
                return JsonSerializer.Deserialize<List<SheetTask>>(body, SerializerOptions) ?? [];
            }

            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("tasks", out var tasksElement))
            {
                return JsonSerializer.Deserialize<List<SheetTask>>(tasksElement.GetRawText(), SerializerOptions) ?? [];
            }

            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var dataElement))
            {
                return ParseDataRows(dataElement);
            }

            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("error", out var errorElement))
            {
                throw new InvalidOperationException(errorElement.GetString() ?? "Google Sheet returned an error.");
            }

            throw new InvalidOperationException("Google Sheet response must be either a task array or an object with a tasks array.");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Google Sheet returned invalid JSON: {ex.Message}", ex);
        }
    }

    private static IReadOnlyList<SheetTask> ParseDataRows(JsonElement dataElement)
    {
        if (dataElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Google Sheet data field must be an array.");
        }

        var tasks = new List<SheetTask>();

        var sheetRowNumber = 2;
        foreach (var row in dataElement.EnumerateArray())
        {
            if (row.ValueKind != JsonValueKind.Array || row.GetArrayLength() < 6)
            {
                sheetRowNumber++;
                continue;
            }

            var category = GetString(row, 0);
            var type = GetString(row, 1);
            var taskName = GetString(row, 2);

            if (string.IsNullOrWhiteSpace(category)
                && string.IsNullOrWhiteSpace(type)
                && string.IsNullOrWhiteSpace(taskName))
            {
                sheetRowNumber++;
                continue;
            }

            tasks.Add(new SheetTask
            {
                Category = category,
                Type = type,
                Task = taskName,
                ExpiredDate = GetDate(row, 3),
                WarningDate = GetDate(row, 4),
                PreviousDate1 = GetDate(row, 6),
                PreviousDate2 = GetDate(row, 7),
                Remark = GetString(row, 8)
            });
            tasks[^1].RowNumber = sheetRowNumber;
            tasks[^1].TrackId = sheetRowNumber.ToString();
            sheetRowNumber++;
        }

        return tasks;
    }

    private static string GetString(JsonElement row, int index)
    {
        if (index >= row.GetArrayLength())
        {
            return string.Empty;
        }

        var value = row[index];
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => string.Empty
        };
    }

    private static DateTime? GetDate(JsonElement row, int index)
    {
        var value = GetString(row, index);
        return DateTime.TryParse(value, out var date) ? date : null;
    }
}
