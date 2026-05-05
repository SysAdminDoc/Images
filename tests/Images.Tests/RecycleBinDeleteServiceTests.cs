using System.IO;
using Images.Services;

namespace Images.Tests;

public sealed class RecycleBinDeleteServiceTests
{
    [Fact]
    public void Delete_WhenConfirmationDisabled_SendsWithoutShowingDialog()
    {
        using var temp = TestDirectory.Create();
        var image = temp.WriteFile("photo.jpg");
        var settings = CreateSettings(temp);
        settings.SetBool(Keys.ConfirmRecycleBinDelete, false);
        var deleted = "";
        var service = new RecycleBinDeleteService(
            settings,
            sendToRecycleBin: path => deleted = path,
            confirmRecycleBinMove: (_, _) => throw new InvalidOperationException("Confirmation should be skipped."));

        var result = service.Delete(image);

        Assert.Equal(RecycleBinDeleteStatus.Deleted, result.Status);
        Assert.Equal("photo.jpg", result.FileName);
        Assert.Equal(image, deleted);
    }

    [Fact]
    public void Delete_WhenUserCancels_DoesNotSendToRecycleBin()
    {
        using var temp = TestDirectory.Create();
        var image = temp.WriteFile("photo.jpg");
        var sent = false;
        var service = new RecycleBinDeleteService(
            CreateSettings(temp),
            sendToRecycleBin: _ => sent = true,
            confirmRecycleBinMove: (_, _) => new ConfirmDialog.ConfirmationResult(Confirmed: false, DoNotAskAgain: false));

        var result = service.Delete(image);

        Assert.Equal(RecycleBinDeleteStatus.Canceled, result.Status);
        Assert.False(sent);
    }

    [Fact]
    public void Delete_WhenUserChecksDoNotAskAgain_PersistsPreference()
    {
        using var temp = TestDirectory.Create();
        var image = temp.WriteFile("photo.jpg");
        var settings = CreateSettings(temp);
        var service = new RecycleBinDeleteService(
            settings,
            sendToRecycleBin: _ => { },
            confirmRecycleBinMove: (_, _) => new ConfirmDialog.ConfirmationResult(Confirmed: true, DoNotAskAgain: true));

        var result = service.Delete(image);

        Assert.Equal(RecycleBinDeleteStatus.Deleted, result.Status);
        Assert.False(settings.GetBool(Keys.ConfirmRecycleBinDelete, true));
    }

    [Fact]
    public void Delete_WhenRecycleBinSendFails_ReturnsFailure()
    {
        using var temp = TestDirectory.Create();
        var image = temp.WriteFile("photo.jpg");
        var service = new RecycleBinDeleteService(
            CreateSettings(temp),
            sendToRecycleBin: _ => throw new IOException("locked"),
            confirmRecycleBinMove: (_, _) => new ConfirmDialog.ConfirmationResult(Confirmed: true, DoNotAskAgain: false));

        var result = service.Delete(image);

        Assert.Equal(RecycleBinDeleteStatus.Failed, result.Status);
        Assert.Equal("locked", result.ErrorMessage);
    }

    [Fact]
    public void Delete_WhenPathIsMissing_ReturnsMissing()
    {
        using var temp = TestDirectory.Create();
        var service = new RecycleBinDeleteService(CreateSettings(temp));

        var result = service.Delete(Path.Combine(temp.Path, "missing.jpg"));

        Assert.Equal(RecycleBinDeleteStatus.Missing, result.Status);
    }

    private static SettingsService CreateSettings(TestDirectory temp)
        => new(Path.Combine(temp.Path, "settings.db"));
}
