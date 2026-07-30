using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using LifeSyncTaskClient.Models;

namespace LifeSyncTaskClient.Services;

public sealed class JsonFileStore
{
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

    public async Task<List<TaskMutation>> LoadTaskSyncQueueAsync()
    {
        if (!File.Exists(AppPaths.TaskSyncQueuePath))
        {
            return [];
        }

        await using var stream = File.OpenRead(AppPaths.TaskSyncQueuePath);
        return await JsonSerializer.DeserializeAsync<List<TaskMutation>>(stream, SerializerOptions) ?? [];
    }

    public async Task SaveTaskSyncQueueAsync(IEnumerable<TaskMutation> mutations)
    {
        Directory.CreateDirectory(AppPaths.DataDirectory);
        await using var stream = File.Create(AppPaths.TaskSyncQueuePath);
        await JsonSerializer.SerializeAsync(stream, mutations, SerializerOptions);
    }

    public async Task<List<WatchListEntry>> LoadWatchListAsync()
    {
        if (!File.Exists(AppPaths.WatchListPath))
        {
            return [];
        }

        await using var stream = File.OpenRead(AppPaths.WatchListPath);
        return await JsonSerializer.DeserializeAsync<List<WatchListEntry>>(stream, SerializerOptions) ?? [];
    }

    public async Task SaveWatchListAsync(IEnumerable<WatchListEntry> entries)
    {
        Directory.CreateDirectory(AppPaths.DataDirectory);
        await using var stream = File.Create(AppPaths.WatchListPath);
        await JsonSerializer.SerializeAsync(stream, entries, SerializerOptions);
    }

    public async Task<CheckinSettings> LoadCheckinSettingsAsync()
    {
        Directory.CreateDirectory(AppPaths.DataDirectory);

        if (!File.Exists(AppPaths.CheckinSettingsPath))
        {
            var settings = CreateDefaultCheckinSettings();
            await SaveCheckinSettingsAsync(settings);
            return settings;
        }

        await using var stream = File.OpenRead(AppPaths.CheckinSettingsPath);
        var loadedSettings = await JsonSerializer.DeserializeAsync<CheckinSettings>(stream, SerializerOptions)
            ?? CreateDefaultCheckinSettings();

        return NormalizeCheckinSettings(loadedSettings);
    }

    public async Task SaveCheckinSettingsAsync(CheckinSettings settings)
    {
        Directory.CreateDirectory(AppPaths.DataDirectory);
        await using var stream = File.Create(AppPaths.CheckinSettingsPath);
        await JsonSerializer.SerializeAsync(stream, NormalizeCheckinSettings(settings), SerializerOptions);
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

    private static CheckinSettings CreateDefaultCheckinSettings()
    {
        return new CheckinSettings
        {
            Days = new ObservableCollection<CheckinDaySetting>(CreateDefaultCheckinDays())
        };
    }

    private static CheckinSettings NormalizeCheckinSettings(CheckinSettings settings)
    {
        var existingDays = settings.Days.ToDictionary(day => day.DayOfWeek);
        var normalizedDays = CreateDefaultCheckinDays()
            .Select(defaultDay =>
            {
                if (!existingDays.TryGetValue(defaultDay.DayOfWeek, out var existingDay))
                {
                    return defaultDay;
                }

                existingDay.DayName = defaultDay.DayName;
                existingDay.TimeText = NormalizeCheckinTime(existingDay.TimeText);
                return existingDay;
            });

        settings.Days = new ObservableCollection<CheckinDaySetting>(normalizedDays);
        settings.LastAlertDate = settings.LastAlertDate?.Date;
        return settings;
    }

    private static IEnumerable<CheckinDaySetting> CreateDefaultCheckinDays()
    {
        yield return new CheckinDaySetting { DayOfWeek = DayOfWeek.Monday, DayName = "Monday" };
        yield return new CheckinDaySetting { DayOfWeek = DayOfWeek.Tuesday, DayName = "Tuesday" };
        yield return new CheckinDaySetting { DayOfWeek = DayOfWeek.Wednesday, DayName = "Wednesday" };
        yield return new CheckinDaySetting { DayOfWeek = DayOfWeek.Thursday, DayName = "Thursday" };
        yield return new CheckinDaySetting { DayOfWeek = DayOfWeek.Friday, DayName = "Friday" };
        yield return new CheckinDaySetting { DayOfWeek = DayOfWeek.Saturday, DayName = "Saturday" };
        yield return new CheckinDaySetting { DayOfWeek = DayOfWeek.Sunday, DayName = "Sunday" };
    }

    private static string NormalizeCheckinTime(string? value)
    {
        var digits = new string((value ?? string.Empty).Where(char.IsDigit).Take(4).ToArray());
        return digits.Length == 4 ? digits : "1200";
    }
}
