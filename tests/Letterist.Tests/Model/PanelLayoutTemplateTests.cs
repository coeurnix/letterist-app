using System;
using System.Linq;
using Letterist.Model;
using Xunit;

namespace Letterist.Tests.Model;

public class PanelLayoutTemplateTests
{
    [Fact]
    public void CreateDefaults_IncludesPageTypeCategories()
    {
        var templates = PanelLayoutTemplate.CreateDefaults(new Size2(1200, 1800));
        var categories = templates
            .Select(template => template.Category)
            .Where(category => !string.IsNullOrWhiteSpace(category))
            .Cast<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Contains("Current Page", categories);
        Assert.Contains("US Letter", categories);
        Assert.Contains("A4", categories);
        Assert.Contains("Manga B5", categories);
        Assert.Contains("3-Panel Strip", categories);
        Assert.Contains("4-Panel Strip", categories);
        Assert.Contains("Webtoon", categories);
    }

    [Fact]
    public void CreateDefaults_ContainsNamedWebAndMobileTemplates()
    {
        var templates = PanelLayoutTemplate.CreateDefaults(new Size2(1200, 1800));

        Assert.Contains(templates, template => template.Name == "US Letter - Hero + 3 supports");
        Assert.Contains(templates, template => template.Name == "3-Panel Strip - Equal thirds");
        Assert.Contains(templates, template => template.Name == "Square - Triptych");
        Assert.Contains(templates, template => template.Name == "Webtoon - Action drop");
    }

    [Fact]
    public void CreateDefaults_ProvidesExpandedLibrary()
    {
        var templates = PanelLayoutTemplate.CreateDefaults(new Size2(1200, 1800));
        var sizeCount = templates
            .Select(template => $"{template.Size.Width:F0}x{template.Size.Height:F0}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        Assert.True(templates.Count >= 50);
        Assert.True(sizeCount >= 10);
        Assert.All(templates, template => Assert.True(template.Panels.Count > 0));
    }

    [Fact]
    public void CreateDefaults_DoublesUSComicAndMangaB5TemplateCoverage()
    {
        var templates = PanelLayoutTemplate.CreateDefaults(new Size2(1200, 1800));

        var usComicTemplates = templates.Count(template => string.Equals(template.Category, "US Comic", StringComparison.OrdinalIgnoreCase));
        var mangaB5Templates = templates.Count(template => string.Equals(template.Category, "Manga B5", StringComparison.OrdinalIgnoreCase));

        Assert.True(usComicTemplates >= 10);
        Assert.True(mangaB5Templates >= 10);
    }
}
