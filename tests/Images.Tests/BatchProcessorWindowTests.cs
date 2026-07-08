using Images.Services;

namespace Images.Tests;

public sealed class BatchProcessorWindowTests
{
    [Fact]
    public void BuildResultRows_SkipsNullPartialResults()
    {
        var success = new MacroRunItemResult("source.png", "output.png", ["Wrote output.png."], null);
        var failure = new MacroRunItemResult("broken.png", "broken.png", [], "failed");
        MacroRunItemResult?[] partial = [null, success, null, failure];

        var rows = BatchProcessorWindow.BuildResultRows(partial);

        Assert.Equal(3, rows.Count);
        Assert.Contains("OK: source.png", rows[0], StringComparison.Ordinal);
        Assert.Equal("  Wrote output.png.", rows[1]);
        Assert.Contains("Failed: broken.png", rows[2], StringComparison.Ordinal);
    }
}
