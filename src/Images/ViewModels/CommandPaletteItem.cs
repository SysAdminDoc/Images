namespace Images.ViewModels;

public sealed class CommandPaletteItem
{
    public string Name { get; init; } = "";
    public string Shortcut { get; init; } = "";
    public string Category { get; init; } = "";
    public System.Windows.Input.ICommand? Command { get; init; }
}
