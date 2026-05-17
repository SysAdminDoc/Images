using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Images.Services;
using Microsoft.Win32;

namespace Images;

public partial class ExportPreviewWindow : Window
{
    private readonly ExportPreviewService _previewService = new();
    private readonly BitmapSource _source;
    private readonly string? _sourcePath;
    private readonly ObservableCollection<string> _warnings = [];
    private bool _loadingPreset;
    private bool _hasPreview;

    public ExportPreviewWindow(BitmapSource source, string? sourcePath, string initialExtension)
    {
        InitializeComponent();

        _source = source ?? throw new ArgumentNullException(nameof(source));
        _sourcePath = string.IsNullOrWhiteSpace(sourcePath) ? null : sourcePath;

        OriginalImage.Source = _source;
        WarningsList.ItemsSource = _warnings;
        PresetCombo.ItemsSource = ExportPreviewPreset.Defaults;
        PresetCombo.DisplayMemberPath = nameof(ExportPreviewPreset.Name);
        PresetCombo.SelectedItem = PickInitialPreset(initialExtension);
        ApplyPreset(PresetCombo.SelectedItem as ExportPreviewPreset ?? ExportPreviewPreset.Defaults[0]);
        SourceText.Text = _sourcePath is null
            ? "Preview compression settings against the displayed bitmap before writing a copy."
            : $"Preview compression settings for {Path.GetFileName(_sourcePath)} before writing a copy.";

        SourceInitialized += (_, _) =>
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            WindowChrome.ApplyDarkCaption(hwnd);
        };

        Loaded += async (_, _) => await BuildPreviewAsync();
    }

    private ExportPreviewPreset PickInitialPreset(string initialExtension)
    {
        var normalized = RenameService.NormalizeExtension(initialExtension);
        return ExportPreviewPreset.Defaults.FirstOrDefault(
                   preset => preset.Extension.Equals(normalized, StringComparison.OrdinalIgnoreCase))
               ?? ExportPreviewPreset.Defaults[0];
    }

    private async void PreviewButton_Click(object sender, RoutedEventArgs e)
        => await BuildPreviewAsync();

    private async void SaveCopyButton_Click(object sender, RoutedEventArgs e)
    {
        var request = ReadRequest();
        if (!_hasPreview)
            await BuildPreviewAsync();
        if (!_hasPreview)
            return;

        var stem = _sourcePath is null
            ? "image"
            : Path.GetFileNameWithoutExtension(_sourcePath);
        var dialog = new SaveFileDialog
        {
            Title = "Save previewed export copy",
            FileName = stem + "_export" + request.Extension,
            Filter = ImageExportService.ExportFilter,
            DefaultExt = request.Extension.TrimStart('.'),
            AddExtension = true,
            InitialDirectory = _sourcePath is null ? null : Path.GetDirectoryName(_sourcePath)
        };

        if (dialog.ShowDialog(this) != true)
            return;

        try
        {
            var targetPath = ImageExportService.ResolveWritablePath(dialog.FileName);
            if (_sourcePath is not null && PathsReferToSameFile(targetPath, _sourcePath))
            {
                SetStatus("Choose a different filename for the exported copy.", ExportPreviewStatus.Warning);
                return;
            }

            SetBusy(true);
            SetStatus($"Saving {Path.GetFileName(targetPath)}...", ExportPreviewStatus.Busy);
            var savedPath = await Task.Run(() => ImageExportService.Save(
                _source,
                targetPath,
                (uint)request.Quality,
                request.MaxWidth,
                request.MaxHeight));
            SetStatus($"Saved copy to {savedPath}.", ExportPreviewStatus.Ready);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or NotSupportedException or ImageMagick.MagickException)
        {
            SetStatus($"Save failed: {ex.Message}", ExportPreviewStatus.Error);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void PresetCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingPreset)
            return;

        if (PresetCombo.SelectedItem is ExportPreviewPreset preset)
            ApplyPreset(preset);
    }

    private void QualitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (QualityText is not null)
            QualityText.Text = ((int)Math.Round(e.NewValue)).ToString(CultureInfo.InvariantCulture);
        SettingsChanged(sender, e);
    }

    private void SettingsChanged(object sender, RoutedEventArgs e)
    {
        if (_loadingPreset)
            return;

        _hasPreview = false;
        SetStatus("Settings changed. Build a new preview before saving.", ExportPreviewStatus.Warning);
    }

    private void ApplyPreset(ExportPreviewPreset preset)
    {
        _loadingPreset = true;
        var request = ExportPreviewService.FromPreset(preset);
        ExtensionBox.Text = request.Extension;
        QualitySlider.Value = request.Quality;
        QualityText.Text = request.Quality.ToString(CultureInfo.InvariantCulture);
        MaxWidthBox.Text = request.MaxWidth.ToString(CultureInfo.InvariantCulture);
        MaxHeightBox.Text = request.MaxHeight.ToString(CultureInfo.InvariantCulture);
        _loadingPreset = false;
        _hasPreview = false;
        SetStatus("Preset ready. Build a preview to inspect pixels, size, and warnings.", ExportPreviewStatus.Ready);
    }

    private async Task BuildPreviewAsync()
    {
        SetBusy(true);
        SetStatus("Encoding preview...", ExportPreviewStatus.Busy);
        try
        {
            var request = ReadRequest();
            var result = await Task.Run(() => _previewService.BuildPreview(_source, _sourcePath, request));
            PreviewImage.Source = result.PreviewImage;
            ApplySummary(result.Summary);
            _hasPreview = true;
            SetStatus("Preview ready.", ExportPreviewStatus.Ready);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or NotSupportedException or ImageMagick.MagickException)
        {
            _hasPreview = false;
            SetStatus($"Preview failed: {ex.Message}", ExportPreviewStatus.Error);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private ExportPreviewRequest ReadRequest()
    {
        var request = ExportPreviewService.NormalizeRequest(new ExportPreviewRequest(
            ExtensionBox.Text,
            (int)Math.Round(QualitySlider.Value),
            Math.Max(0, ParseInt(MaxWidthBox.Text)),
            Math.Max(0, ParseInt(MaxHeightBox.Text))));

        if (!ExtensionBox.Text.Equals(request.Extension, StringComparison.OrdinalIgnoreCase))
            ExtensionBox.Text = request.Extension;

        return request;
    }

    private void ApplySummary(ExportPreviewSummary summary)
    {
        SourceSizeText.Text = summary.SourceSizeText;
        OutputSizeText.Text = summary.EstimatedSizeText;
        DeltaText.Text = summary.DeltaText;
        DimensionsText.Text = summary.DimensionsText;
        FormatText.Text = $"{summary.FormatText} quality {summary.QualityText}";

        _warnings.Clear();
        foreach (var warning in summary.Warnings)
            _warnings.Add(warning);
        if (_warnings.Count == 0)
            _warnings.Add("No format warnings.");
    }

    private void SetBusy(bool busy)
    {
        PreviewButton.IsEnabled = !busy;
        SaveCopyButton.IsEnabled = !busy;
        PresetCombo.IsEnabled = !busy;
        ExtensionBox.IsEnabled = !busy;
        QualitySlider.IsEnabled = !busy;
        MaxWidthBox.IsEnabled = !busy;
        MaxHeightBox.IsEnabled = !busy;
    }

    private void SetStatus(string message, ExportPreviewStatus status)
    {
        StatusText.Text = message;
        StatusDot.Fill = status switch
        {
            ExportPreviewStatus.Busy => Brush("AccentBrush"),
            ExportPreviewStatus.Warning => Brush("YellowBrush"),
            ExportPreviewStatus.Error => Brush("RedBrush"),
            _ => Brush("GreenBrush")
        };
    }

    private Brush Brush(string key)
        => TryFindResource(key) as Brush ?? Brushes.Transparent;

    private static int ParseInt(string value)
        => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;

    private static bool PathsReferToSameFile(string left, string right)
    {
        try
        {
            return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }
    }

    private enum ExportPreviewStatus
    {
        Ready,
        Busy,
        Warning,
        Error
    }
}
