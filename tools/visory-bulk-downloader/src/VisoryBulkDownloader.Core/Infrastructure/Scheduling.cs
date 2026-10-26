using System.Diagnostics;
using Microsoft.Win32;

namespace VisoryBulkDownloader.Core.Infrastructure;

/// <summary>Optional, user-enabled scheduling through Windows Task Scheduler (schtasks.exe), with a per-user Run-key fallback for sign-in start.</summary>
public static class Scheduling
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static string TaskName(string appRoot) => $"VisoryBulkDownloader-{DataLocations.Hash(appRoot)}";
    public static string LogonTaskName(string appRoot) => TaskName(appRoot) + "-logon";

    public static IReadOnlyList<string> DailyArguments(string taskName, string executable, string time) =>
        ["/Create", "/F", "/TN", taskName, "/SC", "DAILY", "/ST", time, "/RL", "LIMITED", "/TR", $"\"{executable}\" --scheduled"];

    public static IReadOnlyList<string> LogonArguments(string taskName, string executable) =>
        ["/Create", "/F", "/TN", taskName, "/SC", "ONLOGON", "/RL", "LIMITED", "/TR", $"\"{executable}\" --background"];

    public static IReadOnlyList<string> DeleteArguments(string taskName) => ["/Delete", "/F", "/TN", taskName];

    public static (int ExitCode, string Output) RunSchtasks(IReadOnlyList<string> arguments)
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "schtasks.exe");
        var info = new ProcessStartInfo(path)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(info) ?? throw new InvalidOperationException("Task Scheduler could not be started.");
        var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        if (!process.WaitForExit(30000))
        {
            process.Kill();
            return (-1, "Task Scheduler did not respond.");
        }
        return (process.ExitCode, output.Trim());
    }

    public static (bool Ok, string Message) ConfigureDaily(string appRoot, string executable, bool enable, string time)
    {
        var name = TaskName(appRoot);
        if (!enable)
        {
            var (code, output) = RunSchtasks(DeleteArguments(name));
            return (code == 0 || output.Contains("cannot find", StringComparison.OrdinalIgnoreCase) || output.Contains("nicht gefunden", StringComparison.OrdinalIgnoreCase), output);
        }
        var result = RunSchtasks(DailyArguments(name, executable, time));
        return (result.ExitCode == 0, result.Output);
    }

    /// <summary>Starts quietly after sign-in. Tries a Task Scheduler logon task, then the per-user Run key.</summary>
    public static (bool Ok, string Message) ConfigureLogon(string appRoot, string executable, bool enable)
    {
        var name = LogonTaskName(appRoot);
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true) ?? Registry.CurrentUser.CreateSubKey(RunKeyPath);
        if (!enable)
        {
            RunSchtasks(DeleteArguments(name));
            key.DeleteValue(name, throwOnMissingValue: false);
            return (true, "Start after sign-in disabled.");
        }
        var task = RunSchtasks(LogonArguments(name, executable));
        if (task.ExitCode == 0) return (true, "Task Scheduler will start the downloader quietly after sign-in.");
        key.SetValue(name, $"\"{executable}\" --background");
        return (true, "Task Scheduler declined a logon task without elevation; the per-user startup entry was used instead.");
    }
}

public enum WebView2Status { Available, Missing, Error }

public sealed record WebView2Availability(WebView2Status Status, string? Version, string Message)
{
    public const string InstallUrl = "https://developer.microsoft.com/microsoft-edge/webview2/consumer/";

    public static WebView2Availability Evaluate(Func<string?> versionProvider)
    {
        try
        {
            var version = versionProvider();
            return string.IsNullOrWhiteSpace(version)
                ? new WebView2Availability(WebView2Status.Missing, null, "Microsoft Edge WebView2 Runtime is not installed. It is required to sign in to Visory.")
                : new WebView2Availability(WebView2Status.Available, version, $"WebView2 Runtime {version}");
        }
        catch (Exception e) when (e.GetType().Name.Contains("NotFound", StringComparison.Ordinal) || e is FileNotFoundException or DllNotFoundException)
        {
            return new WebView2Availability(WebView2Status.Missing, null, "Microsoft Edge WebView2 Runtime is not installed. It is required to sign in to Visory.");
        }
        catch (Exception e)
        {
            return new WebView2Availability(WebView2Status.Error, null, "WebView2 could not be checked: " + e.Message);
        }
    }
}
