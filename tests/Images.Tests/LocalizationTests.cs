using System.Globalization;
using System.IO;
using System.Reflection;
using System.Resources;
using ImageMagick;
using Images.Localization;
using Images.Services;
using Images.ViewModels;

namespace Images.Tests;

[CollectionDefinition("LocalizationCulture", DisableParallelization = true)]
public sealed class LocalizationCultureCollection;

[Collection("LocalizationCulture")]
public sealed class LocalizationTests
{
    [Fact]
    public void Strings_ReturnsDefaultEnglishResource()
    {
        Assert.Equal("Confirm action", Strings.ConfirmAction);
        Assert.Equal("Cancel", Strings.Cancel);
    }

    [Fact]
    public void Strings_UnknownKeyReturnsVisibleMissingMarker()
    {
        Assert.Equal("!Missing.Localization.Key!", Strings.Get("Missing.Localization.Key"));
    }

    [Fact]
    public void LocExtension_ReturnsResourceValue()
    {
        var extension = new LocExtension("ConfirmAction");

        Assert.Equal("Confirm action", extension.ProvideValue(serviceProvider: null!));
    }

    [Fact]
    public void PseudoLocaleResource_ExpandsTextAndPreservesPlaceholders()
    {
        var manager = GetResourceManager();
        var culture = CultureInfo.GetCultureInfo("qps-ploc");

        var cancel = manager.GetString(nameof(Strings.Cancel), culture);
        Assert.NotNull(cancel);
        Assert.StartsWith("[!!", cancel, StringComparison.Ordinal);
        Assert.Contains("Cancel", cancel, StringComparison.Ordinal);
        Assert.True(cancel!.Length > Strings.Cancel.Length);

        var baseFormat = manager.GetString("ModelManagerStorageOpenFailedFormat", CultureInfo.InvariantCulture);
        var pseudoFormat = manager.GetString("ModelManagerStorageOpenFailedFormat", culture);
        Assert.NotNull(baseFormat);
        Assert.NotNull(pseudoFormat);
        Assert.Contains("{0}", pseudoFormat, StringComparison.Ordinal);
        Assert.Equal(FormatPlaceholders(baseFormat!), FormatPlaceholders(pseudoFormat!));
    }

    [Fact]
    public void PseudoLocale_LocalizesServiceGeneratedStatesAndMetadata()
    {
        var previousCulture = Strings.Culture;
        try
        {
            Strings.Culture = CultureInfo.GetCultureInfo("qps-ploc");

            AssertPseudo(PhotoMetadataController.LoadingStatusText);
            AssertPseudo(PhotoMetadataController.EmptyStatusText);
            AssertPseudo(PhotoMetadataController.TimeoutStatusText);
            AssertPseudo(ColorAnalysisController.LoadingStatusText);
            AssertPseudo(Strings.ColorAnalysisUnavailable);
            AssertPseudo(C2paInspectionController.LoadingStatusText);
            AssertPseudo(Strings.C2paTrustSigned);
            AssertPseudo(Strings.C2paInspectionFailed);
            AssertPseudo(OcrCapabilityService.BuildOverviewText(new OcrCapabilityService.OcrCapabilityStatus(
                IsAvailable: true,
                LanguageCount: 1,
                LanguageSummary: "English (en-US)",
                StatusTitle: Strings.OcrCapabilityReadyTitle,
                StatusDetail: Strings.OcrCapabilityReadyDetail,
                BadgeText: Strings.OcrCapabilityReadyBadge)));

            using var temp = TestDirectory.Create();
            var metadataPath = Path.Combine(temp.Path, "localized-metadata.jpg");
            using (var image = new MagickImage(MagickColors.Blue, 8, 8) { Format = MagickFormat.Jpeg })
            {
                var exif = new ExifProfile();
                exif.SetValue(ExifTag.Make, "Test Camera");
                image.SetProfile(exif);
                image.Write(metadataPath);
            }

            var metadata = ImageMetadataService.Read(metadataPath);
            Assert.NotEmpty(metadata.Rows);
            Assert.All(metadata.Rows, row => AssertPseudo(row.Label));

            var exif31Path = Path.Combine(temp.Path, "localized-exif31.jpg");
            Exif31TestFixture.WriteJpeg(
                exif31Path,
                littleEndian: true,
                "0300",
                "Test Camera",
                Exif31TestFixture.Undefined(0x9287, littleEndian: true, 1, 0, 2),
                Exif31TestFixture.Short(0xa40d, littleEndian: true, 0x0101),
                Exif31TestFixture.Utf8(0xa40e, "Localized development"),
                Exif31TestFixture.Short(0xa40f, littleEndian: true, 1),
                Exif31TestFixture.Short(0xa412, littleEndian: true, 2));
            var exif31Metadata = ImageMetadataService.Read(exif31Path);
            Assert.True(exif31Metadata.Rows.Count >= 5);
            Assert.All(exif31Metadata.Rows, row =>
            {
                AssertPseudo(row.Label);
                if (row.Label != Strings.MetadataDevelopmentDescription &&
                    row.Label != Strings.MetadataCamera)
                    AssertPseudo(row.Value);
            });

            var archivePath = Path.Combine(temp.Path, "localized.cbz");
            File.WriteAllBytes(archivePath, []);
            AssertPseudo(ImageColorAnalysisService.Read(archivePath).StatusText);

            var diagnostics = DiagnosticsStatusService.BuildStatusItems(
                ReadyProvenance(),
                new OcrCapabilityService.OcrCapabilityStatus(
                    IsAvailable: true,
                    LanguageCount: 1,
                    LanguageSummary: "English (en-US)",
                    StatusTitle: Strings.OcrCapabilityReadyTitle,
                    StatusDetail: Strings.OcrCapabilityReadyDetail,
                    BadgeText: Strings.OcrCapabilityReadyBadge),
                updateChecksEnabled: false,
                lastUpdateCheckUtc: null,
                appDataRoot: null,
                logsPath: null,
                thumbnailsPath: null,
                crashLogPath: "crash.log");
            Assert.NotEmpty(diagnostics);
            Assert.All(diagnostics, item => AssertPseudo(item.Title));

            var models = new ModelManagerService(getModelRoot: () => null).GetSnapshot();
            AssertPseudo(models.RegistrySummary);
            Assert.All(models.Models, model =>
            {
                AssertPseudo(model.Purpose);
                AssertPseudo(model.StatusText);
                AssertPseudo(model.ActionText);
            });

            NetworkEgressService.Clear();
            AssertPseudo(NetworkEgressService.BuildClipboardText());
        }
        finally
        {
            Strings.Culture = previousCulture;
        }
    }

    private static ResourceManager GetResourceManager()
    {
        var field = typeof(Strings).GetField("ResourceManager", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(field);
        return Assert.IsType<ResourceManager>(field.GetValue(null));
    }

    private static CodecCapabilityService.RuntimeProvenance ReadyProvenance()
        => new(
            AppVersion: "Images test",
            Runtime: ".NET test",
            OperatingSystem: "Windows",
            ProcessArchitecture: "X64",
            AppDirectory: @"C:\Images",
            MagickVersion: "14.15.0",
            MagickNetRuntimeVersion: "Magick.NET 14.15.0",
            ImageMagickVersion: "ImageMagick 7.1.2-27",
            SqliteRuntimeVersion: "3.53.3",
            MagickAssemblyPath: @"C:\Images\Magick.NET.dll",
            MagickPolicy: MagickSecurityPolicy.Configure(true, "test"),
            SharpCompressVersion: "0.49.1",
            SharpCompressAssemblyPath: @"C:\Images\SharpCompress.dll",
            GhostscriptAvailable: true,
            GhostscriptDirectory: @"C:\Ghostscript",
            GhostscriptSource: "test",
            GhostscriptVersion: "10.06.0",
            GhostscriptDllPath: @"C:\Ghostscript\gsdll64.dll",
            GhostscriptDllSha256: "sha256:test",
            JpegTranAvailable: true,
            JpegTranExecutablePath: @"C:\Images\jpegtran.exe",
            JpegTranSource: "test",
            JpegTranVersion: "test",
            JpegTranSha256: "sha256:test",
            JpegTranStatus: "ready",
            C2paToolAvailable: true,
            C2paToolExecutablePath: @"C:\Images\c2patool.exe",
            C2paToolSource: "test",
            C2paToolVersion: "test",
            C2paToolSha256: "sha256:test",
            C2paToolStatus: "ready");

    private static IReadOnlyList<string> FormatPlaceholders(string value) =>
        System.Text.RegularExpressions.Regex
            .Matches(value, @"\{[0-9]+(?:[^{}]*)\}")
            .Select(match => match.Value)
            .ToArray();

    private static void AssertPseudo(string value)
        => Assert.StartsWith("[!!", value, StringComparison.Ordinal);
}
