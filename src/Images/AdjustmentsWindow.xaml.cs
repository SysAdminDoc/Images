using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Images.Services;

namespace Images;

public partial class AdjustmentsWindow : Window
{
    private readonly string _imagePath;
    private readonly Action<ImageAdjustmentPlan> _apply;
    private readonly DispatcherTimer _previewTimer;
    private int _previewVersion;
    private bool _isReady;

    public AdjustmentsWindow(string imagePath, Action<ImageAdjustmentPlan> apply)
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

        ResetSliders();
        _isReady = true;
        SchedulePreview(immediate: true);
    }

    private void AdjustmentSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isReady)
            return;

        UpdateText();
        SchedulePreview();
    }

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
            ? "No adjustment is active."
            : plan.Summary;

        if (!File.Exists(_imagePath))
        {
            StatusText.Text = "Preview unavailable: image file is no longer available.";
            PreviewImage.Source = null;
            return;
        }

        try
        {
            StatusText.Text = "Updating preview...";
            var preview = await Task.Run(() => ImageAdjustmentService.CreatePreview(_imagePath, plan));
            if (version != _previewVersion)
                return;

            PreviewImage.Source = preview;
            StatusText.Text = plan.IsIdentity
                ? "Move a slider to preview an adjustment."
                : "Preview ready. Press Enter to apply.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or NotSupportedException or ImageMagick.MagickException)
        {
            if (version != _previewVersion)
                return;

            PreviewImage.Source = null;
            StatusText.Text = "Preview failed: " + ex.Message;
        }
    }

    private void ApplyButton_Click(object sender, RoutedEventArgs e) => ApplyAndClose();

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        ResetSliders();
        UpdateText();
        SchedulePreview();
    }

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
            StatusText.Text = "Move a slider before applying.";
            return;
        }

        _apply(plan);
        Close();
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
        StatusText.Text = "Preview queued...";
        _previewTimer.Start();
    }

    private void ResetSliders()
    {
        var plan = ImageAdjustmentPlan.Default;
        BlackPointSlider.Value = plan.BlackPoint;
        WhitePointSlider.Value = plan.WhitePoint;
        GammaSlider.Value = plan.Gamma;
        CurveSlider.Value = plan.Curve;
        HueSlider.Value = plan.Hue;
        SaturationSlider.Value = plan.Saturation;
        LightnessSlider.Value = plan.Lightness;
        UpdateText();
    }

    private ImageAdjustmentPlan CurrentPlan()
        => ImageAdjustmentService.Normalize(new ImageAdjustmentPlan(
            BlackPointSlider.Value,
            WhitePointSlider.Value,
            GammaSlider.Value,
            CurveSlider.Value,
            HueSlider.Value,
            SaturationSlider.Value,
            LightnessSlider.Value));

    private void UpdateText()
    {
        BlackPointText.Text = Percent(BlackPointSlider.Value);
        WhitePointText.Text = Percent(WhitePointSlider.Value);
        GammaText.Text = GammaSlider.Value.ToString("0.00", CultureInfo.InvariantCulture);
        CurveText.Text = Signed(CurveSlider.Value);
        HueText.Text = Signed(HueSlider.Value) + " deg";
        SaturationText.Text = Percent(SaturationSlider.Value);
        LightnessText.Text = Percent(LightnessSlider.Value);
    }

    private static string Percent(double value)
        => value.ToString("0.#", CultureInfo.InvariantCulture) + "%";

    private static string Signed(double value)
        => value.ToString("+0.#;-0.#;0", CultureInfo.InvariantCulture);
}
