using System.IO;
using System.Text.Json;
using Images.Services;

if (args.Length != 2 || args[0] is not ("image" or "archive"))
{
    Console.Error.WriteLine("Usage: Images.CodecProbe <image|archive> <path>");
    return 64;
}

var mode = args[0];
var path = Path.GetFullPath(args[1]);

try
{
    CodecRuntime.Configure();
    ProbeResult result;
    if (mode == "archive")
    {
        var names = ArchiveBookService.ListPageNames(path);
        if (names.Count > 0)
            _ = ArchiveBookService.LoadPage(path, 0);
        result = new ProbeResult("Decoded", null, names.Count, null, null);
    }
    else
    {
        var image = ImageLoader.Load(path);
        result = new ProbeResult(
            "Decoded",
            image.DecoderUsed,
            null,
            image.PixelWidth,
            image.PixelHeight);
    }

    Console.Out.Write(JsonSerializer.Serialize(result));
    return 0;
}
catch (Exception ex)
{
    var result = new ProbeResult("Rejected", ex.GetType().Name, null, null, null);
    Console.Out.Write(JsonSerializer.Serialize(result));
    return 0;
}

internal sealed record ProbeResult(
    string Classification,
    string? Detail,
    int? PageCount,
    int? Width,
    int? Height);
