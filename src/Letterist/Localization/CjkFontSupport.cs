using Microsoft.Win32;

namespace Letterist;

public sealed record CjkFontRecommendation(
    string LanguageTag,
    string RecommendedFont,
    IReadOnlyList<string> FallbackChain,
    string SampleText,
    bool PreferVerticalLayout);

public static class CjkFontSupport
{
    private static readonly string[] GenericFallbackFonts =
    [
        "Yu Gothic UI",
        "Microsoft YaHei UI",
        "Malgun Gothic",
        "Segoe UI",
        "Arial Unicode MS"
    ];

    private static readonly Dictionary<string, string[]> HorizontalCandidates = new(StringComparer.OrdinalIgnoreCase)
    {
        ["zh-CN"] = ["Microsoft YaHei UI", "Microsoft YaHei", "DengXian", "SimSun", "SimHei"],
        ["ja"] = ["Yu Gothic UI", "Meiryo UI", "Meiryo", "Yu Mincho", "MS Gothic", "MS Mincho"],
        ["ko"] = ["Malgun Gothic", "Gulim", "Dotum", "Batang"]
    };

    private static readonly Dictionary<string, string[]> VerticalCandidates = new(StringComparer.OrdinalIgnoreCase)
    {
        ["zh-CN"] = ["SimSun", "NSimSun", "FangSong", "KaiTi", "Microsoft YaHei"],
        ["ja"] = ["Yu Mincho", "MS Mincho", "Yu Gothic UI", "Meiryo"],
        ["ko"] = ["Batang", "Gulim", "Malgun Gothic"]
    };

    private static readonly Lazy<HashSet<string>> InstalledFonts = new(LoadInstalledFonts, isThreadSafe: true);

    public static bool IsCjkLanguageTag(string? languageTag)
    {
        var primary = GetPrimaryLanguageTag(languageTag);
        return string.Equals(primary, "zh", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(primary, "ja", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(primary, "ko", StringComparison.OrdinalIgnoreCase);
    }

    public static bool ContainsCjkCharacters(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        foreach (var ch in text)
        {
            if (IsCjkCharacter(ch))
            {
                return true;
            }
        }

        return false;
    }

    public static string ResolveFontFamily(string? preferredFont, string? languageTag, string? sampleText, bool preferVerticalLayout)
    {
        var preferred = string.IsNullOrWhiteSpace(preferredFont) ? "Segoe UI" : preferredFont.Trim();
        var normalizedLanguage = ResolveCjkLanguage(languageTag, sampleText);
        if (!IsCjkLanguageTag(normalizedLanguage) && !ContainsCjkCharacters(sampleText))
        {
            return preferred;
        }

        var fallbackChain = BuildFallbackChain(preferred, normalizedLanguage, sampleText, preferVerticalLayout);
        foreach (var candidate in fallbackChain)
        {
            if (IsFontInstalled(candidate))
            {
                return candidate;
            }
        }

        return preferred;
    }

    public static IReadOnlyList<string> BuildFallbackChain(string? preferredFont, string? languageTag, string? sampleText, bool preferVerticalLayout)
    {
        var normalizedLanguage = ResolveCjkLanguage(languageTag, sampleText);
        var result = new List<string>();

        void Add(string? candidate)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                return;
            }

            var normalized = candidate.Trim();
            if (result.Any(existing => string.Equals(existing, normalized, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            result.Add(normalized);
        }

        Add(preferredFont);

        var candidates = GetCandidates(normalizedLanguage, preferVerticalLayout);
        foreach (var candidate in candidates)
        {
            Add(candidate);
        }

        foreach (var generic in GenericFallbackFonts)
        {
            Add(generic);
        }

        return result;
    }

    public static bool TryGetRecommendation(string? languageTag, string? sampleText, bool preferVerticalLayout, out CjkFontRecommendation recommendation)
    {
        recommendation = new CjkFontRecommendation("en", string.Empty, Array.Empty<string>(), string.Empty, preferVerticalLayout);

        var normalizedLanguage = ResolveCjkLanguage(languageTag, sampleText);
        if (!IsCjkLanguageTag(normalizedLanguage) && !ContainsCjkCharacters(sampleText))
        {
            return false;
        }

        var chain = BuildFallbackChain(null, normalizedLanguage, sampleText, preferVerticalLayout);
        var recommended = chain.FirstOrDefault(IsFontInstalled) ?? chain.FirstOrDefault() ?? "Segoe UI";
        recommendation = new CjkFontRecommendation(
            normalizedLanguage,
            recommended,
            chain,
            GetPreviewSample(normalizedLanguage),
            preferVerticalLayout);
        return true;
    }

    public static string GetPreviewSample(string? languageTag)
    {
        var normalized = NormalizeLanguageTag(languageTag);
        return normalized switch
        {
            "zh-CN" => "示例文本：字形预览",
            "ja" => "サンプル：縦組みプレビュー",
            "ko" => "샘플 텍스트: 글꼴 미리보기",
            _ => "Sample text"
        };
    }

    public static bool IsFontInstalled(string? fontFamily)
    {
        if (string.IsNullOrWhiteSpace(fontFamily))
        {
            return false;
        }

        var candidate = fontFamily.Trim();
        if (InstalledFonts.Value.Contains(candidate))
        {
            return true;
        }

        return InstalledFonts.Value.Any(name =>
            name.StartsWith(candidate + " ", StringComparison.OrdinalIgnoreCase));
    }

    private static string[] GetCandidates(string normalizedLanguage, bool preferVerticalLayout)
    {
        var table = preferVerticalLayout ? VerticalCandidates : HorizontalCandidates;
        if (table.TryGetValue(normalizedLanguage, out var direct))
        {
            return direct;
        }

        var primary = GetPrimaryLanguageTag(normalizedLanguage);
        if (string.Equals(primary, "zh", StringComparison.OrdinalIgnoreCase) && table.TryGetValue("zh-CN", out var zh))
        {
            return zh;
        }

        if (string.Equals(primary, "ja", StringComparison.OrdinalIgnoreCase) && table.TryGetValue("ja", out var ja))
        {
            return ja;
        }

        if (string.Equals(primary, "ko", StringComparison.OrdinalIgnoreCase) && table.TryGetValue("ko", out var ko))
        {
            return ko;
        }

        return Array.Empty<string>();
    }

    private static string ResolveCjkLanguage(string? languageTag, string? sampleText)
    {
        var normalized = NormalizeLanguageTag(languageTag);
        if (IsCjkLanguageTag(normalized))
        {
            return normalized;
        }

        var detected = DetectCjkLanguageFromText(sampleText);
        return string.IsNullOrWhiteSpace(detected)
            ? normalized
            : detected;
    }

    private static string NormalizeLanguageTag(string? languageTag)
    {
        if (string.IsNullOrWhiteSpace(languageTag))
        {
            return "en";
        }

        var normalized = languageTag.Trim();
        if (string.Equals(normalized, "zh", StringComparison.OrdinalIgnoreCase)) return "zh-CN";
        if (normalized.StartsWith("zh-", StringComparison.OrdinalIgnoreCase)) return "zh-CN";
        if (normalized.StartsWith("ja", StringComparison.OrdinalIgnoreCase)) return "ja";
        if (normalized.StartsWith("ko", StringComparison.OrdinalIgnoreCase)) return "ko";
        return normalized;
    }

    private static string GetPrimaryLanguageTag(string? languageTag)
    {
        if (string.IsNullOrWhiteSpace(languageTag))
        {
            return string.Empty;
        }

        var dashIndex = languageTag.IndexOf('-');
        return dashIndex > 0
            ? languageTag[..dashIndex]
            : languageTag;
    }

    private static string? DetectCjkLanguageFromText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var hasHan = false;
        var hasKana = false;
        var hasHangul = false;

        foreach (var ch in text)
        {
            if (IsKana(ch)) hasKana = true;
            if (IsHangul(ch)) hasHangul = true;
            if (IsHan(ch) || IsBopomofo(ch)) hasHan = true;
        }

        if (hasHangul) return "ko";
        if (hasKana) return "ja";
        if (hasHan || IsCjkLanguageTag(UiLocalizationService.CurrentLanguage)) return "zh-CN";

        return null;
    }

    private static bool IsCjkCharacter(char ch)
    {
        return IsHan(ch) || IsKana(ch) || IsHangul(ch) || IsBopomofo(ch);
    }

    private static bool IsHan(char ch)
    {
        return (ch >= '\u3400' && ch <= '\u4DBF') ||
               (ch >= '\u4E00' && ch <= '\u9FFF') ||
               (ch >= '\uF900' && ch <= '\uFAFF');
    }

    private static bool IsKana(char ch)
    {
        return (ch >= '\u3040' && ch <= '\u309F') ||
               (ch >= '\u30A0' && ch <= '\u30FF') ||
               (ch >= '\u31F0' && ch <= '\u31FF');
    }

    private static bool IsHangul(char ch)
    {
        return (ch >= '\u1100' && ch <= '\u11FF') ||
               (ch >= '\u3130' && ch <= '\u318F') ||
               (ch >= '\uAC00' && ch <= '\uD7AF');
    }

    private static bool IsBopomofo(char ch)
    {
        return (ch >= '\u3100' && ch <= '\u312F') ||
               (ch >= '\u31A0' && ch <= '\u31BF');
    }

    private static HashSet<string> LoadInstalledFonts()
    {
        var fonts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        AddRegistryFontNames(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts", fonts);
        AddRegistryFontNames(Registry.CurrentUser, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts", fonts);

        if (fonts.Count == 0)
        {
            fonts.Add("Segoe UI");
            fonts.Add("Yu Gothic UI");
            fonts.Add("Microsoft YaHei UI");
            fonts.Add("Malgun Gothic");
        }

        return fonts;
    }

    private static void AddRegistryFontNames(RegistryKey hive, string subKeyPath, HashSet<string> target)
    {
        using var key = hive.OpenSubKey(subKeyPath);
        if (key == null)
        {
            return;
        }

        foreach (var valueName in key.GetValueNames())
        {
            var normalized = valueName;
            var markerIndex = normalized.IndexOf(" (", StringComparison.Ordinal);
            if (markerIndex > 0)
            {
                normalized = normalized[..markerIndex];
            }

            normalized = normalized.Trim();
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                target.Add(normalized);
            }
        }
    }
}
