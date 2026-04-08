using System;
using System.IO;
using Xunit;

namespace Letterist.Tests.Export;

public class ComicArchiveToolsTests
{
    [Fact]
    public void BuildRarCreateArguments_IncludesExpectedFlags()
    {
        var args = ComicArchiveTools.BuildRarCreateArguments(@"C:\out\book.cbr", @"C:\tmp\files.lst");

        Assert.Equal("a", args[0]);
        Assert.Contains("-ep1", args);
        Assert.Contains("-idq", args);
        Assert.Contains("-m5", args);
        Assert.Contains("-ma5", args);
        Assert.Equal(@"C:\out\book.cbr", args[5]);
        Assert.Equal(@"@C:\tmp\files.lst", args[6]);
    }

    [Fact]
    public void ResolveRarExecutable_ReturnsConfiguredPathWhenFileExists()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"rar-test-{Guid.NewGuid():N}.exe");
        File.WriteAllText(tempPath, "stub");

        try
        {
            var resolved = ComicArchiveTools.ResolveRarExecutable(tempPath);
            Assert.Equal(tempPath, resolved);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }
}
