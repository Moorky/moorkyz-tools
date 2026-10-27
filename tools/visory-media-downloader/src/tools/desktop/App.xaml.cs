using System.Windows;
using System.Windows.Threading;
using MoorkszVisoryMediaDownloader.Core.Infrastructure;
using MoorkszVisoryMediaDownloader.Core.Reporting;

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
            Console.Error.WriteLine(Redactor.Clean("Data location initialization failed: " + ex.Message));
            MessageBox.Show(Redactor.Clean(ex.Message), "Moorksz Visory Media Downloader", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
            return;
        }
        try
        {
            var runId = Environment.GetEnvironmentVariable("VISORY_RUN_ID") ?? DateTimeOffset.Now.ToString("yyyy-MM-dd_HH-mm-ss-fff") + "_" + Guid.NewGuid().ToString("N");
            var runRoot = Environment.GetEnvironmentVariable("VISORY_RUN_DIRECTORY") ?? Path.Combine(locations.DiagnosticsRoot, runId);
            log = new AppLog(runRoot, runId);
            log.Write("INFO", "App startup | Logging active before settings, single-instance check and UI initialization");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine(Redactor.Clean(ex.ToString()));
            MessageBox.Show(Loc.T("DiagnosticsInitFailed") + "\n" + Redactor.Clean(ex.Message), "Moorksz Visory Media Downloader", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
            return;
        }
        instance = SingleInstance.TryAcquire(locations.AppRoot);
        if (instance is null)
        {
            SingleInstance.SignalExisting(locations.AppRoot, scheduled);
            log.Write("INFO", "Existing app instance notified; this launch will exit.");
            Shutdown();
            return;
        }
        DispatcherUnhandledException += OnDispatcherException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) => log.Write("FATAL", args.ExceptionObject.ToString() ?? "unknown");
        TaskScheduler.UnobservedTaskException += (_, args) => { log.Error(args.Exception, "Unobserved task"); args.SetObserved(); };

        var settingsStore = new SettingsStore(locations.SettingsPath, message => log.Write("ERROR", message));
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
        try { log?.Error(e.Exception, "UI"); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine(Redactor.Clean($"Logging failed: {ex.Message}\nOriginal error: {e.Exception.Message}"));
            MessageBox.Show(Loc.T("DiagnosticsStopped") + "\n" + Redactor.Clean(ex.Message), "Moorksz Visory Media Downloader", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
            Shutdown(1);
            return;
        }
        viewModel?.ShowError(e.Exception.Message, null);
        e.Handled = true;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        tray?.Dispose();
        viewModel?.Dispose();
        instance?.Dispose();
        try { log?.Write("INFO", $"App stopped | Exit code: {e.ApplicationExitCode}"); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine(Redactor.Clean("Logging failed during exit: " + ex.Message));
            e.ApplicationExitCode = 1;
        }
        finally { log?.Dispose(); }
        base.OnExit(e);
    }
}
