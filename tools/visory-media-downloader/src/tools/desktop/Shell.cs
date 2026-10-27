using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using Microsoft.Win32;
using MoorkszVisoryMediaDownloader.Core;
using MoorkszVisoryMediaDownloader.Core.Infrastructure;
using MoorkszVisoryMediaDownloader.Core.Reporting;
using Application = System.Windows.Application;

namespace MoorkszVisoryMediaDownloader.App;

/// <summary>Redacted application diagnostics log (not part of run reports).</summary>
public sealed class AppLog : IDisposable
{
    private readonly DiagnosticLog diagnostics;

    public AppLog(string root, string runId)
    {
        diagnostics = new DiagnosticLog(Path.Combine(root, "logs", "app.md"), Path.Combine(root, "failed", "app-errors.md"), runId);
    }

    public void Write(string level, string message) => diagnostics.Write(level, message);
    public void Error(Exception e, string context) => Write("ERROR", $"{context}: {e.GetType().Name}: {e.Message}\r\n{e.StackTrace}");
    public void Dispose() => diagnostics.Dispose();
}

/// <summary>One active hub per data directory. Later launches focus it or forward a scheduled request.</summary>
public sealed class SingleInstance : IDisposable
{
    private readonly Mutex mutex;
    private readonly EventWaitHandle show;
    private readonly EventWaitHandle scheduled;
    private readonly Thread listener;
    private volatile bool disposed;

    private SingleInstance(Mutex mutex, string baseName)
    {
        this.mutex = mutex;
        show = new EventWaitHandle(false, EventResetMode.AutoReset, baseName + "-show");
        scheduled = new EventWaitHandle(false, EventResetMode.AutoReset, baseName + "-scheduled");
        listener = new Thread(Listen) { IsBackground = true, Name = "single-instance" };
        listener.Start();
    }

    public event Action? ShowRequested;
    public event Action? ScheduledRequested;

    private static string BaseName(string appRoot) => @"Local\" + DataLocations.StorageIdentity + "-" + DataLocations.Hash(appRoot);

    public static SingleInstance? TryAcquire(string appRoot)
    {
        var mutex = new Mutex(initiallyOwned: true, BaseName(appRoot), out var created);
        if (created) return new SingleInstance(mutex, BaseName(appRoot));
        mutex.Dispose();
        return null;
    }

    public static void SignalExisting(string appRoot, bool scheduledRun)
    {
        var name = BaseName(appRoot) + (scheduledRun ? "-scheduled" : "-show");
        if (EventWaitHandle.TryOpenExisting(name, out var handle))
            using (handle) handle.Set();
    }

    private void Listen()
    {
        var handles = new WaitHandle[] { show, scheduled };
        while (!disposed)
        {
            var index = WaitHandle.WaitAny(handles, 1000);
            if (disposed) return;
            if (index == 0) ShowRequested?.Invoke();
            else if (index == 1) ScheduledRequested?.Invoke();
        }
    }

    public void Dispose()
    {
        disposed = true;
        try { mutex.ReleaseMutex(); } catch (ApplicationException) { }
        mutex.Dispose();
        show.Dispose();
        scheduled.Dispose();
    }
}

public static class ThemeManager
{
    private static ThemeChoice current = ThemeChoice.System;
    private static bool hooked;

    public static bool ReduceMotion { get; set; }
    public static bool AnimationsEnabled => !ReduceMotion && SystemParameters.ClientAreaAnimation && !SystemParameters.HighContrast;

    public static void Apply(ThemeChoice choice)
    {
        current = choice;
        var app = Application.Current;
        var highContrast = SystemParameters.HighContrast;
        var dark = choice == ThemeChoice.Dark || (choice == ThemeChoice.System && SystemPrefersDark());
#pragma warning disable WPF0001
        app.ThemeMode = highContrast ? ThemeMode.System : dark ? ThemeMode.Dark : ThemeMode.Light;
#pragma warning restore WPF0001
        var palette = highContrast ? "HighContrast" : dark ? "Dark" : "Light";
        var dictionaries = app.Resources.MergedDictionaries;
        var existing = dictionaries.FirstOrDefault(d => d.Source?.OriginalString.Contains("Palette.", StringComparison.Ordinal) == true);
        var next = new ResourceDictionary { Source = new Uri($"/MoorkszVisoryMediaDownloader;component/Themes/Palette.{palette}.xaml", UriKind.Relative) };
        if (existing is null) dictionaries.Add(next);
        else dictionaries[dictionaries.IndexOf(existing)] = next;
        if (!hooked)
        {
            hooked = true;
            SystemEvents.UserPreferenceChanged += (_, e) =>
            {
                if (e.Category is UserPreferenceCategory.General or UserPreferenceCategory.Accessibility or UserPreferenceCategory.Color)
                    Ui.OnUi(() => Apply(current));
            };
        }
    }

    private static bool SystemPrefersDark()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
    }
}

/// <summary>Notification-area icon with live progress and safe exit.</summary>
public sealed class TrayIcon : IDisposable
{
    private readonly NotifyIcon icon;
    private readonly ToolStripMenuItem progress;
    private readonly ToolStripMenuItem speed;
    private readonly ToolStripMenuItem pause;
    private readonly ToolStripMenuItem open;
    private readonly ToolStripMenuItem downloads;
    private readonly ToolStripMenuItem report;
    private readonly ToolStripMenuItem exit;
    private readonly MainViewModel vm;

    public TrayIcon(MainViewModel vm, Action openHub, Action exitSafely)
    {
        this.vm = vm;
        open = new ToolStripMenuItem("", null, (_, _) => openHub()) { Font = new Font(System.Drawing.SystemFonts.MenuFont ?? System.Drawing.SystemFonts.DefaultFont, System.Drawing.FontStyle.Bold) };
        progress = new ToolStripMenuItem("") { Enabled = false };
        speed = new ToolStripMenuItem("") { Enabled = false };
        pause = new ToolStripMenuItem("", null, (_, _) => { if (vm.PauseResumeCommand.CanExecute(null)) vm.PauseResumeCommand.Execute(null); });
        downloads = new ToolStripMenuItem("", null, (_, _) => vm.OpenDownloadsCommand.Execute(null));
        report = new ToolStripMenuItem("", null, (_, _) => vm.OpenLatestReportCommand.Execute(null));
        exit = new ToolStripMenuItem("", null, (_, _) => exitSafely());
        var menu = new ContextMenuStrip();
        menu.Items.AddRange([open, new ToolStripSeparator(), progress, speed, new ToolStripSeparator(), pause, downloads, report, new ToolStripSeparator(), exit]);
        menu.Opening += (_, _) => Update();
        icon = new NotifyIcon { Icon = LoadIcon(), Visible = true, ContextMenuStrip = menu, Text = "Moorksz Visory Media Downloader" };
        icon.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) openHub(); };
        Update();
    }

    public static Icon LoadIcon()
    {
        var resource = Application.GetResourceStream(new Uri("pack://application:,,,/app.ico"));
        return resource is null ? SystemIcons.Application : new Icon(resource.Stream, SystemInformation.SmallIconSize);
    }

    public void Update()
    {
        open.Text = Loc.T("TrayOpen");
        progress.Text = $"{Loc.T("OverallProgress")}: {vm.OverallPercent:0.0} %";
        speed.Text = $"{Loc.T("Speed")}: {vm.SpeedText}";
        pause.Text = vm.IsPaused ? Loc.T("Resume") : Loc.T("Pause");
        pause.Enabled = vm.IsRunning;
        downloads.Text = Loc.T("OpenDownloads");
        report.Text = Loc.T("TrayLatestReport");
        exit.Text = Loc.T("TrayExit");
        var text = vm.IsRunning ? $"Moorksz Visory Media Downloader - {vm.OverallPercent:0.0} %" : "Moorksz Visory Media Downloader";
        icon.Text = text.Length > 63 ? text[..63] : text;
    }

    public void Notify(string title, string text, bool error = false) =>
        icon.ShowBalloonTip(6000, title, text, error ? ToolTipIcon.Warning : ToolTipIcon.Info);

    public void Dispose()
    {
        icon.Visible = false;
        icon.Dispose();
    }
}
