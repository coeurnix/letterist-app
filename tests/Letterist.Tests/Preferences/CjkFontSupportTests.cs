using Xunit;

namespace Letterist.Tests.Preferences;

public class CjkFontSupportTests
{
    [Theory]
    [InlineData("こんにちは", true)]
    [InlineData("안녕하세요", true)]
    [InlineData("你好", true)]
    [InlineData("Hello", false)]
    public void ContainsCjkCharacters_DetectsScripts(string text, bool expected)
    {
        Assert.Equal(expected, CjkFontSupport.ContainsCjkCharacters(text));
    }

    [Fact]
    public void BuildFallbackChain_PutsPreferredFontFirst()
    {
        var chain = CjkFontSupport.BuildFallbackChain("MyFont", "ja", "サンプル", preferVerticalLayout: true);

        Assert.NotEmpty(chain);
        Assert.Equal("MyFont", chain[0]);
    }

    [Fact]
    public void ResolveFontFamily_LeavesLatinTextUnchanged()
    {
        var resolved = CjkFontSupport.ResolveFontFamily("Arial", "en", "Hello world", preferVerticalLayout: false);

        Assert.Equal("Arial", resolved);
    }

    [Fact]
    public void TryGetRecommendation_ReturnsChainForCjkText()
    {
        var ok = CjkFontSupport.TryGetRecommendation("en", "漢字テキスト", preferVerticalLayout: false, out var recommendation);

        Assert.True(ok);
        Assert.NotEmpty(recommendation.FallbackChain);
        Assert.False(string.IsNullOrWhiteSpace(recommendation.RecommendedFont));
    }
}
