using Letterist.Publishing;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace Letterist.Tests.Export;

public class PdfExportServiceTests
{
    private static readonly byte[] TinyJpeg = Convert.FromBase64String(
        "/9j/4AAQSkZJRgABAQAAAQABAAD/2wCEAAkGBxAQEBAQEA8PDw8PDw8PDw8PDw8PDw8PFREWFhURFRUYHSggGBolGxUVITEhJSkrLi4uFx8zODMsNygtLisBCgoKDg0OGhAQGi0lHyUtLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLf/AABEIAAEAAQMBIgACEQEDEQH/xAAZAAADAQEBAAAAAAAAAAAAAAACAwQBBQb/xAAgEAABAwQDAQAAAAAAAAAAAAABAgMEAAUREiExQSIy/8QAFQEBAQAAAAAAAAAAAAAAAAAAAwT/xAAZEQEAAwEBAAAAAAAAAAAAAAAAAQIRAzH/2gAMAwEAAhEDEQA/AN4QAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABGm0bG7l0w8sG3mrrhV4d4MspqSyb1v0vQ0fWT9P7jZ8lkq7Y7HttY5M+Y7Wj8k4vN4Rr4oAAAAAAAAAAAAAAAAAAAAAAB//2Q==");

    [Fact]
    public void WriteImagePdf_WritesValidHeader_AndWarnings()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"letterist-pdf-test-{Guid.NewGuid():N}.pdf");

        try
        {
            var pages = new[]
            {
                new PdfRenderedPage
                {
                    ImageBytes = TinyJpeg,
                    PixelWidth = 1,
                    PixelHeight = 1,
                    Dpi = 300,
                    Label = "Page 1"
                }
            };

            var result = PdfExportService.WriteImagePdf(
                tempPath,
                pages,
                new PdfExportSettings
                {
                    ColorMode = PdfColorMode.Cmyk,
                    FontEmbeddingMode = PdfFontEmbeddingMode.Subset,
                    IccProfileName = string.Empty
                });

            Assert.True(File.Exists(tempPath));
            Assert.Equal(1, result.PageCount);
            Assert.True(result.Warnings.Count >= 2);

            var header = new byte[8];
            using var stream = File.OpenRead(tempPath);
            stream.ReadExactly(header, 0, header.Length);
            var headerText = System.Text.Encoding.ASCII.GetString(header);
            Assert.StartsWith("%PDF-", headerText, StringComparison.Ordinal);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    [Fact]
    public void WriteImagePdf_ExportSpreads_ReducesPageCount()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"letterist-pdf-spread-test-{Guid.NewGuid():N}.pdf");

        try
        {
            var pages = Enumerable.Range(0, 3)
                .Select(index => new PdfRenderedPage
                {
                    ImageBytes = TinyJpeg,
                    PixelWidth = 1,
                    PixelHeight = 1,
                    Dpi = 300,
                    Label = $"Page {index + 1}"
                })
                .ToArray();

            var result = PdfExportService.WriteImagePdf(
                tempPath,
                pages,
                new PdfExportSettings
                {
                    ExportSpreads = true,
                    FontEmbeddingMode = PdfFontEmbeddingMode.None
                });

            Assert.Equal(2, result.PageCount);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }
}
