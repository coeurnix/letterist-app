using System.Text;
using System.Text.RegularExpressions;

namespace Letterist.Model;

public sealed class TextSearchOptions
{
    public string Query { get; init; } = "";
    public bool MatchCase { get; init; }
    public bool WholeWord { get; init; }
    public bool UseRegex { get; init; }
}

public readonly struct TextMatch
{
    public TextMatch(int start, int length)
    {
        Start = start;
        Length = length;
    }

    public int Start { get; }
    public int Length { get; }
}

public static class TextSearch
{
    public static bool TryFindNext(string text, TextSearchOptions options, int startIndex, out TextMatch match)
    {
        match = default;

        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(options.Query))
        {
            return false;
        }

        if (startIndex < 0) startIndex = 0;
        if (startIndex > text.Length) return false;

        if (options.UseRegex)
        {
            var regex = BuildRegex(options);
            var regexMatch = regex.Match(text, startIndex);
            if (!regexMatch.Success) return false;

            match = new TextMatch(regexMatch.Index, regexMatch.Length);
            return true;
        }

        var comparison = options.MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var index = text.IndexOf(options.Query, startIndex, comparison);
        while (index >= 0)
        {
            if (!options.WholeWord || IsWholeWordMatch(text, index, options.Query.Length))
            {
                match = new TextMatch(index, options.Query.Length);
                return true;
            }

            index = text.IndexOf(options.Query, index + 1, comparison);
        }

        return false;
    }

    public static IReadOnlyList<TextMatch> FindAll(string text, TextSearchOptions options)
    {
        var matches = new List<TextMatch>();
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(options.Query))
        {
            return matches;
        }

        if (options.UseRegex)
        {
            var regex = BuildRegex(options);
            foreach (Match match in regex.Matches(text))
            {
                if (match.Success)
                {
                    matches.Add(new TextMatch(match.Index, match.Length));
                }
            }

            return matches;
        }

        var startIndex = 0;
        while (TryFindNext(text, options, startIndex, out var matchResult))
        {
            matches.Add(matchResult);
            startIndex = matchResult.Start + matchResult.Length;
        }

        return matches;
    }

    public static string ReplaceAll(string text, TextSearchOptions options, string replacement, out int count)
    {
        count = 0;
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(options.Query))
        {
            return text;
        }

        if (options.UseRegex)
        {
            var regex = BuildRegex(options);
            var replacementCount = 0;
            var result = regex.Replace(text, match =>
            {
                replacementCount++;
                return match.Result(replacement);
            });

            count = replacementCount;
            return result;
        }

        var builder = new StringBuilder(text.Length);
        var startIndex = 0;
        while (TryFindNext(text, options, startIndex, out var matchResult))
        {
            builder.Append(text.AsSpan(startIndex, matchResult.Start - startIndex));
            builder.Append(replacement);
            count++;
            startIndex = matchResult.Start + matchResult.Length;
        }

        if (count == 0)
        {
            return text;
        }

        builder.Append(text.AsSpan(startIndex));
        return builder.ToString();
    }

    public static string ReplaceMatch(string text, TextSearchOptions options, TextMatch match, string replacement, out int replacementLength)
    {
        replacementLength = 0;
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        if (options.UseRegex)
        {
            var regex = BuildRegex(options);
            var regexMatch = regex.Match(text, match.Start);
            if (!regexMatch.Success || regexMatch.Index != match.Start)
            {
                return text;
            }

            var replacementText = regexMatch.Result(replacement);
            replacementLength = replacementText.Length;
            return string.Concat(
                text.AsSpan(0, match.Start),
                replacementText,
                text.AsSpan(match.Start + match.Length));
        }

        replacementLength = replacement.Length;
        return string.Concat(
            text.AsSpan(0, match.Start),
            replacement,
            text.AsSpan(match.Start + match.Length));
    }

    private static Regex BuildRegex(TextSearchOptions options)
    {
        var pattern = options.Query;
        if (options.WholeWord)
        {
            pattern = $@"\b(?:{pattern})\b";
        }

        var regexOptions = RegexOptions.Multiline | RegexOptions.CultureInvariant;
        if (!options.MatchCase)
        {
            regexOptions |= RegexOptions.IgnoreCase;
        }

        return new Regex(pattern, regexOptions);
    }

    private static bool IsWholeWordMatch(string text, int startIndex, int length)
    {
        var beforeIndex = startIndex - 1;
        var afterIndex = startIndex + length;

        var beforeOk = beforeIndex < 0 || !IsWordChar(text[beforeIndex]);
        var afterOk = afterIndex >= text.Length || !IsWordChar(text[afterIndex]);

        return beforeOk && afterOk;
    }

    private static bool IsWordChar(char c)
    {
        return char.IsLetterOrDigit(c) || c == '_';
    }
}
