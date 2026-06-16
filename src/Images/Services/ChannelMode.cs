namespace Images.Services;

/// <summary>
/// Selects which color channel to display in isolation.
/// When any mode other than <see cref="Normal"/> is active, the viewport renders a
/// grayscale representation of the selected channel.
/// </summary>
public enum ChannelMode
{
    Normal,
    Red,
    Green,
    Blue,
    Alpha
}
