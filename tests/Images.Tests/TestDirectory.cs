using System.IO;

namespace Images.Tests;

internal sealed class TestDirectory : IDisposable
{
    private TestDirectory(string path) => Path = path;

    public string Path { get; }

    public static TestDirectory Create()
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Images.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return new TestDirectory(path);
    }

    public string WriteFile(string fileName, string contents = "test")
    {
        var path = System.IO.Path.Combine(Path, fileName);
        File.WriteAllText(path, contents);
        return path;
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
        catch
        {
            // Test cleanup is best-effort; failed deletes land under %TEMP%\Images.Tests.
        }
    }
}
