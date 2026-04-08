using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Letterist.Rendering.Typesetting;

public static class Hyphenation
{
    private const char SoftHyphen = '\u00AD';
    private static readonly object _lock = new object();
    private static readonly Dictionary<string, List<HyphenationPattern>> _patternCache = new();

    public class HyphenationPattern
    {
        public string Text { get; }
        public int[] Scores { get; }

        public HyphenationPattern(string pattern)
        {
            var letters = new List<char>(pattern.Length);
            var scores = new List<int>(pattern.Length + 1) { 0 };

            foreach (var c in pattern)
            {
                if (char.IsDigit(c))
                {
                    scores[^1] = c - '0';
                }
                else
                {
                    letters.Add(char.ToLowerInvariant(c));
                    scores.Add(0);
                }
            }

            if (letters.Count == 0)
            {
                throw new ArgumentException("Hyphenation pattern must contain at least one letter.", nameof(pattern));
            }

            Text = new string(letters.ToArray());
            Scores = scores.ToArray();
        }
    }

    public static List<HyphenationPattern> LoadPatternsForLocale(string locale)
    {
        lock (_lock)
        {
            if (_patternCache.TryGetValue(locale, out var cached))
            {
                return cached;
            }
        }

        var patterns = new List<HyphenationPattern>();

        try
        {
            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            var hyphenationDir = Path.Combine(appDir, "hyphenation");

            if (!Directory.Exists(hyphenationDir))
            {
                return patterns;
            }

            var patternFile = ResolvePatternFilePath(hyphenationDir, locale);
            if (patternFile == null || !File.Exists(patternFile))
            {
                return patterns;
            }

            foreach (var line in File.ReadLines(patternFile))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) ||
                    trimmed.StartsWith("%", StringComparison.Ordinal) ||
                    trimmed.StartsWith("\\", StringComparison.Ordinal))
                {
                    continue;
                }

                var tokens = trimmed
                    .Trim('{', '}')
                    .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

                foreach (var token in tokens)
                {
                    var candidate = token.Trim();
                    if (!LooksLikePattern(candidate))
                    {
                        continue;
                    }

                    try
                    {
                        patterns.Add(new HyphenationPattern(candidate));
                    }
                    catch
                    {
                    }
                }
            }

            lock (_lock)
            {
                _patternCache[locale] = patterns;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading hyphenation patterns for {locale}: {ex.Message}");
        }

        return patterns;
    }

    private static string? ResolvePatternFilePath(string hyphenationDir, string locale)
    {
        var patternFiles = Directory.GetFiles(hyphenationDir, "hyph-*.pat.txt");
        if (patternFiles.Length == 0)
        {
            return null;
        }

        var normalizedLocale = locale.Trim().ToLowerInvariant().Replace('_', '-');
        var languageCode = normalizedLocale.Split('-', 2)[0];

        var exact = patternFiles.FirstOrDefault(file =>
            Path.GetFileName(file).StartsWith($"hyph-{normalizedLocale}.", StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrEmpty(exact))
        {
            return exact;
        }

        return patternFiles.FirstOrDefault(file =>
        {
            var fileName = Path.GetFileName(file);
            return fileName.StartsWith($"hyph-{languageCode}-", StringComparison.OrdinalIgnoreCase) ||
                   fileName.Equals($"hyph-{languageCode}.pat.txt", StringComparison.OrdinalIgnoreCase);
        });
    }

    private static bool LooksLikePattern(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var hasLetter = false;
        foreach (var c in token)
        {
            if (char.IsLetter(c))
            {
                hasLetter = true;
                continue;
            }

            if (char.IsDigit(c) || c == '.')
            {
                continue;
            }

            return false;
        }

        return hasLetter;
    }

    public static string FindBreakPoints(string word, List<HyphenationPattern> patterns, int level)
    {
        if (string.IsNullOrEmpty(word) || word.Length < 4)
        {
            return word;
        }

        if (patterns.Count == 0)
        {
            return word;
        }

        var normalized = "." + word.ToLowerInvariant() + ".";
        var breakScores = new int[normalized.Length + 1];

        for (int i = 0; i < patterns.Count; i++)
        {
            var pattern = patterns[i];
            if (pattern.Text.Length == 0 || pattern.Text.Length > normalized.Length)
            {
                continue;
            }

            for (int pos = 0; pos <= normalized.Length - pattern.Text.Length; pos++)
            {
                var matches = true;

                for (int j = 0; j < pattern.Text.Length && matches; j++)
                {
                    if (pattern.Text[j] != normalized[pos + j])
                    {
                        matches = false;
                    }
                }

                if (!matches)
                {
                    continue;
                }

                for (int j = 0; j < pattern.Scores.Length; j++)
                {
                    var scorePos = pos + j;
                    if (scorePos < breakScores.Length)
                    {
                        breakScores[scorePos] = Math.Max(breakScores[scorePos], pattern.Scores[j]);
                    }
                }
            }
        }

        var minPrefix = level > 70 ? 2 : level > 30 ? 2 : 3;
        var minSuffix = level > 70 ? 2 : level > 30 ? 3 : 3;
        var maxConsecutive = level > 70 ? 4 : level > 30 ? 3 : 2;

        var result = new StringBuilder(word);
        var insertedBreakCount = 0;
        var lastBreakPos = -1;

        for (int i = 1; i < word.Length; i++)
        {
            var normalizedBreakIndex = i + 1;
            var score = normalizedBreakIndex < breakScores.Length ? breakScores[normalizedBreakIndex] : 0;

            if (score <= 0 || score % 2 == 0)
            {
                continue;
            }

            if (i < minPrefix || i > word.Length - minSuffix)
            {
                continue;
            }

            if (lastBreakPos >= 0 && i - lastBreakPos < 2)
            {
                continue;
            }

            if (insertedBreakCount >= maxConsecutive)
            {
                break;
            }

            result.Insert(i + insertedBreakCount, SoftHyphen);
            insertedBreakCount++;
            lastBreakPos = i;
        }

        return result.ToString();
    }

    public static string ApplyHyphenation(string text, string locale, int level)
    {
        if (level <= 0 || string.IsNullOrEmpty(text))
        {
            return text;
        }

        var patterns = LoadPatternsForLocale(locale);
        if (patterns.Count == 0 || text.Length < 4)
        {
            return text;
        }

        var result = new StringBuilder(text.Length + Math.Max(8, text.Length / 8));
        var index = 0;
        while (index < text.Length)
        {
            if (!char.IsLetter(text[index]))
            {
                result.Append(text[index]);
                index++;
                continue;
            }

            var tokenStart = index;
            while (index < text.Length && (char.IsLetter(text[index]) || text[index] == '\'' || text[index] == '\u2019'))
            {
                index++;
            }

            var token = text[tokenStart..index];
            result.Append(HyphenateToken(token, patterns, level));
        }

        return result.ToString();
    }

    private static string HyphenateToken(string token, List<HyphenationPattern> patterns, int level)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return token;
        }

        var result = new StringBuilder(token.Length);
        var index = 0;
        while (index < token.Length)
        {
            if (!char.IsLetter(token[index]))
            {
                result.Append(token[index]);
                index++;
                continue;
            }

            var wordStart = index;
            while (index < token.Length && char.IsLetter(token[index]))
            {
                index++;
            }

            var word = token[wordStart..index];
            if (word.Length < 4)
            {
                result.Append(word);
            }
            else
            {
                result.Append(FindBreakPoints(word, patterns, level));
            }
        }

        return result.ToString();
    }
}
