using System.Threading;
using System.Windows.Controls;

namespace Images.Tests;

public sealed class MainWindowInputTests
{
    [Fact]
    public void IsTextEntryElement_DetectsEditableControls()
    {
        RunOnSta(() =>
        {
            Assert.True(MainWindow.IsTextEntryElement(new TextBox()));
            Assert.True(MainWindow.IsTextEntryElement(new PasswordBox()));
            Assert.True(MainWindow.IsTextEntryElement(new RichTextBox()));
            Assert.True(MainWindow.IsTextEntryElement(new ComboBox { IsEditable = true }));
            Assert.False(MainWindow.IsTextEntryElement(new ComboBox { IsEditable = false }));
            Assert.False(MainWindow.IsTextEntryElement(new Button()));
            Assert.False(MainWindow.IsTextEntryElement(null));
        });
    }

    private static void RunOnSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
            throw failure;
    }
}
