using System.Threading;
using System.Windows;
using System.Windows.Controls;

namespace Images.Tests;

[Collection("WpfSmoke")]
public sealed class ImportInboxWindowTests
{
    [Fact]
    public void SetBusy_DisablesDestinationChooser()
    {
        RunOnStaWithTheme(() =>
        {
            var window = new ImportInboxWindow();
            try
            {
                var chooseDestination = Assert.IsType<Button>(window.FindName("ChooseDestinationButton"));

                window.SetBusy(true);
                Assert.False(chooseDestination.IsEnabled);

                window.SetBusy(false);
                Assert.True(chooseDestination.IsEnabled);
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static void RunOnStaWithTheme(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var createdApplication = Application.Current is null;
            var application = Application.Current ?? new Application
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown
            };

            try
            {
                application.Resources.MergedDictionaries.Clear();
                application.Resources.MergedDictionaries.Add(
                    (ResourceDictionary)Application.LoadComponent(
                        new Uri("/Images;component/Themes/DarkTheme.xaml", UriKind.Relative)));

                action();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
            finally
            {
                if (createdApplication)
                    application.Shutdown();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
            throw failure;
    }
}
