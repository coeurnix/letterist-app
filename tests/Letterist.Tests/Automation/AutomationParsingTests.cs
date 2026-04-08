using System.Globalization;
using System.Reflection;
using Letterist;
using Xunit;

namespace Letterist.Tests.Automation;

public class AutomationParsingTests
{
    private static readonly MethodInfo ParseQueryFloatMethod =
        typeof(MainWindow).GetMethod("ParseQueryFloat", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("Could not locate MainWindow.ParseQueryFloat");

    [Fact]
    public void ParseQueryFloat_UsesInvariantCulture_ForDotDecimalValues()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("fr-FR");

            var value = InvokeParseQueryFloat("1.5", 0f);

            Assert.Equal(1.5f, value, 3);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void ParseQueryFloat_FallsBackToCurrentCulture_ForCommaDecimalValues()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("fr-FR");

            var value = InvokeParseQueryFloat("1,5", 0f);

            Assert.Equal(1.5f, value, 3);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    private static float InvokeParseQueryFloat(string raw, float defaultValue)
    {
        var result = ParseQueryFloatMethod.Invoke(null, [raw, defaultValue]);
        return Assert.IsType<float>(result);
    }
}
