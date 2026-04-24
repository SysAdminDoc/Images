using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using Images.ViewModels;

namespace Images;

public partial class MainWindow : Window
{
    private MainViewModel Vm => (MainViewModel)DataContext;

    public MainWindow()
    {
        InitializeComponent();

        Viewport.MouseEnter += (_, _) => FadeArrows(1.0);
        Viewport.MouseLeave += (_, _) => FadeArrows(0.0);
        Loaded += (_, _) => Focus();
    }

    public void OpenPath(string path) => Vm.OpenFile(path);

    private void FadeArrows(double target)
    {
        if (!Vm.HasImage) return;
        var anim = new DoubleAnimation
        {
            To = target,
            Duration = TimeSpan.FromMilliseconds(180)
        };
        PrevArrow.BeginAnimation(OpacityProperty, anim);
        NextArrow.BeginAnimation(OpacityProperty, anim);
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        // Don't steal keys from the rename editor.
        if (Keyboard.FocusedElement is TextBox) return;

        switch (e.Key)
        {
            case Key.Left:
            case Key.Back:
                Vm.PrevCommand.Execute(null); e.Handled = true; break;
            case Key.Right:
            case Key.Space:
                Vm.NextCommand.Execute(null); e.Handled = true; break;
            case Key.Home:
                Vm.FirstCommand.Execute(null); e.Handled = true; break;
            case Key.End:
                Vm.LastCommand.Execute(null); e.Handled = true; break;
            case Key.Delete:
                Vm.DeleteCommand.Execute(null); e.Handled = true; break;
            case Key.F5:
                Vm.RefreshCommand.Execute(null); e.Handled = true; break;
            case Key.OemPlus:
            case Key.Add:
                Canvas.ZoomBy(1.2); e.Handled = true; break;
            case Key.OemMinus:
            case Key.Subtract:
                Canvas.ZoomBy(1 / 1.2); e.Handled = true; break;
            case Key.D0:
            case Key.NumPad0:
                Canvas.ResetView(); e.Handled = true; break;
            case Key.D1:
            case Key.NumPad1:
                Canvas.OneToOne(); e.Handled = true; break;
        }
    }

    private void StemEditor_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                Vm.CommitRenameCommand.Execute(null);
                Keyboard.ClearFocus();
                Focus();
                e.Handled = true;
                break;
            case Key.Escape:
                Vm.CancelRenameCommand.Execute(null);
                Keyboard.ClearFocus();
                Focus();
                e.Handled = true;
                break;
        }
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = GetDroppedPath(e) is not null ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        var path = GetDroppedPath(e);
        if (path is null) return;
        Vm.OpenFile(path);
        e.Handled = true;
    }

    private static string? GetDroppedPath(DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return null;
        var paths = (string[]?)e.Data.GetData(DataFormats.FileDrop);
        if (paths is null || paths.Length == 0) return null;
        var first = paths[0];
        if (!File.Exists(first)) return null;
        var ext = Path.GetExtension(first);
        return Services.DirectoryNavigator.SupportedExtensions.Contains(ext) ? first : null;
    }
}
