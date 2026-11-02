using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

internal static class MoorkyzLauncher
{
    [STAThread]
    private static int Main(string[] args)
    {
        bool check = Array.IndexOf(args, "--check") >= 0;
        try
        {
            string root = null;
            bool seenCheck = false;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--check" && !seenCheck)
                    seenCheck = true;
                else if (args[i] == "--root" && root == null && i + 1 < args.Length)
                    root = Path.GetFullPath(args[++i]);
                else
                    throw new ArgumentException("Usage: Moorkyz.exe [--root DIRECTORY] [--check]");
            }

            root = root ?? FindRoot();
            foreach (string file in new[] {
                "moorkyz",
                @"src\.moorkyz\scripts\moorkyz.cjs",
                @"src\.moorkyz\scripts\helpers\startup.cjs"
            })
                if (!File.Exists(Path.Combine(root, file)))
                    throw new FileNotFoundException("Required checkout file is missing: " + Path.Combine(root, file));

            string node = FindNode();
            string versionText;
            int versionExit = Run(node, "--version", root, 10000, out versionText);
            Match match = Regex.Match(versionText.Trim(), @"^v(\d+)\.(\d+)\.(\d+)$");
            if (versionExit != 0 || !match.Success ||
                new Version(match.Groups[1].Value + "." + match.Groups[2].Value + "." + match.Groups[3].Value)
                    .CompareTo(new Version(22, 18, 0)) < 0)
                throw new InvalidOperationException("Node.js 22.18.0+ is required. Observed: " + versionText.Trim());

            string output;
            int code = Run(node, Quote(Path.Combine(root, "moorkyz")) + " launch" + (check ? " --check" : ""), root, 0, out output);
            if (check) Console.Write(output);
            else if (code != 0) throw new InvalidOperationException("The control center could not start (exit " + code + ").\n" + output);
            return code;
        }
        catch (Exception error)
        {
            string message = error.Message + "\nMachine prerequisites were not installed. Check the checkout and Node/PowerShell setup.";
            if (check) Console.Error.WriteLine(message);
            else System.Windows.Forms.MessageBox.Show(message, "Moorkyz startup failed",
                System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
            return 1;
        }
    }

    private static string FindRoot()
    {
        for (DirectoryInfo folder = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            folder != null; folder = folder.Parent)
            if (File.Exists(Path.Combine(folder.FullName, "moorkyz")) &&
                File.Exists(Path.Combine(folder.FullName, @"src\package.json")))
                return folder.FullName;
        throw new DirectoryNotFoundException("Keep the EXE inside the checkout, or pass --root DIRECTORY.");
    }

    private static string FindNode()
    {
        foreach (string folder in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';'))
        {
            string directory = folder.Trim().Trim('"');
            if (directory.Length == 0)
                continue;
            string prefix = Path.GetPathRoot(directory);
            if (prefix == null || prefix.Length < 3)
                continue;
            string candidate = Path.Combine(directory, "node.exe");
            if (File.Exists(candidate))
                return candidate;
        }
        throw new FileNotFoundException("Node.js 22.18.0+ was not found on PATH. Install it explicitly, then reopen the launcher.");
    }

    // Windows command-line quoting, including backslashes before quotes and the closing quote.
    private static string Quote(string value)
    {
        StringBuilder quoted = new StringBuilder("\"");
        int slashes = 0;
        foreach (char character in value)
        {
            if (character == '\\')
            {
                slashes++;
                continue;
            }
            quoted.Append('\\', character == '"' ? slashes * 2 + 1 : slashes);
            quoted.Append(character);
            slashes = 0;
        }
        quoted.Append('\\', slashes * 2);
        return quoted.Append('"').ToString();
    }

    private static int Run(string executable, string arguments, string root, int timeout, out string output)
    {
        StringBuilder text = new StringBuilder();
        DataReceivedEventHandler collect = delegate(object sender, DataReceivedEventArgs eventArgs)
        {
            if (eventArgs.Data == null) return;
            lock (text)
            {
                text.AppendLine(eventArgs.Data);
                if (text.Length > 32768) text.Remove(0, text.Length - 32768);
            }
        };
        using (Process process = new Process())
        {
            process.StartInfo = new ProcessStartInfo(executable, arguments) {
                WorkingDirectory = root,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            process.OutputDataReceived += collect;
            process.ErrorDataReceived += collect;
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            if (timeout > 0 && !process.WaitForExit(timeout))
            {
                process.Kill();
                process.WaitForExit();
                throw new TimeoutException("Timed out checking the Node.js runtime.");
            }
            process.WaitForExit();
            output = text.ToString();
            return process.ExitCode;
        }
    }
}
