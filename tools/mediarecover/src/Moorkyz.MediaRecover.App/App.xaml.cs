using System.IO;
using System.Windows;

namespace Moorkyz.Tools.MediaRecover.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show(args.Exception.Message, "Moorkyz Media Recover — error", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
        try
        {
            var state = e.Args.Length == 2 && e.Args[0] == "--smoke" ? Path.GetFullPath(e.Args[1]) : Core.Settings.StateDirectory;
            var model = new MainViewModel(state);
            var window = new MainWindow(model); MainWindow = window; window.Show();
            if (e.Args.Length == 2 && e.Args[0] == "--smoke") File.WriteAllText(Path.Combine(state, "ready.txt"), "WPF window loaded; no scan or connection started.");
        }
        catch (Exception error) { MessageBox.Show(error.Message, "Moorkyz Media Recover — startup error", MessageBoxButton.OK, MessageBoxImage.Error); Shutdown(1); }
    }
}
