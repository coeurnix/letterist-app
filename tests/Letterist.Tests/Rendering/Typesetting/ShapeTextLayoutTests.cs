using Letterist.Model;
using Letterist.Rendering.Typesetting;
using Xunit;

namespace Letterist.Tests.Rendering.Typesetting;

public class ShapeTextLayoutTests
{
    [Fact]
    public void PrepareShapeLayoutTextForHyphenation_LeavesTextUnchanged_WhenLocaleIsSet()
    {
        var style = TextStyle.Default.With(hyphenationLocale: "en-US", hyphenationLevel: 80);

        var text = ShapeTextLayout.PrepareShapeLayoutTextForHyphenation("hyphenation", style);

        Assert.Equal("hyphenation", text);
    }

    [Fact]
    public void PrepareShapeLayoutTextForHyphenation_LeavesTextUnchanged_WhenLocaleIsNotSet()
    {
        var style = TextStyle.Default.With(hyphenationLocale: string.Empty, hyphenationLevel: 80);

        var text = ShapeTextLayout.PrepareShapeLayoutTextForHyphenation("hyphenation", style);

        Assert.Equal("hyphenation", text);
    }
}
