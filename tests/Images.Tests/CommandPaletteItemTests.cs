using Images.ViewModels;

namespace Images.Tests;

public sealed class CommandPaletteItemTests
{
    [Fact]
    public void ToString_ReturnsDisplayNameForAutomationFallback()
    {
        var item = new CommandPaletteItem
        {
            CommandId = "open",
            Name = "Open file",
            Category = "File",
            Shortcut = "Ctrl+O"
        };

        Assert.Equal("Open file", item.ToString());
    }
}
