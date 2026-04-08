using System.Collections.Generic;
using Letterist.Rendering.Typesetting;
using Xunit;

namespace Letterist.Tests.Rendering.Typesetting;

public class HyphenationTests
{
    private const char SoftHyphen = '\u00AD';

    [Fact]
    public void HyphenationPattern_ParsesLettersAndScores()
    {
        var pattern = new Hyphenation.HyphenationPattern("a1bc3d4");

        Assert.Equal("abcd", pattern.Text);
        Assert.Equal(new[] { 0, 1, 0, 3, 4 }, pattern.Scores);
    }

    [Fact]
    public void FindBreakPoints_InsertsSoftHyphenAtPatternMatches()
    {
        var patterns = new List<Hyphenation.HyphenationPattern>
        {
            new("com1"),
            new("pu1")
        };

        var result = Hyphenation.FindBreakPoints("computer", patterns, level: 50);

        Assert.Equal($"com{SoftHyphen}pu{SoftHyphen}ter", result);
    }

    [Fact]
    public void ApplyHyphenation_WithMissingLocale_PreservesPunctuationAndWhitespace()
    {
        const string text = "Hello,   world!\nNext line.";

        var result = Hyphenation.ApplyHyphenation(text, "zz-ZZ", level: 80);

        Assert.Equal(text, result);
    }
}
