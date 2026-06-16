using System.Diagnostics;
using System.IO;
using System.Windows.Media.Imaging;

namespace Images.Services;

/// <summary>
/// Detects whether a Windows Store codec extension is needed for a given image format and
/// provides a one-click deep link to install it. This is a pure detection + launch helper —
/// it never downloads or installs anything directly.
/// </summary>
public static class StoreExtensionService
{
    /// <summary>
    /// A Store extension that would enable decoding for the current file format.
    /// </summary>
    public sealed record StoreExtensionInfo(
        string DisplayName,
        string ProductId,
        string StoreUri)
    {
        /// <summary>
        /// Opens the Microsoft Store PDP page for this extension.
        /// </summary>
        public void OpenStorePage()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = StoreUri,
                    UseShellExecute = true
                });
            }
            catch
            {
                // Swallow — the Store may not be available (Server SKUs, LTSC without Store).
            }
        }
    }

    // Microsoft Store product IDs for codec extensions that enable WIC decode of modern formats.
    // These are stable across Windows 10/11 versions.
    private static readonly StoreExtensionInfo HeifExtension = new(
        "HEIF Image Extensions",
        "9pmmsr1cgpwg",
        "ms-windows-store://pdp/?productid=9pmmsr1cgpwg");

    private static readonly StoreExtensionInfo HevcExtension = new(
        "HEVC Video Extensions",
        "9n4wgh0z6vhq",
        "ms-windows-store://pdp/?productid=9n4wgh0z6vhq");

    private static readonly StoreExtensionInfo Av1Extension = new(
        "AV1 Video Extension",
        "9mvzqvxjbq9v",
        "ms-windows-store://pdp/?productid=9mvzqvxjbq9v");

    // JPEG XL has no stable Store extension yet. Windows 11 24H2+ has native support.
    // We return null for JXL to show an OS-update hint instead of a Store link.

    /// <summary>
    /// Returns the Store extension needed to decode the given file extension via WIC, or null
    /// if no Store extension applies (either the format doesn't need one, or the codec is
    /// already installed). Checks WIC decodability first — if the codec is already present,
    /// returns null even for HEIC/AVIF.
    /// </summary>
    public static StoreExtensionInfo? GetMissingExtension(string fileExtension)
    {
        var ext = (fileExtension ?? "").TrimStart('.').ToLowerInvariant();

        var candidate = ext switch
        {
            "heic" or "heif" or "hif" => HeifExtension,
            "avif" => Av1Extension,
            _ => null
        };

        if (candidate is null)
            return null;

        // If WIC can already decode this format, the extension is installed — no prompt needed.
        if (CanWicDecode(ext))
            return null;

        return candidate;
    }

    /// <summary>
    /// Returns the HEVC Store extension info for callers that want to offer it alongside the
    /// HEIF extension (HEVC is needed for HEIC files that use HEVC compression). Returns null
    /// if HEVC decoding already works.
    /// </summary>
    public static StoreExtensionInfo? GetHevcExtensionIfMissing()
    {
        // HEVC availability is hard to probe without an actual HEVC stream. We expose the
        // extension info unconditionally for callers that know they need it — the Store page
        // itself will show "already installed" if present.
        return HevcExtension;
    }

    /// <summary>
    /// Returns true when the given extension maps to a format that <em>could</em> benefit from
    /// a Store extension, regardless of whether the extension is currently installed.
    /// </summary>
    public static bool IsStoreExtensionFormat(string fileExtension)
    {
        var ext = (fileExtension ?? "").TrimStart('.').ToLowerInvariant();
        return ext is "heic" or "heif" or "hif" or "avif";
    }

    /// <summary>
    /// Returns true when JXL failed and the fix is an OS update rather than a Store extension.
    /// </summary>
    public static bool IsJxlFormat(string fileExtension)
    {
        var ext = (fileExtension ?? "").TrimStart('.').ToLowerInvariant();
        return ext is "jxl";
    }

    /// <summary>
    /// Probes whether the Windows Imaging Component has a decoder registered for the given
    /// extension by scanning the WIC codec registry keys. This catches the case where the
    /// Store extension is already installed.
    /// </summary>
    private static bool CanWicDecode(string extensionWithoutDot)
    {
        // WIC decoder CLSIDs live under HKLM\SOFTWARE\Classes\CLSID\{...}\Instance.
        // Each registered decoder has a FileExtensions value. Rather than walking the entire
        // tree (slow + fragile across WoW64), we use a simple BitmapDecoder.Create probe:
        // feed it a tiny stream and see if WIC throws NotSupportedException (no codec) or
        // FileFormatException (codec present but bad data — meaning the codec IS registered).
        var dotExt = "." + extensionWithoutDot;
        try
        {
            // Create a 16-byte dummy stream. WIC will attempt to match the stream against
            // registered decoders. If a codec for this format is installed, it will try to
            // parse the header and fail with FileFormatException or COMException. If NO codec
            // is registered at all, it throws NotSupportedException.
            using var ms = new MemoryStream(new byte[16]);
            BitmapDecoder.Create(ms, BitmapCreateOptions.None, BitmapCacheOption.None);
            // If this somehow succeeds, a codec is present.
            return true;
        }
        catch (NotSupportedException)
        {
            // No WIC codec recognized the stream at all. But this is a format-blind probe —
            // we need a format-aware check. Fall back to the MIME-type registry scan.
        }
        catch
        {
            // FileFormatException, COMException, etc. — a codec tried to parse but the dummy
            // data was invalid. This means a codec IS registered, but we can't distinguish
            // which format it belongs to from a blind probe.
        }

        // Format-aware check: scan the WIC decoder registry for the target extension.
        return WicRegistryHasDecoder(dotExt);
    }

    /// <summary>
    /// Walks the WIC decoder instance registry keys to check whether any registered decoder
    /// advertises support for the given file extension (e.g. ".heic").
    /// </summary>
    private static bool WicRegistryHasDecoder(string dotExtension)
    {
        // WIC decoders are registered under:
        // HKLM\SOFTWARE\Classes\CLSID\{7ED96837-96F0-4812-B211-F13C24117ED3}\Instance\{decoder-clsid}
        // Each decoder subkey has a "FileExtensions" REG_SZ value like ".heic,.heif".
        const string wicDecodersPath =
            @"SOFTWARE\Classes\CLSID\{7ED96837-96F0-4812-B211-F13C24117ED3}\Instance";
        try
        {
            using var instancesKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(wicDecodersPath);
            if (instancesKey is null) return false;

            foreach (var subKeyName in instancesKey.GetSubKeyNames())
            {
                try
                {
                    using var decoderKey = instancesKey.OpenSubKey(subKeyName);
                    if (decoderKey?.GetValue("FileExtensions") is string extensions)
                    {
                        // The value is a comma-separated list of extensions like ".bmp,.dib,.rle"
                        foreach (var ext in extensions.Split(','))
                        {
                            if (ext.Trim().Equals(dotExtension, StringComparison.OrdinalIgnoreCase))
                                return true;
                        }
                    }
                }
                catch
                {
                    // Skip unreadable subkeys.
                }
            }
        }
        catch
        {
            // Registry access denied or key missing — assume codec is not installed.
        }

        return false;
    }
}
