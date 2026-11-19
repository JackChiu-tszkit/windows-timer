using System.Text.Json;
using System.Text.Json.Serialization;
using WindowsTimer.Models;

namespace WindowsTimer.Services;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public SettingsService(string? settingsPath = null)
    {
        SettingsPath = settingsPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WindowsTimer",
            "settings.json");
    }

    public string SettingsPath { get; }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
                return new AppSettings();

            var json = File.ReadAllText(SettingsPath);
            if (string.IsNullOrWhiteSpace(json))
                return new AppSettings();

            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            return Normalize(settings ?? new AppSettings());
        }
        catch (Exception ex)
        {
            AppLog.Warn("Settings could not be read. Defaults will be used. " + ex.GetType().Name);
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var normalized = Normalize(settings);
        var directory = Path.GetDirectoryName(SettingsPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(normalized, JsonOptions);
        var temporary = SettingsPath + ".tmp";
        File.WriteAllText(temporary, json);
        File.Move(temporary, SettingsPath, overwrite: true);
    }

    public static AppSettings Normalize(AppSettings settings)
    {
        settings.Volume = Math.Clamp(settings.Volume, 0, 1);
        settings.LastHours = Math.Clamp(settings.LastHours, 0, 99);
        settings.LastMinutes = Math.Clamp(settings.LastMinutes, 0, 59);
        settings.LastSeconds = Math.Clamp(settings.LastSeconds, 0, 59);
        if (!Enum.IsDefined(settings.Theme))
            settings.Theme = ThemePreference.System;
        if (!Enum.IsDefined(settings.AlertSound))
            settings.AlertSound = AlertSoundId.Bell;
        if (!Enum.IsDefined(settings.CloseBehavior))
            settings.CloseBehavior = CloseBehavior.Exit;
        return settings;
    }
}
