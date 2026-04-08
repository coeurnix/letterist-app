using Letterist.Model;
using Xunit;

namespace Letterist.Tests.Model;

public class BalloonStylePatternTests
{
    [Fact]
    public void BalloonStyleOverride_FromDifference_CapturesPatternFields()
    {
        var baseStyle = BalloonStyle.Default.With(
            patternEnabled: false,
            patternType: TextFillPattern.DiagonalStripes,
            patternScale: 1f);

        var targetStyle = baseStyle.With(
            patternEnabled: true,
            patternType: TextFillPattern.Crosshatch,
            patternSecondaryColor: new Color(210, 190, 120),
            patternScale: 2.5f,
            patternAngle: 27f,
            patternImagePath: @"C:\fills\paper.png");

        var diff = BalloonStyleOverride.FromDifference(baseStyle, targetStyle);
        var resolved = diff.ApplyTo(baseStyle);

        Assert.True(diff.PatternEnabled);
        Assert.Equal(TextFillPattern.Crosshatch, diff.PatternType);
        Assert.Equal(new Color(210, 190, 120), diff.PatternSecondaryColor);
        Assert.Equal(2.5f, diff.PatternScale);
        Assert.Equal(27f, diff.PatternAngle);
        Assert.Equal(@"C:\fills\paper.png", diff.PatternImagePath);

        Assert.True(resolved.PatternEnabled);
        Assert.Equal(TextFillPattern.Crosshatch, resolved.PatternType);
        Assert.Equal(new Color(210, 190, 120), resolved.PatternSecondaryColor);
        Assert.Equal(2.5f, resolved.PatternScale);
        Assert.Equal(27f, resolved.PatternAngle);
        Assert.Equal(@"C:\fills\paper.png", resolved.PatternImagePath);
    }

    [Fact]
    public void BalloonStyleUtilities_AreEquivalent_DetectsPatternChanges()
    {
        var styleA = BalloonStyle.Default.With(
            patternEnabled: true,
            patternType: TextFillPattern.Dots,
            patternSecondaryColor: new Color(240, 210, 150),
            patternScale: 1.25f,
            patternAngle: 20f,
            patternImagePath: @"C:\fills\noise.png");
        var styleB = BalloonStyle.Default.With(
            patternEnabled: true,
            patternType: TextFillPattern.Dots,
            patternSecondaryColor: new Color(240, 210, 150),
            patternScale: 1.25f,
            patternAngle: 20f,
            patternImagePath: @"c:\fills\NOISE.png");
        var styleC = styleB.With(patternImagePath: @"C:\fills\other.png");

        Assert.True(BalloonStyleUtilities.AreEquivalent(styleA, styleB));
        Assert.False(BalloonStyleUtilities.AreEquivalent(styleA, styleC));
    }

    [Fact]
    public void BalloonStyleOverride_FromDifference_CapturesThoughtSmoothness()
    {
        var baseStyle = BalloonStyle.Default.With(thoughtSmoothness: 0.2f);
        var targetStyle = BalloonStyle.Default.With(thoughtSmoothness: 0.8f);

        var diff = BalloonStyleOverride.FromDifference(baseStyle, targetStyle);
        var resolved = diff.ApplyTo(baseStyle);

        Assert.Equal(0.8f, diff.ThoughtSmoothness);
        Assert.Equal(0.8f, resolved.ThoughtSmoothness);
    }

    [Fact]
    public void BalloonStyleUtilities_AreEquivalent_DetectsThoughtSmoothnessChanges()
    {
        var styleA = BalloonStyle.Default.With(thoughtSmoothness: 0.35f);
        var styleB = BalloonStyle.Default.With(thoughtSmoothness: 0.35f);
        var styleC = BalloonStyle.Default.With(thoughtSmoothness: 0.9f);

        Assert.True(BalloonStyleUtilities.AreEquivalent(styleA, styleB));
        Assert.False(BalloonStyleUtilities.AreEquivalent(styleA, styleC));
    }
}
