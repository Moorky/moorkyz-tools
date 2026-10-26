using System.Windows;
using System.Windows.Threading;
using MoorkszVisoryMediaDownloader.Core.Infrastructure;

namespace MoorkszVisoryMediaDownloader.App;

public partial class App : Application
{
    private SingleInstance? instance;
    private MainViewModel? viewModel;
    private MainWindow? window;
    private TrayIcon? tray;
    private AppLog? log;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var scheduled = e.Args.Contains("--scheduled", StringComparer.OrdinalIgnoreCase);
        var background = scheduled || e.Args.Contains("--background", StringComparer.OrdinalIgnoreCase);
        var exeDirectory = Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;
        DataLocations locations;
        try
        {
            locations = DataLocations.Resolve(exeDirectory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(ex.Message, "Moorksz Visory Media Downloader", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
            return;
        }
        instance = SingleInstance.TryAcquire(locations.AppRoot);
        if (instance is null)
        {
            SingleInstance.SignalExisting(locations.AppRoot, scheduled);
            Shutdown();
            return;
        }
        log = new AppLog(locations.AppLogPath);
        DispatcherUnhandledException += OnDispatcherException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) => log.Write("FATAL", args.ExceptionObject.ToString() ?? "unknown");
        TaskScheduler.UnobservedTaskException += (_, args) => { log.Error(args.Exception, "Unobserved task"); args.SetObserved(); };

        var settingsStore = new SettingsStore(locations.SettingsPath);
        var settings = settingsStore.Load();
        Loc.Instance.Apply(settings.Language);
        ThemeManager.ReduceMotion = settings.ReduceMotion;
        ThemeManager.Apply(settings.Theme);

        viewModel = new MainViewModel(locations, settingsStore, settings, log);
        window = new MainWindow(viewModel);
        MainWindow = window;
        tray = new TrayIcon(viewModel, ShowHub, () => _ = viewModel.ExitAsync(confirm: true));
        viewModel.Tray = tray;
        viewModel.RequestShowHub = ShowHub;
        instance.ShowRequested += () => Dispatcher.BeginInvoke(ShowHub);
        instance.ScheduledRequested += () => Dispatcher.BeginInvoke(() => _ = viewModel.RunScheduledAsync(exitWhenDone: false));
        if (!background) window.Show();
        log.Write("INFO", $"Started ({(background ? "background" : "interactive")}); data: {locations.AppRoot}; portable: {locations.Portable}");
        await viewModel.InitializeAsync();
        if (scheduled) await viewModel.RunScheduledAsync(exitWhenDone: true);
        else if (background) await viewModel.ResumeAfterLoginAsync();
    }

    private void ShowHub()
    {
        if (window is null) return;
        if (!window.IsVisible) window.Show();
        if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal;
        window.Activate();
        window.Topmost = true;
        window.Topmost = false;
        window.Focus();
    }

    private void OnDispatcherException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        log?.Error(e.Exception, "UI");
        viewModel?.ShowError(e.Exception.Message, null);
        e.Handled = true;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        tray?.Dispose();
        viewModel?.Dispose();
        instance?.Dispose();
        base.OnExit(e);
    }
}
