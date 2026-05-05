namespace Images.Services;

public enum DirectorySortMode
{
    NaturalName,
    NameDescending,
    ModifiedNewest,
    ModifiedOldest,
    CreatedNewest,
    CreatedOldest,
    SizeLargest,
    SizeSmallest,
    ExtensionThenName
}

public static class DirectorySortModeInfo
{
    public static IReadOnlyList<DirectorySortMode> All { get; } =
    [
        DirectorySortMode.NaturalName,
        DirectorySortMode.NameDescending,
        DirectorySortMode.ModifiedNewest,
        DirectorySortMode.ModifiedOldest,
        DirectorySortMode.CreatedNewest,
        DirectorySortMode.CreatedOldest,
        DirectorySortMode.SizeLargest,
        DirectorySortMode.SizeSmallest,
        DirectorySortMode.ExtensionThenName
    ];

    public static string DisplayName(DirectorySortMode mode) => mode switch
    {
        DirectorySortMode.NaturalName => "Name (natural)",
        DirectorySortMode.NameDescending => "Name (Z to A)",
        DirectorySortMode.ModifiedNewest => "Modified (newest first)",
        DirectorySortMode.ModifiedOldest => "Modified (oldest first)",
        DirectorySortMode.CreatedNewest => "Created (newest first)",
        DirectorySortMode.CreatedOldest => "Created (oldest first)",
        DirectorySortMode.SizeLargest => "Size (largest first)",
        DirectorySortMode.SizeSmallest => "Size (smallest first)",
        DirectorySortMode.ExtensionThenName => "Type, then name",
        _ => "Name (natural)"
    };

    public static string ShortLabel(DirectorySortMode mode) => mode switch
    {
        DirectorySortMode.NaturalName => "Sort: Name",
        DirectorySortMode.NameDescending => "Sort: Z to A",
        DirectorySortMode.ModifiedNewest => "Sort: Newest",
        DirectorySortMode.ModifiedOldest => "Sort: Oldest",
        DirectorySortMode.CreatedNewest => "Sort: Created",
        DirectorySortMode.CreatedOldest => "Sort: Created old",
        DirectorySortMode.SizeLargest => "Sort: Largest",
        DirectorySortMode.SizeSmallest => "Sort: Smallest",
        DirectorySortMode.ExtensionThenName => "Sort: Type",
        _ => "Sort: Name"
    };

    public static bool TryParseCommandParameter(object? value, out DirectorySortMode mode)
    {
        if (value is DirectorySortMode typed)
        {
            mode = typed;
            return true;
        }

        if (value is string text && Enum.TryParse(text, ignoreCase: true, out DirectorySortMode parsed))
        {
            mode = parsed;
            return true;
        }

        mode = DirectorySortMode.NaturalName;
        return false;
    }
}
