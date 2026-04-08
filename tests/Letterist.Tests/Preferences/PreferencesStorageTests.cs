using Letterist.Persistence;
using System.IO;
using Xunit;

namespace Letterist.Tests.Preferences;

public class PreferencesStorageTests
{
    [Fact]
    public void LoadFromPath_ReturnsDefaults_WhenMissing()
    {
        var path = Path.Combine(Path.GetTempPath(), $"letterist-pref-{Guid.NewGuid():N}.json");
        var preferences = PreferencesStorage.LoadFromPath(path);

        Assert.NotNull(preferences);
        Assert.Equal(300, preferences.General.DefaultDpi);
        Assert.Equal(10, preferences.General.RecentFilesCount);
    }

    [Fact]
    public void SaveAndLoadFromPath_RoundTripsValues()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"letterist-pref-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "preferences.json");

        var source = AppPreferences.CreateDefault();
        source.General.DefaultDpi = 600;
        source.General.AutosaveIntervalSeconds = 45;
        source.Canvas.ZoomStepPercent = 15f;
        source.BalloonDefaults.Shape = Letterist.Model.BalloonShape.Whisper;
        source.ExportDefaults.RarExecutablePath = @"C:\Tools\rar.exe";

        PreferencesStorage.SaveToPath(source, path);
        var loaded = PreferencesStorage.LoadFromPath(path);

        Assert.Equal(600, loaded.General.DefaultDpi);
        Assert.Equal(45, loaded.General.AutosaveIntervalSeconds);
        Assert.Equal(15f, loaded.Canvas.ZoomStepPercent);
        Assert.Equal(Letterist.Model.BalloonShape.Whisper, loaded.BalloonDefaults.Shape);
        Assert.Equal(@"C:\Tools\rar.exe", loaded.ExportDefaults.RarExecutablePath);
    }

    [Fact]
    public void LoadFromPath_NormalizesInvalidValues()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"letterist-pref-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "preferences.json");

        var invalid = AppPreferences.CreateDefault();
        invalid.General.DefaultDpi = -20;
        invalid.General.RecentFilesCount = 0;
        invalid.Canvas.ScrollSpeed = 999f;

        PreferencesStorage.SaveToPath(invalid, path);
        var loaded = PreferencesStorage.LoadFromPath(path);

        Assert.Equal(72, loaded.General.DefaultDpi);
        Assert.Equal(1, loaded.General.RecentFilesCount);
        Assert.Equal(8f, loaded.Canvas.ScrollSpeed);
    }

    [Theory]
    [InlineData("en", 1988f, 3075f)]
    [InlineData("en-US", 1988f, 3075f)]
    [InlineData("ja", 2150f, 3035f)]
    [InlineData("zh-CN", 2150f, 3035f)]
    [InlineData("ko-KR", 2150f, 3035f)]
    [InlineData("fr", 2480f, 3508f)]
    public void GetDefaultPageSizeForLanguage_UsesExpectedPreset(string language, float expectedWidth, float expectedHeight)
    {
        var size = GeneralPreferences.GetDefaultPageSizeForLanguage(language);

        Assert.Equal(expectedWidth, size.Width);
        Assert.Equal(expectedHeight, size.Height);
    }

    [Fact]
    public void GetEffectivePageSize_UsesExplicitPreferenceWhenSet()
    {
        var prefs = new GeneralPreferences
        {
            Language = "ja",
            IsPageSizeExplicitlySet = true,
            DefaultPageWidth = 1234f,
            DefaultPageHeight = 2345f
        };

        var size = prefs.GetEffectivePageSize();
        Assert.Equal(1234f, size.Width);
        Assert.Equal(2345f, size.Height);
    }
}
