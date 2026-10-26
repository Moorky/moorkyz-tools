using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using MoorkszVisoryMediaDownloader.Core.Localization;

namespace MoorkszVisoryMediaDownloader.App;

public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        Raise(name);
        return true;
    }

    protected void Raise([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null) : ICommand
{
    public RelayCommand(Action execute, Func<bool>? canExecute = null) : this(_ => execute(), canExecute is null ? null : _ => canExecute()) { }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => canExecute?.Invoke(parameter) ?? true;
    public void Execute(object? parameter) => execute(parameter);
}

/// <summary>Async command that reports exceptions to the hub instead of crashing and prevents re-entry.</summary>
public sealed class AsyncCommand(Func<object?, Task> execute, Func<object?, bool>? canExecute = null) : ICommand
{
    private bool running;
    public static Action<Exception>? ErrorHandler { get; set; }

    public AsyncCommand(Func<Task> execute, Func<bool>? canExecute = null) : this(_ => execute(), canExecute is null ? null : _ => canExecute()) { }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => !running && (canExecute?.Invoke(parameter) ?? true);

    public async void Execute(object? parameter)
    {
        if (running) return;
        running = true;
        CommandManager.InvalidateRequerySuggested();
        try
        {
            await execute(parameter);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            ErrorHandler?.Invoke(e);
        }
        finally
        {
            running = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }
}

/// <summary>Live-switchable localization. XAML: {Binding [Key], Source={x:Static app:Loc.Instance}}.</summary>
public sealed class Loc : INotifyPropertyChanged
{
    public static Loc Instance { get; } = new();
    private IReadOnlyDictionary<string, string> table = Strings.En;

    public event PropertyChangedEventHandler? PropertyChanged;
    public string Language { get; private set; } = "en";
    public static System.Globalization.CultureInfo Culture { get; private set; } = new("en-US");

    public string this[string key] => table.TryGetValue(key, out var value) ? value : Strings.En.TryGetValue(key, out var en) ? en : $"[{key}]";

    public static string T(string key) => Instance[key];
    public static string F(string key, params object[] args) => string.Format(Culture, Instance[key], args);

    public static string ArchiveStatus(string status)
    {
        var incomplete = status.Contains("incomplete", StringComparison.Ordinal);
        var key = status.Split(' ')[0] switch
        {
            "verified" => "ArchiveStatusVerified",
            "corrupt" => "ArchiveStatusCorrupt",
            "missing" => "ArchiveStatusMissing",
            "reused" => "ArchiveStatusReused",
            "failed" => "ArchiveStatusFailed",
            "skipped" => "ArchiveStatusSkipped",
            _ => "ArchiveStatusNotCreated",
        };
        return T(key) + (incomplete ? $" ({T("ArchiveIncomplete")})" : "");
    }

    public static string TransferStatus(string status) => status switch
    {
        "Downloading" => T("RecStatusDownloading"),
        "Waiting to retry" => T("WaitingRetry"),
        "Waiting for network" => T("StateWaitingNetwork"),
        "Queued" or "Starting" => T("RecStatusQueued"),
        _ => status,
    };

    public void Apply(string setting)
    {
        Language = Strings.ResolveLanguage(setting);
        table = Strings.For(Language);
        var culture = new System.Globalization.CultureInfo(Language == "de" ? "de-DE" : "en-US");
        Culture = culture;
        System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
    }
}

public sealed record Choice<T>(T Value, string Key)
{
    public string Label => Loc.T(Key);
    public override string ToString() => Label;
}

public static class Ui
{
    public static void OnUi(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess()) action();
        else dispatcher.BeginInvoke(action);
    }

    public static void ReplaceAll<T>(this ObservableCollection<T> collection, IEnumerable<T> items)
    {
        collection.Clear();
        foreach (var item in items) collection.Add(item);
    }

    public static void OpenFolder(string path)
    {
        if (File.Exists(path))
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = false });
            return;
        }
        Directory.CreateDirectory(path);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
    }

    public static void OpenFile(string path)
    {
        if (File.Exists(path)) System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
    }

    public static void OpenUrl(string url) => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
}
