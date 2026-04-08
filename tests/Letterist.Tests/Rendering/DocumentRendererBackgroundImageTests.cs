using Letterist.Model;
using Letterist.Rendering;
using Xunit;

namespace Letterist.Tests.Rendering;

public class DocumentRendererBackgroundImageTests
{
    [Fact]
    public void TryComputeBackgroundImageDrawRects_FillMode_CropsToPageBounds()
    {
        var ok = DocumentRenderer.TryComputeBackgroundImageDrawRects(
            new Size2(100, 100),
            imageWidth: 400,
            imageHeight: 100,
            PanelImageFitMode.Fill,
            out var destination,
            out var source);

        Assert.True(ok);
        AssertRect(destination, 0, 0, 100, 100);
        AssertRect(source, 150, 0, 100, 100);
    }

    [Fact]
    public void TryComputeBackgroundImageDrawRects_FitMode_DoesNotCropImage()
    {
        var ok = DocumentRenderer.TryComputeBackgroundImageDrawRects(
            new Size2(100, 100),
            imageWidth: 400,
            imageHeight: 100,
            PanelImageFitMode.Fit,
            out var destination,
            out var source);

        Assert.True(ok);
        AssertRect(destination, 0, 37.5, 100, 25);
        AssertRect(source, 0, 0, 400, 100);
    }

    [Fact]
    public void TryComputeBackgroundImageDrawRects_OriginalMode_ClipsOverflow()
    {
        var ok = DocumentRenderer.TryComputeBackgroundImageDrawRects(
            new Size2(100, 100),
            imageWidth: 200,
            imageHeight: 200,
            PanelImageFitMode.Original,
            out var destination,
            out var source);

        Assert.True(ok);
        AssertRect(destination, 0, 0, 100, 100);
        AssertRect(source, 50, 50, 100, 100);
    }

    [Fact]
    public void TryComputeBackgroundImageDrawRects_InvalidInput_ReturnsFalse()
    {
        var ok = DocumentRenderer.TryComputeBackgroundImageDrawRects(
            new Size2(0, 100),
            imageWidth: 200,
            imageHeight: 200,
            PanelImageFitMode.Fill,
            out _,
            out _);

        Assert.False(ok);
    }

    private static void AssertRect(Windows.Foundation.Rect rect, double x, double y, double width, double height)
    {
        const double epsilon = 0.001;
        Assert.InRange(rect.X, x - epsilon, x + epsilon);
        Assert.InRange(rect.Y, y - epsilon, y + epsilon);
        Assert.InRange(rect.Width, width - epsilon, width + epsilon);
        Assert.InRange(rect.Height, height - epsilon, height + epsilon);
    }
}
