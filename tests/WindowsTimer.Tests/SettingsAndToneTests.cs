using WindowsTimer.Models;
using WindowsTimer.Services;
using Xunit;

namespace WindowsTimer.Tests;

public class SettingsServiceTests
{
    [Fact]
    public void SaveAndLoad_RoundTripsSettings()
    {
        var path = Path.Combine(Path.GetTempPath(), "WindowsTimer.Tests", Guid.NewGuid().ToString("N"), "settings.json");
        var service = new SettingsService(path);
        var settings = new AppSettings
        {
            Theme = ThemePreference.Dark,
            AlertSound = AlertSoundId.Soft,
            Volume = 0.35,
            Mute = true,
            RepeatUntilDismissed = true,
            AlwaysOnTop = true,
            CloseBehavior = CloseBehavior.MinimizeToTray,
            LaunchOnStartup = true,
            HasSeenTrayHint = true,
            LastHours = 1,
            LastMinutes = 5,
            LastSeconds = 9
        };

        service.Save(settings);
        var loaded = service.Load();

        Assert.Equal(ThemePreference.Dark, loaded.Theme);
        Assert.Equal(AlertSoundId.Soft, loaded.AlertSound);
        Assert.Equal(0.35, loaded.Volume, 3);
        Assert.True(loaded.Mute);
        Assert.True(loaded.RepeatUntilDismissed);
        Assert.True(loaded.AlwaysOnTop);
        Assert.Equal(CloseBehavior.MinimizeToTray, loaded.CloseBehavior);
        Assert.True(loaded.LaunchOnStartup);
        Assert.True(loaded.HasSeenTrayHint);
        Assert.Equal(1, loaded.LastHours);
        Assert.Equal(5, loaded.LastMinutes);
        Assert.Equal(9, loaded.LastSeconds);
        Assert.Contains("\"theme\": \"Dark\"", File.ReadAllText(path));
    }

    [Fact]
    public void MissingFile_ReturnsDefaults()
    {
        var path = Path.Combine(Path.GetTempPath(), "WindowsTimer.Tests", Guid.NewGuid().ToString("N"), "missing.json");
        var settings = new SettingsService(path).Load();

        Assert.Equal(ThemePreference.System, settings.Theme);
        Assert.Equal(AlertSoundId.Bell, settings.AlertSound);
        Assert.Equal(0.4, settings.Volume, 3);
        Assert.False(settings.Mute);
        Assert.Equal(CloseBehavior.Exit, settings.CloseBehavior);
        Assert.Equal(25, settings.LastMinutes);
    }

    [Fact]
    public void CorruptFile_ReturnsDefaults()
    {
        var path = Path.Combine(Path.GetTempPath(), "WindowsTimer.Tests", Guid.NewGuid().ToString("N"), "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{ this is not json");

        var settings = new SettingsService(path).Load();

        Assert.Equal(ThemePreference.System, settings.Theme);
        Assert.Equal(25, settings.LastMinutes);
    }

    [Fact]
    public void OutOfRangeValues_AreClamped()
    {
        var path = Path.Combine(Path.GetTempPath(), "WindowsTimer.Tests", Guid.NewGuid().ToString("N"), "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, """
            {
              "volume": 9,
              "lastHours": 400,
              "lastMinutes": -5,
              "lastSeconds": 80,
              "theme": "Dark"
            }
            """);

        var settings = new SettingsService(path).Load();

        Assert.Equal(1, settings.Volume, 3);
        Assert.Equal(99, settings.LastHours);
        Assert.Equal(0, settings.LastMinutes);
        Assert.Equal(59, settings.LastSeconds);
        Assert.Equal(ThemePreference.Dark, settings.Theme);
    }
}

public class TimeFormatTests
{
    [Theory]
    [InlineData(0, "00:00:00")]
    [InlineData(0.1, "00:00:01")]
    [InlineData(1, "00:00:01")]
    [InlineData(61, "00:01:01")]
    [InlineData(3600, "01:00:00")]
    [InlineData(26 * 3600, "26:00:00")]
    public void FormatRemaining_RoundsUpPartialSeconds(double totalSeconds, string expected)
    {
        Assert.Equal(expected, TimeFormat.FormatRemaining(TimeSpan.FromSeconds(totalSeconds)));
    }
}

public class ToneLibraryTests
{
    [Theory]
    [InlineData(AlertSoundId.Bell)]
    [InlineData(AlertSoundId.Chime)]
    [InlineData(AlertSoundId.Soft)]
    public void CreateWav_ProducesLocalAudio(AlertSoundId id)
    {
        var wav = ToneLibrary.CreateWav(id, 0.4);

        Assert.True(wav.Length > 44);
        Assert.Equal((byte)'R', wav[0]);
        Assert.Equal((byte)'I', wav[1]);
        Assert.Equal((byte)'F', wav[2]);
        Assert.Equal((byte)'F', wav[3]);
        Assert.Contains(wav.Skip(44), sample => sample != 0);
    }

    [Fact]
    public void Sounds_AreDistinct_AndSilenceStaysSilent()
    {
        var bell = ToneLibrary.CreateWav(AlertSoundId.Bell, 0.4);
        var chime = ToneLibrary.CreateWav(AlertSoundId.Chime, 0.4);
        var silent = ToneLibrary.CreateWav(AlertSoundId.Bell, 0);

        Assert.NotEqual(bell.Length, chime.Length);
        Assert.All(silent.Skip(44), sample => Assert.Equal(0, sample));
    }
}

public class SleepPolicyTests
{
    [Fact]
    public void WallClockGap_IsNotCounted()
    {
        var counted = SleepPolicy.CountedElapsed(new SleepPolicy.SleepGap(
            TimeSpan.Zero,
            TimeSpan.FromHours(2)));

        Assert.Equal(TimeSpan.Zero, counted);
        Assert.Equal(
            TimeSpan.FromMinutes(20),
            SleepPolicy.RemainingAfterResume(TimeSpan.FromMinutes(20), counted));
    }

    [Fact]
    public void MonotonicElapsedDuringSleep_StillCounts()
    {
        var remaining = SleepPolicy.RemainingAfterResume(TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(3));
        Assert.Equal(TimeSpan.FromMinutes(7), remaining);
    }
}
