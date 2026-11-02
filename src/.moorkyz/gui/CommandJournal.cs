using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace Moorkyz
{
    internal sealed class CommandJournal : IDisposable
    {
        private readonly StreamWriter logs;
        private readonly StreamWriter errors;
        private readonly string command;
        private readonly string project;
        private readonly string launcher;
        private readonly DateTime started = DateTime.UtcNow;
        private int logLine;
        private int errorLine;
        private readonly System.Diagnostics.Stopwatch errorClock = new System.Diagnostics.Stopwatch();
        private double errorIntervals;
        private readonly object timingLock = new object();
        private readonly System.Diagnostics.Stopwatch foregroundClock = new System.Diagnostics.Stopwatch();
        private bool timingFinished;
        public string DirectoryPath { get; private set; }
        public double AverageSeconds { get; private set; }
        public int CompletedRuns { get; private set; }

        public CommandJournal(string root, string entryPoint, string command, string project)
        {
            this.command = command;
            this.project = project;
            launcher = Path.GetFileName(entryPoint);
            string runs = SafeFolder(root, Path.Combine(".moorkyz", "control-center", "runs"));
            DirectoryPath = SafeFolder(root, Path.Combine(runs,
                DateTime.UtcNow.ToString("yyyy-MM-ddTHH-mm-ss-fff", CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N")));
            logs = new StreamWriter(new FileStream(Path.Combine(DirectoryPath, "logs.log"), FileMode.CreateNew, FileAccess.Write, FileShare.Read), new UTF8Encoding(false));
            try
            {
                errors = new StreamWriter(new FileStream(Path.Combine(DirectoryPath, "errors.log"), FileMode.CreateNew, FileAccess.Write, FileShare.Read), new UTF8Encoding(false));
            }
            catch
            {
                logs.Dispose();
                throw;
            }
            logs.AutoFlush = true;
            errors.AutoFlush = true;
        }

        internal static string SafeFolder(string root, string relative)
        {
            string basePath = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string target = Path.GetFullPath(Path.Combine(basePath, relative));
            if (!target.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
                throw new IOException("Log directory escapes the checkout.");
            string current = basePath;
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Refusing a linked log root.");
            foreach (string part in target.Substring(basePath.Length).Split(Path.DirectorySeparatorChar))
            {
                current = Path.Combine(current, part);
                Directory.CreateDirectory(current);
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Refusing linked log directory: " + current);
            }
            return target;
        }

        public static string Redact(string value)
        {
            string safe = Regex.Replace(value, @"https?://[^\s""'<>]+", "[URL redacted]", RegexOptions.IgnoreCase);
            safe = Regex.Replace(safe, @"\bBearer\s+\S+", "Bearer [redacted]", RegexOptions.IgnoreCase);
            return Regex.Replace(safe, @"\b(token|password|secret|authorization|cookie|api[_-]?key|_authToken|_auth)[""']?\s*[:=]\s*(""[^""]*""|'[^']*'|\S+)",
                "$1=[redacted]", RegexOptions.IgnoreCase);
        }

        public LogRecord Write(string line, bool error)
        {
            var record = new LogRecord {
                Timestamp = DateTime.UtcNow, Level = LogRecord.DetectLevel(line, error), Message = line,
                File = Path.Combine(DirectoryPath, "logs.log"), Line = ++logLine
            };
            string run = Path.GetFileName(DirectoryPath);
            record.Reference = run.Substring(run.Length - 8) + ":L" + record.Line.ToString(CultureInfo.InvariantCulture);
            logs.WriteLine(record.Display(false));
            if (error || LogRecord.IsError(record.Level))
            {
                if (errorClock.IsRunning) errorIntervals += errorClock.Elapsed.TotalSeconds;
                errorClock.Restart();
                record.ErrorFile = Path.Combine(DirectoryPath, "errors.log");
                record.ErrorLine = ++errorLine;
                errors.WriteLine(record.Display(false));
            }
            return record;
        }

        public void SetForeground(bool active)
        {
            lock (timingLock)
            {
                if (timingFinished) return;
                if (active) foregroundClock.Start();
                else foregroundClock.Stop();
            }
        }

        public void Complete(int exitCode, double seconds)
        {
            double foreground;
            lock (timingLock)
            {
                timingFinished = true;
                foregroundClock.Stop();
                foreground = Math.Min(seconds, foregroundClock.Elapsed.TotalSeconds);
            }
            logs.Flush();
            errors.Flush();
            string temporary = Path.Combine(DirectoryPath, "report.pending");
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (XmlWriter writer = XmlWriter.Create(stream, new XmlWriterSettings { Indent = true, CheckCharacters = true }))
            {
                writer.WriteStartElement("commandReport");
                writer.WriteAttributeString("version", "1");
                foreach (var field in new[] {
                    new[] { "launcher", launcher }, new[] { "command", command }, new[] { "project", project },
                    new[] { "startedUtc", started.ToString("O", CultureInfo.InvariantCulture) },
                    new[] { "finishedUtc", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture) },
                    new[] { "exitCode", exitCode.ToString(CultureInfo.InvariantCulture) },
                    new[] { "durationSeconds", seconds.ToString("R", CultureInfo.InvariantCulture) },
                    new[] { "uiSeconds", foreground.ToString("R", CultureInfo.InvariantCulture) },
                    new[] { "backgroundSeconds", (seconds - foreground).ToString("R", CultureInfo.InvariantCulture) },
                    new[] { "errorCount", errorLine.ToString(CultureInfo.InvariantCulture) },
                    new[] { "errorIntervalCount", Math.Max(0, errorLine - 1).ToString(CultureInfo.InvariantCulture) },
                    new[] { "averageErrorIntervalSeconds", errorLine > 1 ? (errorIntervals / (errorLine - 1)).ToString("R", CultureInfo.InvariantCulture) : "" },
                    new[] { "status", exitCode == 0 ? "PASS" : "FAIL" },
                    new[] { "logs", "logs.log" }, new[] { "errors", "errors.log" }
                }) writer.WriteElementString(field[0], field[1]);
                writer.WriteEndElement();
            }
            File.Move(temporary, Path.Combine(DirectoryPath, "report.xml"));
            double total = 0;
            foreach (string folder in Directory.GetDirectories(Path.GetDirectoryName(DirectoryPath)))
            {
                if ((File.GetAttributes(folder) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Refusing linked report directory: " + folder);
                string report = Path.Combine(folder, "report.xml");
                if (!File.Exists(report)) continue;
                if ((File.GetAttributes(report) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Refusing linked report: " + report);
                var document = new XmlDocument { XmlResolver = null };
                using (XmlReader reader = XmlReader.Create(report, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
                    document.Load(reader);
                if (document.DocumentElement == null || document.DocumentElement.Name != "commandReport")
                    throw new IOException("Invalid command report: " + report);
                if (Field(document, "command", report) != command ||
                    Field(document, "project", report) != project ||
                    Field(document, "launcher", report) != launcher) continue;
                double elapsed;
                if (!double.TryParse(Field(document, "durationSeconds", report), NumberStyles.Float, CultureInfo.InvariantCulture, out elapsed))
                    throw new IOException("Invalid report duration: " + report);
                if (double.IsNaN(elapsed) || double.IsInfinity(elapsed) || elapsed < 0)
                    throw new IOException("Invalid report duration: " + report);
                total += elapsed;
                CompletedRuns++;
            }
            AverageSeconds = total / CompletedRuns;
        }

        private static string Field(XmlDocument document, string name, string report)
        {
            XmlNode field = document.SelectSingleNode("/commandReport/" + name);
            if (field == null) throw new IOException("Missing " + name + " in command report: " + report);
            return field.InnerText;
        }

        public void Dispose()
        {
            try { logs.Dispose(); }
            finally { errors.Dispose(); }
        }
    }
}
