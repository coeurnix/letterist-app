using System.Globalization;
using System.Text;

namespace Letterist.Publishing;

internal enum PdfVersion
{
    Pdf14,
    Pdf15,
    Pdf16,
    Pdf17,
    Pdf20
}

internal enum PdfConformance
{
    None,
    PdfX1A2001,
    PdfX4
}

internal enum PdfFontEmbeddingMode
{
    None,
    Subset,
    Full
}

internal enum PdfColorMode
{
    Rgb,
    Cmyk
}

internal sealed class PdfExportSettings
{
    public PdfVersion Version { get; set; } = PdfVersion.Pdf17;
    public PdfConformance Conformance { get; set; } = PdfConformance.None;
    public PdfFontEmbeddingMode FontEmbeddingMode { get; set; } = PdfFontEmbeddingMode.Subset;
    public PdfColorMode ColorMode { get; set; } = PdfColorMode.Rgb;
    public string IccProfileName { get; set; } = string.Empty;
    public bool IncludePrinterMarks { get; set; }
    public bool ExportSpreads { get; set; }
    public float? CustomPageWidthPoints { get; set; }
    public float? CustomPageHeightPoints { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Keywords { get; set; } = string.Empty;
    public DateTime CreationTimeUtc { get; set; } = DateTime.UtcNow;
}

internal sealed class PdfRenderedPage
{
    public required byte[] ImageBytes { get; init; }
    public required int PixelWidth { get; init; }
    public required int PixelHeight { get; init; }
    public required float Dpi { get; init; }
    public string Label { get; init; } = string.Empty;
}

internal sealed class PdfExportResult
{
    public required int PageCount { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}

internal static class PdfExportService
{
    private sealed class OutputImage
    {
        public required int ObjectId { get; init; }
        public required string ResourceName { get; init; }
        public required byte[] Bytes { get; init; }
        public required int PixelWidth { get; init; }
        public required int PixelHeight { get; init; }
        public required float WidthPoints { get; init; }
        public required float HeightPoints { get; init; }
        public required float DrawX { get; init; }
        public required float DrawY { get; init; }
    }

    private sealed class OutputPage
    {
        public required int PageObjectId { get; init; }
        public required int ContentObjectId { get; init; }
        public required List<OutputImage> Images { get; init; }
        public required float WidthPoints { get; init; }
        public required float HeightPoints { get; init; }
    }

    public static PdfExportResult WriteImagePdf(string outputPath, IReadOnlyList<PdfRenderedPage> sourcePages, PdfExportSettings? settings = null)
    {
        if (sourcePages.Count == 0)
        {
            throw new InvalidOperationException("Cannot export PDF without rendered pages.");
        }

        settings ??= new PdfExportSettings();
        var warnings = BuildWarnings(settings);

        var outputPages = BuildOutputPages(sourcePages, settings);
        WritePdfDocument(outputPath, outputPages, settings);

        return new PdfExportResult
        {
            PageCount = outputPages.Count,
            Warnings = warnings
        };
    }

    private static List<string> BuildWarnings(PdfExportSettings settings)
    {
        var warnings = new List<string>();

        if (settings.FontEmbeddingMode != PdfFontEmbeddingMode.None)
        {
            warnings.Add("PDF export is image-based; live font embedding is unavailable. Source fonts are rasterized.");
        }

        if (settings.ColorMode == PdfColorMode.Cmyk)
        {
            warnings.Add("CMYK output is simulated from the RGB render pipeline.");
            if (string.IsNullOrWhiteSpace(settings.IccProfileName))
            {
                warnings.Add("CMYK mode selected without an ICC profile name.");
            }
        }

        if (settings.Conformance != PdfConformance.None && settings.ColorMode != PdfColorMode.Cmyk)
        {
            warnings.Add("PDF/X conformance generally expects CMYK workflow and validated output intent.");
        }

        return warnings;
    }

    private static List<OutputPage> BuildOutputPages(IReadOnlyList<PdfRenderedPage> sourcePages, PdfExportSettings settings)
    {
        var outputPages = new List<OutputPage>();
        var pairs = settings.ExportSpreads
            ? CreateSpreadPairs(sourcePages)
            : sourcePages.Select(page => (left: page, right: (PdfRenderedPage?)null)).ToList();

        var objectId = 10;
        foreach (var pair in pairs)
        {
            var left = pair.left;
            var right = pair.right;

            var leftWidthPts = PixelsToPoints(left.PixelWidth, left.Dpi);
            var leftHeightPts = PixelsToPoints(left.PixelHeight, left.Dpi);
            var rightWidthPts = right != null ? PixelsToPoints(right.PixelWidth, right.Dpi) : 0f;
            var rightHeightPts = right != null ? PixelsToPoints(right.PixelHeight, right.Dpi) : 0f;

            var naturalPageWidth = settings.ExportSpreads ? leftWidthPts + rightWidthPts : leftWidthPts;
            var naturalPageHeight = settings.ExportSpreads
                ? Math.Max(leftHeightPts, rightHeightPts)
                : leftHeightPts;

            var pageWidth = settings.CustomPageWidthPoints.HasValue && settings.CustomPageWidthPoints.Value > 0
                ? settings.CustomPageWidthPoints.Value
                : naturalPageWidth;
            var pageHeight = settings.CustomPageHeightPoints.HasValue && settings.CustomPageHeightPoints.Value > 0
                ? settings.CustomPageHeightPoints.Value
                : naturalPageHeight;

            var images = new List<OutputImage>();
            if (settings.ExportSpreads)
            {
                var leftCellWidth = pageWidth / 2f;
                images.Add(BuildFittedImage(
                    objectId++,
                    "Im1",
                    left.ImageBytes,
                    left.PixelWidth,
                    left.PixelHeight,
                    leftWidthPts,
                    leftHeightPts,
                    0f,
                    leftCellWidth,
                    pageHeight));

                if (right != null)
                {
                    images.Add(BuildFittedImage(
                        objectId++,
                        "Im2",
                        right.ImageBytes,
                        right.PixelWidth,
                        right.PixelHeight,
                        rightWidthPts,
                        rightHeightPts,
                        leftCellWidth,
                        leftCellWidth,
                        pageHeight));
                }
            }
            else
            {
                images.Add(BuildFittedImage(
                    objectId++,
                    "Im1",
                    left.ImageBytes,
                    left.PixelWidth,
                    left.PixelHeight,
                    leftWidthPts,
                    leftHeightPts,
                    0f,
                    pageWidth,
                    pageHeight));
            }

            outputPages.Add(new OutputPage
            {
                PageObjectId = objectId++,
                ContentObjectId = objectId++,
                Images = images,
                WidthPoints = pageWidth,
                HeightPoints = pageHeight
            });
        }

        return outputPages;
    }

    private static List<(PdfRenderedPage left, PdfRenderedPage? right)> CreateSpreadPairs(IReadOnlyList<PdfRenderedPage> sourcePages)
    {
        var pairs = new List<(PdfRenderedPage left, PdfRenderedPage? right)>();
        for (int i = 0; i < sourcePages.Count; i += 2)
        {
            var left = sourcePages[i];
            var right = i + 1 < sourcePages.Count ? sourcePages[i + 1] : null;
            pairs.Add((left, right));
        }

        return pairs;
    }

    private static OutputImage BuildFittedImage(
        int objectId,
        string resourceName,
        byte[] bytes,
        int pixelWidth,
        int pixelHeight,
        float naturalWidth,
        float naturalHeight,
        float cellX,
        float cellWidth,
        float cellHeight)
    {
        var widthScale = cellWidth / Math.Max(1f, naturalWidth);
        var heightScale = cellHeight / Math.Max(1f, naturalHeight);
        var scale = MathF.Min(widthScale, heightScale);

        var drawWidth = naturalWidth * scale;
        var drawHeight = naturalHeight * scale;
        var drawX = cellX + (cellWidth - drawWidth) * 0.5f;
        var drawY = (cellHeight - drawHeight) * 0.5f;

        return new OutputImage
        {
            ObjectId = objectId,
            ResourceName = resourceName,
            Bytes = bytes,
            PixelWidth = Math.Max(1, pixelWidth),
            PixelHeight = Math.Max(1, pixelHeight),
            WidthPoints = drawWidth,
            HeightPoints = drawHeight,
            DrawX = drawX,
            DrawY = drawY
        };
    }

    private static void WritePdfDocument(string outputPath, IReadOnlyList<OutputPage> pages, PdfExportSettings settings)
    {
        var directory = Path.GetDirectoryName(outputPath);
        Directory.CreateDirectory(string.IsNullOrWhiteSpace(directory) ? "." : directory);

        using var stream = File.Create(outputPath);
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: false);

        var rootObjectId = 1;
        var pagesObjectId = 2;
        var infoObjectId = 3;

        var objectBodies = new Dictionary<int, byte[]>();

        foreach (var page in pages)
        {
            foreach (var image in page.Images)
            {
                objectBodies[image.ObjectId] = BuildStreamObject(
                    $"<< /Type /XObject /Subtype /Image /Width {image.PixelWidth} /Height {image.PixelHeight} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode",
                    image.Bytes);
            }

            var contentCommands = BuildPageContent(page, settings.IncludePrinterMarks);
            var contentBytes = Encoding.ASCII.GetBytes(contentCommands);
            objectBodies[page.ContentObjectId] = BuildStreamObject("<<", contentBytes);

            var xObjectEntries = string.Join(" ", page.Images.Select(image => $"/{image.ResourceName} {image.ObjectId} 0 R"));
            var pageDictionary =
                $"<< /Type /Page /Parent {pagesObjectId} 0 R /MediaBox [0 0 {FormatPdfNumber(page.WidthPoints)} {FormatPdfNumber(page.HeightPoints)}] /Resources << /ProcSet [/PDF /ImageC] /XObject << {xObjectEntries} >> >> /Contents {page.ContentObjectId} 0 R >>";
            objectBodies[page.PageObjectId] = Encoding.ASCII.GetBytes(pageDictionary);
        }

        var pageRefs = string.Join(" ", pages.Select(page => $"{page.PageObjectId} 0 R"));
        objectBodies[pagesObjectId] = Encoding.ASCII.GetBytes($"<< /Type /Pages /Count {pages.Count} /Kids [{pageRefs}] >>");

        objectBodies[rootObjectId] = Encoding.ASCII.GetBytes($"<< /Type /Catalog /Pages {pagesObjectId} 0 R >>");
        objectBodies[infoObjectId] = Encoding.ASCII.GetBytes(BuildInfoDictionary(settings));

        var maxObjectId = objectBodies.Keys.Max();
        var version = GetPdfVersionString(settings.Version);
        writer.Write(Encoding.ASCII.GetBytes($"%PDF-{version}\n%\u00e2\u00e3\u00cf\u00d3\n"));

        var offsets = new long[maxObjectId + 1];
        for (int id = 1; id <= maxObjectId; id++)
        {
            if (!objectBodies.TryGetValue(id, out var body))
            {
                continue;
            }

            offsets[id] = stream.Position;
            writer.Write(Encoding.ASCII.GetBytes($"{id} 0 obj\n"));
            writer.Write(body);
            writer.Write(Encoding.ASCII.GetBytes("\nendobj\n"));
        }

        var xrefPosition = stream.Position;
        writer.Write(Encoding.ASCII.GetBytes($"xref\n0 {maxObjectId + 1}\n"));
        writer.Write(Encoding.ASCII.GetBytes("0000000000 65535 f \n"));

        for (int id = 1; id <= maxObjectId; id++)
        {
            var offset = offsets[id];
            writer.Write(Encoding.ASCII.GetBytes($"{offset:0000000000} 00000 n \n"));
        }

        var trailer = $"trailer\n<< /Size {maxObjectId + 1} /Root {rootObjectId} 0 R /Info {infoObjectId} 0 R >>\nstartxref\n{xrefPosition}\n%%EOF";
        writer.Write(Encoding.ASCII.GetBytes(trailer));
    }

    private static byte[] BuildStreamObject(string dictionaryPrefix, byte[] streamBytes)
    {
        var dictionary = $"{dictionaryPrefix} /Length {streamBytes.Length} >>";
        using var ms = new MemoryStream();
        ms.Write(Encoding.ASCII.GetBytes(dictionary));
        ms.WriteByte((byte)'\n');
        ms.Write(Encoding.ASCII.GetBytes("stream\n"));
        ms.Write(streamBytes);
        ms.WriteByte((byte)'\n');
        ms.Write(Encoding.ASCII.GetBytes("endstream"));
        return ms.ToArray();
    }

    private static string BuildPageContent(OutputPage page, bool includeMarks)
    {
        var builder = new StringBuilder();

        foreach (var image in page.Images)
        {
            builder.Append("q\n");
            builder.Append(
                $"{FormatPdfNumber(image.WidthPoints)} 0 0 {FormatPdfNumber(image.HeightPoints)} {FormatPdfNumber(image.DrawX)} {FormatPdfNumber(image.DrawY)} cm\n");
            builder.Append($"/{image.ResourceName} Do\n");
            builder.Append("Q\n");
        }

        if (includeMarks)
        {
            AppendPrinterMarks(builder, page.WidthPoints, page.HeightPoints);
        }

        return builder.ToString();
    }

    private static void AppendPrinterMarks(StringBuilder builder, float pageWidth, float pageHeight)
    {
        const float markLength = 12f;
        const float offset = 6f;

        builder.Append("0 0 0 RG\n");
        builder.Append("0.35 w\n");

        AppendLine(builder, -offset, 0, -offset - markLength, 0);
        AppendLine(builder, -offset, pageHeight, -offset - markLength, pageHeight);
        AppendLine(builder, pageWidth + offset, 0, pageWidth + offset + markLength, 0);
        AppendLine(builder, pageWidth + offset, pageHeight, pageWidth + offset + markLength, pageHeight);

        AppendLine(builder, 0, -offset, 0, -offset - markLength);
        AppendLine(builder, pageWidth, -offset, pageWidth, -offset - markLength);
        AppendLine(builder, 0, pageHeight + offset, 0, pageHeight + offset + markLength);
        AppendLine(builder, pageWidth, pageHeight + offset, pageWidth, pageHeight + offset + markLength);
    }

    private static void AppendLine(StringBuilder builder, float x1, float y1, float x2, float y2)
    {
        builder.Append($"{FormatPdfNumber(x1)} {FormatPdfNumber(y1)} m {FormatPdfNumber(x2)} {FormatPdfNumber(y2)} l S\n");
    }

    private static string BuildInfoDictionary(PdfExportSettings settings)
    {
        var title = EscapePdfString(settings.Title);
        var author = EscapePdfString(settings.Author);
        var subject = EscapePdfString(settings.Subject);
        var keywords = EscapePdfString(settings.Keywords);
        var created = settings.CreationTimeUtc.ToUniversalTime().ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);

        return
            $"<< /Title ({title}) /Author ({author}) /Subject ({subject}) /Keywords ({keywords}) /Creator (Letterist) /Producer (Letterist PDF Export) /CreationDate (D:{created}Z) >>";
    }

    private static string EscapePdfString(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("(", "\\(", StringComparison.Ordinal)
            .Replace(")", "\\)", StringComparison.Ordinal);
    }

    private static float PixelsToPoints(int pixels, float dpi)
    {
        var safeDpi = dpi <= 0 ? 300f : dpi;
        return pixels / safeDpi * 72f;
    }

    private static string GetPdfVersionString(PdfVersion version)
    {
        return version switch
        {
            PdfVersion.Pdf14 => "1.4",
            PdfVersion.Pdf15 => "1.5",
            PdfVersion.Pdf16 => "1.6",
            PdfVersion.Pdf20 => "2.0",
            _ => "1.7"
        };
    }

    private static string FormatPdfNumber(float value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
