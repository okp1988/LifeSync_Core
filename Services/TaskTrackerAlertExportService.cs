using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LifeSyncTaskClient.Models;

namespace LifeSyncTaskClient.Services;

public sealed class TaskTrackerAlertExportService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public async Task<bool> AppendRequestAsync(string path, TaskTrackerAlertRequest request)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? AppPaths.DataDirectory);
        var document = await LoadDocumentAsync(path);
        document.Requests ??= [];
        if (document.Requests.Any(existing =>
            string.Equals(existing.SourceKey, request.SourceKey, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        document.CreatedAt = DateTime.Now;
        document.Requests.Add(request);
        await SaveDocumentAsync(path, document);
        return true;
    }

    public static string CreateRequestId()
    {
        return $"lifesync-request-{DateTime.Now:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..42];
    }

    public static string CreateHashedSourceKey(string prefix, string fingerprint)
    {
        var normalized = NormalizeFingerprint(fingerprint);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        var hash = Convert.ToHexString(bytes)[..12];
        return $"{prefix}-{hash}";
    }

    private static async Task<TaskTrackerAlertExportDocument> LoadDocumentAsync(string path)
    {
        if (!File.Exists(path))
        {
            return new TaskTrackerAlertExportDocument();
        }

        await using var stream = File.OpenRead(path);
        var document = await JsonSerializer.DeserializeAsync<TaskTrackerAlertExportDocument>(stream, SerializerOptions)
            ?? new TaskTrackerAlertExportDocument();
        document.Requests ??= [];
        return document;
    }

    private static async Task SaveDocumentAsync(string path, TaskTrackerAlertExportDocument document)
    {
        var temporaryPath = $"{path}.tmp";
        await using (var stream = File.Create(temporaryPath))
        {
            await JsonSerializer.SerializeAsync(stream, document, SerializerOptions);
        }

        File.Move(temporaryPath, path, true);
    }

    private static string NormalizeFingerprint(string value)
    {
        return string.Join(" ", value
                .Trim()
                .ToLowerInvariant()
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}
