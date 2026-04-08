using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace Letterist.Tests.Preferences;

public class LocalizationTests
{
    [Theory]
    [InlineData("en-US", "en")]
    [InlineData("es-MX", "es")]
    [InlineData("pt", "pt-BR")]
    [InlineData("ja-JP", "ja")]
    [InlineData("zh", "zh-CN")]
    [InlineData("it-IT", "en")]
    public void NormalizeLanguageTag_MapsToSupportedLanguages(string raw, string expected)
    {
        var normalized = UiLocalizationService.NormalizeLanguageTag(raw);
        Assert.Equal(expected, normalized);
    }

    [Fact]
    public void GeneralPreferences_Normalize_UsesSupportedLanguageTags()
    {
        var preferences = new GeneralPreferences { Language = "fr-CA" };

        preferences.Normalize();

        Assert.Equal("fr", preferences.Language);
    }

    [Fact]
    public void GetString_FallsBackToEnglish_WhenLanguageResourceIsMissingKey()
    {
        UiLocalizationService.Initialize("es");

        var value = UiLocalizationService.GetString("prefs.dialog.title");

        Assert.False(string.IsNullOrWhiteSpace(value));
        Assert.NotEqual("prefs.dialog.title", value);
    }
}

public class LocalizationKeyParityTests
{
    private static readonly string LocalizationDir = FindLocalizationDirectory();
    private static readonly string[] NonEnglishLanguages = ["de", "es", "fr", "ja", "ko", "pt-br", "zh-cn"];

    private static string FindLocalizationDirectory()
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 10; i++)
        {
            var candidate = Path.Combine(dir, "src", "Letterist", "Localization");
            if (Directory.Exists(candidate))
                return candidate;
            dir = Path.GetDirectoryName(dir)!;
        }
        return Path.Combine(AppContext.BaseDirectory, "Localization");
    }

    private static Dictionary<string, string> LoadLanguage(string lang)
    {
        var path = Path.Combine(LocalizationDir, $"strings.{lang}.json");
        if (!File.Exists(path))
            return new Dictionary<string, string>();
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
    }

    [Fact]
    public void EnglishFile_Exists_AndHasKeys()
    {
        var en = LoadLanguage("en");
        Assert.True(en.Count > 0, "English strings file should exist and have keys");
    }

    [Theory]
    [InlineData("de")]
    [InlineData("es")]
    [InlineData("fr")]
    [InlineData("ja")]
    [InlineData("ko")]
    [InlineData("pt-br")]
    [InlineData("zh-cn")]
    public void LanguageFile_HasAllEnglishKeys(string lang)
    {
        var en = LoadLanguage("en");
        var target = LoadLanguage(lang);

        var missingKeys = en.Keys.Where(k => !target.ContainsKey(k)).OrderBy(k => k).ToList();

        Assert.True(missingKeys.Count == 0,
            $"Language '{lang}' is missing {missingKeys.Count} key(s):\n  {string.Join("\n  ", missingKeys.Take(20))}" +
            (missingKeys.Count > 20 ? $"\n  ... and {missingKeys.Count - 20} more" : ""));
    }

    [Theory]
    [InlineData("de")]
    [InlineData("es")]
    [InlineData("fr")]
    [InlineData("ja")]
    [InlineData("ko")]
    [InlineData("pt-br")]
    [InlineData("zh-cn")]
    public void LanguageFile_HasNoExtraKeys(string lang)
    {
        var en = LoadLanguage("en");
        var target = LoadLanguage(lang);

        var extraKeys = target.Keys.Where(k => !en.ContainsKey(k)).OrderBy(k => k).ToList();

        Assert.True(extraKeys.Count == 0,
            $"Language '{lang}' has {extraKeys.Count} extra key(s) not in English:\n  {string.Join("\n  ", extraKeys.Take(20))}");
    }

    [Theory]
    [InlineData("de")]
    [InlineData("es")]
    [InlineData("fr")]
    [InlineData("ja")]
    [InlineData("ko")]
    [InlineData("pt-br")]
    [InlineData("zh-cn")]
    public void LanguageFile_HasNoEmptyValues(string lang)
    {
        var target = LoadLanguage(lang);

        var emptyKeys = target.Where(kv => string.IsNullOrWhiteSpace(kv.Value)).Select(kv => kv.Key).OrderBy(k => k).ToList();

        Assert.True(emptyKeys.Count == 0,
            $"Language '{lang}' has {emptyKeys.Count} empty value(s):\n  {string.Join("\n  ", emptyKeys.Take(20))}");
    }

    [Theory]
    [InlineData("de")]
    [InlineData("es")]
    [InlineData("fr")]
    [InlineData("ja")]
    [InlineData("ko")]
    [InlineData("pt-br")]
    [InlineData("zh-cn")]
    public void LanguageFile_PreservesFormatPlaceholders(string lang)
    {
        var en = LoadLanguage("en");
        var target = LoadLanguage(lang);
        var placeholderPattern = new Regex(@"\{(\d+)(:[^}]*)?\}");

        var mismatches = new List<string>();
        foreach (var (key, enValue) in en)
        {
            if (!target.TryGetValue(key, out var targetValue))
                continue;

            var enPlaceholders = placeholderPattern.Matches(enValue).Select(m => m.Groups[1].Value).OrderBy(p => p).ToList();
            var targetPlaceholders = placeholderPattern.Matches(targetValue).Select(m => m.Groups[1].Value).OrderBy(p => p).ToList();

            if (!enPlaceholders.SequenceEqual(targetPlaceholders))
            {
                mismatches.Add($"{key}: en=[{string.Join(",", enPlaceholders)}] {lang}=[{string.Join(",", targetPlaceholders)}]");
            }
        }

        Assert.True(mismatches.Count == 0,
            $"Language '{lang}' has {mismatches.Count} placeholder mismatch(es):\n  {string.Join("\n  ", mismatches.Take(20))}");
    }

    [Fact]
    public void AllLanguageFiles_Exist()
    {
        foreach (var lang in NonEnglishLanguages)
        {
            var path = Path.Combine(LocalizationDir, $"strings.{lang}.json");
            Assert.True(File.Exists(path), $"Missing language file: strings.{lang}.json");
        }
    }

    [Fact]
    public void EnglishFile_HasNoEmptyValues()
    {
        var en = LoadLanguage("en");
        var emptyKeys = en.Where(kv => string.IsNullOrWhiteSpace(kv.Value)).Select(kv => kv.Key).OrderBy(k => k).ToList();

        Assert.True(emptyKeys.Count == 0,
            $"English file has {emptyKeys.Count} empty value(s):\n  {string.Join("\n  ", emptyKeys.Take(20))}");
    }
}
