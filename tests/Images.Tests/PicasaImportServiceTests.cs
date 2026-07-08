using System.IO;
using System.Xml.Linq;
using Images.Services;

namespace Images.Tests;

public sealed class PicasaImportServiceTests
{
    [Fact]
    public void ImportFolder_ConvertsRatingsAlbumsFacesAndContactNamesToXmpSidecars()
    {
        using var temp = TestDirectory.Create();
        var photo = temp.WriteFile("photo1.jpg", "original image bytes");
        var second = temp.WriteFile("photo2.png", "second image bytes");
        var ini = temp.WriteFile(".picasa.ini", """
        [photo1.jpg]
        star=yes
        albums=Trip, Family
        faces=rect64(1000200050006000),abc123;rect64(7000100090003000),missing

        [photo2.png]
        rating=3
        album=Archive
        """);
        var contacts = temp.WriteFile("contacts.xml", """
        <contacts>
          <contact id="abc123" name="Ada Lovelace" />
        </contacts>
        """);

        var result = new PicasaImportService().ImportFolder(temp.Path, contacts);
        var sidecar = XDocument.Load(photo + ".xmp");
        var imported = new XmpSidecarImportService().ImportForImage(photo);

        Assert.True(result.Success, result.Message);
        Assert.Equal(2, result.SidecarsWritten);
        Assert.Equal(2, result.RatingsWritten);
        Assert.Equal(3, result.AlbumsWritten);
        Assert.Equal(2, result.FacesWritten);
        Assert.Equal(1, result.ContactsResolved);
        Assert.Equal("original image bytes", File.ReadAllText(photo));
        Assert.Equal("second image bytes", File.ReadAllText(second));

        Assert.True(imported.Success, imported.Message);
        Assert.Equal(5, imported.Rating);
        Assert.Contains("album:Trip", imported.FlatKeywords);
        Assert.Contains("album:Family", imported.FlatKeywords);
        Assert.Contains("person:Ada Lovelace", imported.FlatKeywords);
        Assert.Contains("Picasa|Albums|Trip", imported.HierarchicalKeywords);
        Assert.Contains("Picasa|People|Ada Lovelace", imported.HierarchicalKeywords);

        var xml = sidecar.ToString();
        Assert.Contains("mwg-rs:Regions", xml, StringComparison.Ordinal);
        Assert.Contains("Ada Lovelace", xml, StringComparison.Ordinal);
        Assert.Contains("missing", xml, StringComparison.Ordinal);
        Assert.Contains("stArea:unit=\"normalized\"", xml, StringComparison.Ordinal);
        Assert.Contains("stArea:x=\"0.187503\"", xml, StringComparison.Ordinal);
    }

    [Fact]
    public void ImportFolder_SkipsStructuralSectionsAndResolvesAlbumNames()
    {
        using var temp = TestDirectory.Create();
        var photo = temp.WriteFile("photo.jpg", "image bytes");
        temp.WriteFile(".picasa.ini", """
        [Picasa]
        name=My Folder

        [encoding]
        1=UTF-8

        [.album:9a7c]
        name=Summer Trip
        token=9a7c

        [Contacts2]
        abc123=Ada Lovelace;;

        [photo.jpg]
        star=yes
        albums=9a7c
        """);

        var result = new PicasaImportService().ImportFolder(temp.Path);
        var imported = new XmpSidecarImportService().ImportForImage(photo);

        Assert.True(result.Success, result.Message);
        // Only the real image row is imported; structural sections are not
        // reported as missing images.
        Assert.Equal(0, result.MissingImages);
        Assert.Equal(1, result.SidecarsWritten);
        // The album ID resolves to its display name, not the raw ID.
        Assert.Contains("album:Summer Trip", imported.FlatKeywords);
        Assert.DoesNotContain(imported.FlatKeywords, k => k.Contains("9a7c", StringComparison.Ordinal));
    }

    [Fact]
    public void ImportFolder_MissingIni_ReturnsFailureWithoutWritingSidecar()
    {
        using var temp = TestDirectory.Create();
        var photo = temp.WriteFile("photo.jpg", "original image bytes");

        var result = new PicasaImportService().ImportFolder(temp.Path);

        Assert.False(result.Success);
        Assert.Contains(".picasa.ini", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(photo + ".xmp"));
        Assert.Equal("original image bytes", File.ReadAllText(photo));
    }

    [Fact]
    public void ImportFolder_SkipsEntriesOutsideSelectedFolder()
    {
        using var parent = TestDirectory.Create();
        var folder = Directory.CreateDirectory(Path.Combine(parent.Path, "album")).FullName;
        var outside = parent.WriteFile("outside.jpg", "outside image bytes");
        File.WriteAllText(Path.Combine(folder, ".picasa.ini"), """
        [..\outside.jpg]
        star=yes
        """);

        var result = new PicasaImportService().ImportFolder(folder);

        Assert.False(result.Success);
        Assert.Equal(1, result.MissingImages);
        Assert.False(File.Exists(outside + ".xmp"));
        Assert.Equal("outside image bytes", File.ReadAllText(outside));
    }

    [Fact]
    public void ParseIni_HandlesCommentsAndCaseInsensitiveKeys()
    {
        using var temp = TestDirectory.Create();
        var ini = temp.WriteFile(".picasa.ini", """
        # comment
        [Photo.JPG]
        Star=YES
        Albums=One|Two
        ; another comment
        faces=rect64(00000000ffffffff),face1
        """);

        var entries = PicasaImportService.ParseIni(ini);
        var faces = PicasaImportService.ReadFaces(
            entries[0].Values,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["face1"] = "Face One" });

        Assert.Single(entries);
        Assert.Equal("Photo.JPG", entries[0].FileName);
        Assert.Equal("YES", entries[0].Values["star"]);
        Assert.Single(faces);
        Assert.Equal("Face One", faces[0].Name);
        Assert.Equal(0.5, faces[0].X, precision: 3);
        Assert.Equal(1, faces[0].Width, precision: 3);
    }

    [Fact]
    public void ReadFaces_AcceptsRect64WithOmittedLeadingZeros()
    {
        var faces = PicasaImportService.ReadFaces(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["faces"] = "rect64(ffffffff),face1"
            },
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["face1"] = "Face One" });

        var face = Assert.Single(faces);
        Assert.Equal("Face One", face.Name);
        Assert.Equal(0.5, face.X, precision: 3);
        Assert.Equal(0.5, face.Y, precision: 3);
        Assert.Equal(1, face.Width, precision: 3);
        Assert.Equal(1, face.Height, precision: 3);
    }
}
