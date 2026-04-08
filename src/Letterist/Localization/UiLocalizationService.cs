using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;

namespace Letterist;

public sealed record SupportedLanguage(string Tag, string DisplayName);

public static class UiLocalizationService
{
    public const string DefaultLanguage = "en";

    private static readonly IReadOnlyList<SupportedLanguage> SupportedLanguageList = new ReadOnlyCollection<SupportedLanguage>(
    [
        new SupportedLanguage("en", "English"),
        new SupportedLanguage("zh-CN", "Chinese (Simplified)"),
        new SupportedLanguage("ja", "Japanese"),
        new SupportedLanguage("ko", "Korean"),
        new SupportedLanguage("es", "Spanish"),
        new SupportedLanguage("fr", "French"),
        new SupportedLanguage("de", "German"),
        new SupportedLanguage("pt-BR", "Portuguese (Brazil)")
    ]);

    private static readonly Dictionary<string, string> CanonicalLanguageTags =
        SupportedLanguageList.ToDictionary(language => language.Tag, language => language.Tag, StringComparer.OrdinalIgnoreCase);

    private static readonly object Sync = new();
    private static readonly Dictionary<string, IReadOnlyDictionary<string, string>> StringCache = new(StringComparer.OrdinalIgnoreCase);

    public static event EventHandler? LanguageChanged;

    public static IReadOnlyList<SupportedLanguage> SupportedLanguages => SupportedLanguageList;

    public static string CurrentLanguage { get; private set; } = DefaultLanguage;

    public static void Initialize(string? preferredLanguage, bool useSystemLanguageIfNotSet = true)
    {
        EnsureLanguageLoaded(DefaultLanguage);

        var languageToUse = preferredLanguage;
        if (useSystemLanguageIfNotSet && string.IsNullOrWhiteSpace(preferredLanguage))
        {
            languageToUse = DetectSystemLanguage();
        }

        _ = SetLanguage(languageToUse, raiseEvent: false);
    }

    public static string DetectSystemLanguage()
    {
        try
        {
            var systemCulture = System.Globalization.CultureInfo.CurrentUICulture;
            var normalized = NormalizeLanguageTag(systemCulture.Name);

            if (!string.Equals(normalized, DefaultLanguage, StringComparison.OrdinalIgnoreCase) ||
                systemCulture.TwoLetterISOLanguageName.Equals("en", StringComparison.OrdinalIgnoreCase))
            {
                return normalized;
            }

            var currentCulture = System.Globalization.CultureInfo.CurrentCulture;
            return NormalizeLanguageTag(currentCulture.Name);
        }
        catch
        {
            return DefaultLanguage;
        }
    }

    public static bool SetLanguage(string? language, bool raiseEvent = true)
    {
        var normalized = NormalizeLanguageTag(language);
        EnsureLanguageLoaded(normalized);

        if (string.Equals(CurrentLanguage, normalized, StringComparison.OrdinalIgnoreCase))
        {
            ApplyCulture(normalized);
            return false;
        }

        CurrentLanguage = normalized;
        ApplyCulture(normalized);

        if (raiseEvent)
        {
            LanguageChanged?.Invoke(null, EventArgs.Empty);
        }

        return true;
    }

    public static string NormalizeLanguageTag(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return DefaultLanguage;
        }

        var trimmed = language.Trim();

        if (CanonicalLanguageTags.TryGetValue(trimmed, out var directMatch))
        {
            return directMatch;
        }

        string? cultureName = null;
        try
        {
            cultureName = CultureInfo.GetCultureInfo(trimmed).Name;
        }
        catch (CultureNotFoundException)
        {
        }

        if (!string.IsNullOrWhiteSpace(cultureName) && CanonicalLanguageTags.TryGetValue(cultureName, out var cultureMatch))
        {
            return cultureMatch;
        }

        var primary = GetPrimaryLanguageSubtag(cultureName ?? trimmed);
        if (string.IsNullOrWhiteSpace(primary))
        {
            return DefaultLanguage;
        }

        var primaryMatch = SupportedLanguageList.FirstOrDefault(languageOption =>
            string.Equals(GetPrimaryLanguageSubtag(languageOption.Tag), primary, StringComparison.OrdinalIgnoreCase));

        return primaryMatch?.Tag ?? DefaultLanguage;
    }

    public static string GetString(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return string.Empty;
        }

        var language = CurrentLanguage;
        if (TryGetString(language, key, out var localized))
        {
            return localized;
        }

        if (!string.Equals(language, DefaultLanguage, StringComparison.OrdinalIgnoreCase) &&
            TryGetString(DefaultLanguage, key, out var fallback))
        {
            return fallback;
        }

        return key;
    }

    public static string Format(string key, params object[] args)
    {
        var template = GetString(key);
        return args is { Length: > 0 }
            ? string.Format(CultureInfo.CurrentCulture, template, args)
            : template;
    }

    private static bool TryGetString(string language, string key, out string value)
    {
        EnsureLanguageLoaded(language);
        if (StringCache.TryGetValue(language, out var map) && map.TryGetValue(key, out var localized))
        {
            value = localized;
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static void EnsureLanguageLoaded(string language)
    {
        lock (Sync)
        {
            if (StringCache.ContainsKey(language))
            {
                return;
            }

            var fileLanguageTag = language.ToLowerInvariant();
            var path = Path.Combine(AppContext.BaseDirectory, "Localization", $"strings.{fileLanguageTag}.json");
            if (!File.Exists(path))
            {
                StringCache[language] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                return;
            }

            try
            {
                var json = File.ReadAllText(path);
                var map = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
                StringCache[language] = new Dictionary<string, string>(map, StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                StringCache[language] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
        }
    }

    private static void ApplyCulture(string language)
    {
        try
        {
            var culture = CultureInfo.GetCultureInfo(language);
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
        }
        catch (CultureNotFoundException)
        {
            var culture = CultureInfo.GetCultureInfo(DefaultLanguage);
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
        }
    }

    private static string GetPrimaryLanguageSubtag(string language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return string.Empty;
        }

        var dashIndex = language.IndexOf('-');
        return dashIndex < 0
            ? language
            : language[..dashIndex];
    }
}
