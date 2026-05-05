using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Images.Services;

namespace Images;

public partial class ResizeDialogWindow : Window
{
    private readonly int _sourceWidth;
    private readonly int _sourceHeight;
    private bool _isReady;

    public ResizePlan? Result { get; private set; }

    public ResizeDialogWindow(int sourceWidth, int sourceHeight)
    {
        _sourceWidth = sourceWidth;
        _sourceHeight = sourceHeight;

        InitializeComponent();

        SourceInitialized += (_, _) =>
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            WindowChrome.ApplyDarkCaption(hwnd);
        };

        SourceText.Text = $"Source: {_sourceWidth} x {_sourceHeight} px";
        ModeCombo.ItemsSource = ResizePlanService.ModeOptions;
        FilterCombo.ItemsSource = ResizePlanService.FilterPresets;
        ModeCombo.SelectedItem = ResizePlanService.ModeOptions[0];
        FilterCombo.SelectedItem = ResizePlanService.Lanczos3Filter;
        PercentBox.Text = "50";
        WidthBox.Text = _sourceWidth.ToString(CultureInfo.InvariantCulture);
        HeightBox.Text = _sourceHeight.ToString(CultureInfo.InvariantCulture);
        EdgeBox.Text = Math.Max(_sourceWidth, _sourceHeight).ToString(CultureInfo.InvariantCulture);

        _isReady = true;
        UpdatePreview();
    }

    private void InputChanged(object sender, RoutedEventArgs e) => UpdatePreview();

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        var plan = CreatePlan();
        if (!plan.IsValid)
        {
            UpdatePreview();
            return;
        }

        Result = plan;
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void UpdatePreview()
    {
        if (!_isReady)
            return;

        var mode = SelectedMode();
        PercentPanel.Visibility = mode == ResizeDimensionMode.Percent ? Visibility.Visible : Visibility.Collapsed;
        PixelPanel.Visibility = mode == ResizeDimensionMode.Pixels ? Visibility.Visible : Visibility.Collapsed;
        EdgePanel.Visibility = mode is ResizeDimensionMode.LongEdge or ResizeDimensionMode.ShortEdge
            ? Visibility.Visible
            : Visibility.Collapsed;
        EdgeLabel.Text = mode == ResizeDimensionMode.ShortEdge ? "Short edge" : "Long edge";
        AspectLockCheckBox.IsEnabled = mode == ResizeDimensionMode.Pixels;

        if (ModeCombo.SelectedItem is ResizeDimensionModeOption option)
            ModeDescriptionText.Text = option.Description;
        if (FilterCombo.SelectedItem is ResizeFilterPreset filter)
            FilterDescriptionText.Text = filter.Description;

        var plan = CreatePlan();
        PreviewText.Text = plan.Summary;
        StatusText.Text = plan.IsValid ? "Ready to add this resize to edit history." : plan.Error;
        AddButton.IsEnabled = plan.IsValid;
    }

    private ResizePlan CreatePlan()
        => ResizePlanService.CreatePlan(new ResizePlanRequest(
            _sourceWidth,
            _sourceHeight,
            SelectedMode(),
            ParseDouble(PercentBox.Text),
            ParseInt(WidthBox.Text),
            ParseInt(HeightBox.Text),
            ParseInt(EdgeBox.Text),
            SelectedMode() != ResizeDimensionMode.Pixels || AspectLockCheckBox.IsChecked == true,
            FilterCombo.SelectedItem as ResizeFilterPreset ?? ResizePlanService.Lanczos3Filter));

    private ResizeDimensionMode SelectedMode()
        => ModeCombo.SelectedItem is ResizeDimensionModeOption option ? option.Mode : ResizeDimensionMode.Percent;

    private static int ParseInt(string? text)
        => int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;

    private static double ParseDouble(string? text)
        => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0;
}
