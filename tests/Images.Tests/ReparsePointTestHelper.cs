using System.IO;

namespace Images.Tests;

internal static class ReparsePointTestHelper
{
    public static void CreateDirectoryLinkOrSkip(string linkPath, string targetPath)
    {
        try
        {
            Directory.CreateSymbolicLink(linkPath, targetPath);
            var attributes = File.GetAttributes(linkPath);
            if ((attributes & FileAttributes.ReparsePoint) == 0)
                Assert.Skip("Directory symbolic links are not exposed as reparse points on this machine.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException or NotSupportedException)
        {
            Assert.Skip($"Directory symbolic links are not available on this machine: {ex.Message}");
        }
    }
}
