using System.Windows.Media;

namespace Images.ViewModels;

public sealed class AnimationFrameItem : ObservableObject
{
    private bool _isSelected;

    public AnimationFrameItem(int index, ImageSource preview, string delayText, string timestampText)
    {
        Index = index;
        Preview = preview ?? throw new ArgumentNullException(nameof(preview));
        FrameText = $"Frame {index + 1}";
        DelayText = delayText;
        TimestampText = timestampText;
    }

    public int Index { get; }
    public ImageSource Preview { get; }
    public string FrameText { get; }
    public string DelayText { get; }
    public string TimestampText { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => Set(ref _isSelected, value);
    }
}
