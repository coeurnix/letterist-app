using Letterist.Model;
using Xunit;

namespace Letterist.Tests.Model;

public class TextSearchTests
{
    [Fact]
    public void TryFindNext_MatchCase_RespectsCase()
    {
        var options = new TextSearchOptions
        {
            Query = "hello",
            MatchCase = true
        };

        var found = TextSearch.TryFindNext("Hello hello", options, 0, out var match);

        Assert.True(found);
        Assert.Equal(6, match.Start);
        Assert.Equal(5, match.Length);
    }

    [Fact]
    public void TryFindNext_WholeWord_SkipsSubstrings()
    {
        var options = new TextSearchOptions
        {
            Query = "he",
            WholeWord = true
        };

        var matches = TextSearch.FindAll("he hero the", options);

        Assert.Single(matches);
        Assert.Equal(0, matches[0].Start);
    }

    [Fact]
    public void FindAll_Regex_ReturnsAllMatches()
    {
        var options = new TextSearchOptions
        {
            Query = "\\d+",
            UseRegex = true
        };

        var matches = TextSearch.FindAll("A1 B22 C333", options);

        Assert.Equal(3, matches.Count);
        Assert.Equal(1, matches[0].Length);
        Assert.Equal(2, matches[1].Length);
        Assert.Equal(3, matches[2].Length);
    }

    [Fact]
    public void ReplaceAll_NonRegex_ReplacesEveryMatch()
    {
        var options = new TextSearchOptions
        {
            Query = "hello",
            MatchCase = false
        };

        var result = TextSearch.ReplaceAll("Hello hello", options, "hi", out var count);

        Assert.Equal(2, count);
        Assert.Equal("hi hi", result);
    }

    [Fact]
    public void ReplaceMatch_Regex_UsesGroups()
    {
        var options = new TextSearchOptions
        {
            Query = "Issue (\\d+)",
            UseRegex = true
        };

        var matches = TextSearch.FindAll("Issue 12", options);
        var result = TextSearch.ReplaceMatch("Issue 12", options, matches[0], "Issue #$1", out var replacementLength);

        Assert.Equal("Issue #12", result);
        Assert.Equal("Issue #12".Length - "Issue 12".Length + matches[0].Length, replacementLength);
    }
}
