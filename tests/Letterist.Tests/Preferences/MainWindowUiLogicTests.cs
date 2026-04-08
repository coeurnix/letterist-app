using Xunit;

namespace Letterist.Tests.Preferences;

public class MainWindowUiLogicTests
{
    [Theory]
    [InlineData(520, 500, 360, 220, 0)]
    [InlineData(380, 500, 360, 220, 1)]
    [InlineData(210, 500, 360, 220, 2)]
    public void ChooseLeftSidebarTabHeaderVisualMode_SelectsExpectedMode(
        double availableWidth,
        double textAndIconWidth,
        double textOnlyWidth,
        double iconOnlyWidth,
        int expected)
    {
        var mode = MainWindow.ChooseLeftSidebarTabHeaderVisualMode(
            availableWidth,
            textAndIconWidth,
            textOnlyWidth,
            iconOnlyWidth);

        Assert.Equal(expected, (int)mode);
    }

    [Theory]
    [InlineData(720, 700, 520, 300, 0)]
    [InlineData(560, 700, 520, 300, 1)]
    [InlineData(260, 700, 520, 300, 2)]
    public void ChoosePropertiesTabHeaderVisualMode_SelectsExpectedMode(
        double availableWidth,
        double textAndIconWidth,
        double textOnlyWidth,
        double iconOnlyWidth,
        int expected)
    {
        var mode = MainWindow.ChoosePropertiesTabHeaderVisualMode(
            availableWidth,
            textAndIconWidth,
            textOnlyWidth,
            iconOnlyWidth);

        Assert.Equal(expected, (int)mode);
    }

    [Theory]
    [InlineData("en", "The quick brown fox jumps over the lazy dog")]
    [InlineData("es-MX", "El veloz murcielago hindu come feliz kiwi")]
    [InlineData("fr-CA", "Portez ce vieux whisky au juge blond qui fume")]
    [InlineData("de-DE", "Victor jagt zwolf Boxkampfer quer uber den grossen Deich")]
    [InlineData("pt", "A rapida raposa marrom salta sobre o cao preguicoso")]
    [InlineData("ja-JP", "いろはにほへと ちりぬるを")]
    [InlineData("ko-KR", "키스의 고유조건은 입술끼리 만나야 한다")]
    [InlineData("zh", "视野无边，字里行间皆有风景")]
    public void GetFontSampleTextForLanguage_ReturnsLocalizedSample(string languageTag, string expected)
    {
        var sample = MainWindow.GetFontSampleTextForLanguage(languageTag);
        Assert.Equal(expected, sample);
    }
}
