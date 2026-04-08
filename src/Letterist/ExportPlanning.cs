using Letterist.Model;
using System.Text;

namespace Letterist;

internal static class ExportPlanning
{
    public static IReadOnlyList<string> ResolveLanguages(
        Document document,
        bool exportAllLanguages,
        bool visibleOnly,
        string? subset)
    {
        var active = Document.NormalizeLanguageTag(document.ActiveLanguage, document.BaseLanguage);
        if (!exportAllLanguages)
        {
            return new[] { active };
        }

        var subsetSet = ParseLanguageSubset(subset, document.BaseLanguage);
        var results = document.GetKnownLanguages()
            .Select(language => Document.NormalizeLanguageTag(language, document.BaseLanguage))
            .Where(language => !visibleOnly || document.IsLanguageVisibleInExport(language))
            .Where(language => subsetSet.Count == 0 || subsetSet.Contains(language))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(language => language, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (results.Count == 0)
        {
            results.Add(active);
        }

        return results;
    }

    public static string NormalizePattern(string? pattern, bool includePage, bool includeLayer, bool includeLanguage)
    {
        var result = string.IsNullOrWhiteSpace(pattern) ? "{document}" : pattern.Trim();

        if (includePage && result.IndexOf("{page}", StringComparison.OrdinalIgnoreCase) < 0)
        {
            result += "-page-{page}";
        }

        if (includeLayer && result.IndexOf("{layer}", StringComparison.OrdinalIgnoreCase) < 0)
        {
            result += "-{layer}";
        }

        if (includeLanguage && result.IndexOf("{lang}", StringComparison.OrdinalIgnoreCase) < 0)
        {
            result += "-{lang}";
        }

        return result;
    }

    public static string ExpandPattern(
        string pattern,
        string documentName,
        int pageNumber,
        int pagePadding,
        string? layerName,
        string? languageTag)
    {
        var safeDocument = SanitizeFileNameSegment(documentName);
        var safeLayer = SanitizeFileNameSegment(string.IsNullOrWhiteSpace(layerName) ? "Layer" : layerName);
        var safeLanguage = SanitizeFileNameSegment(string.IsNullOrWhiteSpace(languageTag) ? "und" : languageTag);
        var digits = Math.Clamp(pagePadding, 1, 8);
        var pageText = pageNumber.ToString($"D{digits}");

        var result = pattern;
        result = result.Replace("{document}", safeDocument, StringComparison.OrdinalIgnoreCase);
        result = result.Replace("{page}", pageText, StringComparison.OrdinalIgnoreCase);
        result = result.Replace("{layer}", safeLayer, StringComparison.OrdinalIgnoreCase);
        result = result.Replace("{lang}", safeLanguage, StringComparison.OrdinalIgnoreCase);
        return result;
    }

    public static string SanitizeFileNameSegment(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            builder.Append(Array.IndexOf(invalidChars, ch) >= 0 ? '_' : ch);
        }

        return builder.ToString().Trim();
    }

    private static HashSet<string> ParseLanguageSubset(string? subset, string fallbackLanguage)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(subset))
        {
            return result;
        }

        var tokens = subset.Split([',', ';', ' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var token in tokens)
        {
            result.Add(Document.NormalizeLanguageTag(token, fallbackLanguage));
        }

        return result;
    }
}
