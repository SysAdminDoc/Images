using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Images.Localization;
using Images.Services;

namespace Images;

public partial class EffectsWindow : Window
{
    private readonly string _imagePath;
    private readonly Action<ImageEffectsPlan> _apply;
    private readonly DispatcherTimer _previewTimer;
    private int _previewVersion;
    private bool _isReady;

    public EffectsWindow(string imagePath, Action<ImageEffectsPlan> apply)
    {
        _imagePath = imagePath;
        _apply = apply;
        _previewTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(160) };
        _previewTimer.Tick += PreviewTimer_Tick;

        InitializeComponent();

        SourceInitialized += (_, _) =>
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            WindowChrome.ApplyDarkCaption(hwnd);
        };

        Closed += (_, _) => _previewTimer.Stop();

        ResetSliders(schedulePreview: false);
        _isReady = true;
        SchedulePreview(immediate: true);
    }

    private void EffectSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isReady)
            return;

        UpdateText();
        SchedulePreview();
    }

    private void CrispButton_Click(object sender, RoutedEventArgs e)
        => SetPlan(new ImageEffectsPlan(45, 0, 0));

    private void CleanButton_Click(object sender, RoutedEventArgs e)
        => SetPlan(new ImageEffectsPlan(15, 45, 0));

    private void FocusButton_Click(object sender, RoutedEventArgs e)
        => SetPlan(new ImageEffectsPlan(35, 10, 35));

    private void PreviewTimer_Tick(object? sender, EventArgs e)
    {
        _previewTimer.Stop();
        _ = RefreshPreviewAsync();
    }

    private async Task RefreshPreviewAsync()
    {
        var plan = CurrentPlan();
        var version = ++_previewVersion;
        ApplyButton.IsEnabled = !plan.IsIdentity;
        SummaryText.Text = plan.IsIdentity
            ? Strings.EffectsNoEffectActive
            : plan.Summary;

        if (!File.Exists(_imagePath))
        {
            StatusText.Text = Strings.EffectsPreviewUnavailableMissingFile;
            PreviewImage.Source = null;
            return;
        }

        try
        {
            StatusText.Text = Strings.EffectsUpdatingPreview;
            var preview = await Task.Run(() => ImageEffectsService.CreatePreview(_imagePath, plan));
            if (version != _previewVersion)
                return;

            PreviewImage.Source = preview;
            StatusText.Text = plan.IsIdentity
                ? Strings.EffectsChoosePresetOrSlider
                : Strings.EffectsPreviewReadyEnter;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or NotSupportedException or ImageMagick.MagickException)
        {
            if (version != _previewVersion)
                return;

            PreviewImage.Source = null;
            StatusText.Text = Strings.Format("EffectsPreviewFailedFormat", ex.Message);
        }
    }

    private void ApplyButton_Click(object sender, RoutedEventArgs e) => ApplyAndClose();

    private void ResetButton_Click(object sender, RoutedEventArgs e)
        => ResetSliders(schedulePreview: true);

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ApplyAndClose();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    private void ApplyAndClose()
    {
        var plan = CurrentPlan();
        if (plan.IsIdentity)
        {
            StatusText.Text = Strings.EffectsChooseBeforeApplying;
            return;
        }

        _apply(plan);
        Close();
    }

    private void SetPlan(ImageEffectsPlan plan)
    {
        var normalized = ImageEffectsService.Normalize(plan);
        var wasReady = _isReady;
        _isReady = false;
        SharpenSlider.Value = normalized.Sharpen;
        NoiseSlider.Value = normalized.NoiseReduction;
        VignetteSlider.Value = normalized.Vignette;
        _isReady = wasReady;
        UpdateText();
        SchedulePreview();
    }

    private void SchedulePreview(bool immediate = false)
    {
        _previewTimer.Stop();
        if (immediate)
        {
            _ = RefreshPreviewAsync();
            return;
        }

        _previewVersion++;
        StatusText.Text = Strings.EffectsPreviewQueued;
        _previewTimer.Start();
    }

    private void ResetSliders(bool schedulePreview)
    {
        SetPlanValues(ImageEffectsPlan.Default);
        UpdateText();
        if (schedulePreview)
            SchedulePreview();
    }

    private void SetPlanValues(ImageEffectsPlan plan)
    {
        var wasReady = _isReady;
        _isReady = false;
        SharpenSlider.Value = plan.Sharpen;
        NoiseSlider.Value = plan.NoiseReduction;
        VignetteSlider.Value = plan.Vignette;
        _isReady = wasReady;
    }

    private ImageEffectsPlan CurrentPlan()
        => ImageEffectsService.Normalize(new ImageEffectsPlan(
            SharpenSlider.Value,
            NoiseSlider.Value,
            VignetteSlider.Value));

    private void UpdateText()
    {
        SharpenText.Text = Percent(SharpenSlider.Value);
        NoiseText.Text = Percent(NoiseSlider.Value);
        VignetteText.Text = Percent(VignetteSlider.Value);
    }

    private static string Percent(double value)
        => Strings.Format("EffectsPercentFormat", value);
}
