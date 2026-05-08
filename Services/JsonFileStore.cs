using System.IO;
using System.Text.Json;
using LifeSyncTaskClient.Models;

namespace LifeSyncTaskClient.Services;

public sealed class JsonFileStore
{
    private static readonly TrackOptions DefaultTrackOptions = new()
    {
        Categories =
        [
            "Household",
            "Personal",
            "Car",
            "Cleaning"
        ],
        Remarks =
        [
            "Add stock",
            "Start use",
            "Used up",
            "Put back",
            "Changed / replaced"
        ]
    };

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public async Task<IReadOnlyList<SheetTask>> LoadTasksAsync()
    {
        if (!File.Exists(AppPaths.TaskCachePath))
        {
            return [];
        }

        await using var stream = File.OpenRead(AppPaths.TaskCachePath);
        return await JsonSerializer.DeserializeAsync<List<SheetTask>>(stream, SerializerOptions)
            ?? [];
    }

    public async Task SaveTasksAsync(IEnumerable<SheetTask> tasks)
    {
        Directory.CreateDirectory(AppPaths.DataDirectory);
        await using var stream = File.Create(AppPaths.TaskCachePath);
        await JsonSerializer.SerializeAsync(stream, tasks, SerializerOptions);
    }

    public async Task<IReadOnlyList<TrackItem>> LoadTrackItemsAsync()
    {
        if (!File.Exists(AppPaths.TrackItemsPath))
        {
            return [];
        }

        await using var stream = File.OpenRead(AppPaths.TrackItemsPath);
        return await JsonSerializer.DeserializeAsync<List<TrackItem>>(stream, SerializerOptions)
            ?? [];
    }

    public async Task SaveTrackItemsAsync(IEnumerable<TrackItem> items)
    {
        Directory.CreateDirectory(AppPaths.DataDirectory);
        await using var stream = File.Create(AppPaths.TrackItemsPath);
        await JsonSerializer.SerializeAsync(stream, items, SerializerOptions);
    }

    public async Task<TrackOptions> LoadTrackOptionsAsync()
    {
        Directory.CreateDirectory(AppPaths.DataDirectory);

        if (!File.Exists(AppPaths.TrackOptionsPath))
        {
            await SaveTrackOptionsAsync(DefaultTrackOptions);
            return DefaultTrackOptions;
        }

        await using var stream = File.OpenRead(AppPaths.TrackOptionsPath);
        return await JsonSerializer.DeserializeAsync<TrackOptions>(stream, SerializerOptions)
            ?? new TrackOptions();
    }

    public async Task SaveTrackOptionsAsync(TrackOptions options)
    {
        Directory.CreateDirectory(AppPaths.DataDirectory);
        await using var stream = File.Create(AppPaths.TrackOptionsPath);
        await JsonSerializer.SerializeAsync(stream, options, SerializerOptions);
    }

    public async Task<AppConfig> LoadConfigAsync()
    {
        Directory.CreateDirectory(AppPaths.DataDirectory);

        if (!File.Exists(AppPaths.ConfigPath))
        {
            var config = new AppConfig();
            await SaveConfigAsync(config);
            return config;
        }

        await using var stream = File.OpenRead(AppPaths.ConfigPath);
        return await JsonSerializer.DeserializeAsync<AppConfig>(stream, SerializerOptions)
            ?? new AppConfig();
    }

    public async Task SaveConfigAsync(AppConfig config)
    {
        Directory.CreateDirectory(AppPaths.DataDirectory);
        await using var stream = File.Create(AppPaths.ConfigPath);
        await JsonSerializer.SerializeAsync(stream, config, SerializerOptions);
    }
}
