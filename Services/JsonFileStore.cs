using System.Collections.ObjectModel;
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

    public async Task<TrackSettings> LoadTrackSettingsAsync()
    {
        Directory.CreateDirectory(AppPaths.DataDirectory);

        if (!File.Exists(AppPaths.TrackSettingsPath))
        {
            var settings = new TrackSettings();
            await SaveTrackSettingsAsync(settings);
            return settings;
        }

        await using var stream = File.OpenRead(AppPaths.TrackSettingsPath);
        return await JsonSerializer.DeserializeAsync<TrackSettings>(stream, SerializerOptions)
            ?? new TrackSettings();
    }

    public async Task SaveTrackSettingsAsync(TrackSettings settings)
    {
        Directory.CreateDirectory(AppPaths.DataDirectory);
        await using var stream = File.Create(AppPaths.TrackSettingsPath);
        await JsonSerializer.SerializeAsync(stream, settings, SerializerOptions);
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
