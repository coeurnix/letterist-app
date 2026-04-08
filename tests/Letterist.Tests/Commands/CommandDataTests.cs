using Letterist.Commands;
using Letterist.Model;
using System.Text.Json;
using Xunit;

namespace Letterist.Tests.Commands;

public class CommandDataTests
{
    [Fact]
    public void Get_UsesAutomationJsonOptions_ForEnumAndCase()
    {
        using var doc = JsonDocument.Parse("{\"alignment\":\"center\",\"fitMode\":\"shrinkToFit\",\"overflowMode\":\"clip\",\"fontSize\":14}");
        var element = doc.RootElement.Clone();
        var data = new CommandData
        {
            Id = Guid.NewGuid(),
            Type = "Test",
            Parameters = new Dictionary<string, object?> { ["overrides"] = element }
        };

        var overrides = data.Get<TextStyleOverride>("overrides");

        Assert.NotNull(overrides);
        Assert.Equal(TextAlignment.Center, overrides!.Alignment);
        Assert.Equal(TextFitMode.ShrinkToFit, overrides.FitMode);
        Assert.Equal(TextOverflowMode.Clip, overrides.OverflowMode);
        Assert.Equal(14f, overrides.FontSize);
    }
}
