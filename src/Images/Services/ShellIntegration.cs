using System.Diagnostics;
using System.IO;

namespace Images.Services;

/// <summary>
/// Centralized shell launch helpers for files, URLs, settings URIs, and Explorer.
/// Callers own user-facing error text; this service owns process construction details.
/// </summary>
public static class ShellIntegration
{
    public static void OpenShellTarget(string target)
    {
        if (string.IsNullOrWhiteSpace(target))
            throw new ArgumentException("A shell target is required.", nameof(target));

        Process.Start(new ProcessStartInfo
        {
            FileName = target,
            UseShellExecute = true,
        });
    }

    public static void OpenFolder(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
            throw new ArgumentException("A folder path is required.", nameof(folderPath));

        var fullPath = Path.GetFullPath(folderPath);
        if (!Directory.Exists(fullPath))
            throw new DirectoryNotFoundException($"Folder not found: {fullPath}");

        var psi = CreateExplorerStartInfo();
        psi.ArgumentList.Add(fullPath);
        Process.Start(psi);
    }

    public static void RevealPathInExplorer(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("A path is required.", nameof(path));

        var fullPath = Path.GetFullPath(path);
        if (File.Exists(fullPath) || Directory.Exists(fullPath))
        {
            SelectPath(fullPath);
            return;
        }

        var parent = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(parent) && Directory.Exists(parent))
        {
            OpenFolder(parent);
            return;
        }

        throw new FileNotFoundException("Path not found.", fullPath);
    }

    private static void SelectPath(string fullPath)
    {
        var psi = CreateExplorerStartInfo();
        psi.ArgumentList.Add("/select," + fullPath);
        Process.Start(psi);
    }

    private static ProcessStartInfo CreateExplorerStartInfo()
        => new()
        {
            FileName = "explorer.exe",
            UseShellExecute = false,
        };
}
