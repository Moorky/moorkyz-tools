using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml;

namespace Moorkyz
{
    public sealed class SavedReport
    {
        public string Id, File, Launcher, Project, Command, Outcome;
        public DateTime Started;
        public double Seconds;
        public double? UiSeconds, BackgroundSeconds, VideoSeconds, FileBytes;
        public string Rating = "No baseline";

        private static string Field(XmlDocument document, string name, bool required)
        {
            XmlNode node = document.SelectSingleNode("/commandReport/" + name);
            if (node == null && required) throw new IOException("Missing report field: " + name);
            return node == null ? null : node.InnerText;
        }

        private static double? Number(XmlDocument document, string name, bool required)
        {
            string value = Field(document, name, required);
            if (value == null) return null;
            double number;
            if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out number) ||
                double.IsNaN(number) || double.IsInfinity(number) || number < 0)
                throw new IOException("Invalid report measurement: " + name);
            return number;
        }

        public static SavedReport Read(string root, string file)
        {
            TerminalSearch.ValidateFile(root, file);
            var document = new XmlDocument { XmlResolver = null };
            using (var reader = XmlReader.Create(file, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 1048576 }))
                document.Load(reader);
            if (document.DocumentElement == null || document.DocumentElement.Name != "commandReport" ||
                document.DocumentElement.GetAttribute("version") != "1") throw new IOException("Unsupported command report: " + file);
            DateTime started;
            if (!DateTime.TryParse(Field(document, "startedUtc", true), CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out started) || started.Kind != DateTimeKind.Utc)
                throw new IOException("Invalid UTC report timestamp: " + file);
            string outcome = Field(document, "status", true);
            int code;
            if (!int.TryParse(Field(document, "exitCode", true), NumberStyles.Integer, CultureInfo.InvariantCulture, out code) ||
                (outcome != "PASS" && outcome != "FAIL") || (outcome == "PASS") != (code == 0))
                throw new IOException("Invalid report outcome: " + file);
            var report = new SavedReport {
                File = file, Id = Path.GetFileName(Path.GetDirectoryName(file)), Started = started,
                Launcher = Field(document, "launcher", true), Project = Field(document, "project", true),
                Command = Field(document, "command", true), Outcome = outcome,
                Seconds = Number(document, "durationSeconds", true).Value,
                UiSeconds = Number(document, "uiSeconds", false), BackgroundSeconds = Number(document, "backgroundSeconds", false),
                VideoSeconds = Number(document, "videoDurationSeconds", false), FileBytes = Number(document, "fileSizeBytes", false)
            };
            if (report.UiSeconds.HasValue != report.BackgroundSeconds.HasValue ||
                (report.UiSeconds.HasValue && Math.Abs(report.UiSeconds.Value + report.BackgroundSeconds.Value - report.Seconds) > 0.001))
                throw new IOException("Report UI/background time does not equal task duration: " + file);
            if (report.FileBytes.HasValue && Math.Floor(report.FileBytes.Value) != report.FileBytes.Value)
                throw new IOException("File size must be a whole number of bytes: " + file);
            return report;
        }

        public static string Grade(double duration, double? baseline, bool success)
        {
            if (double.IsNaN(duration) || double.IsInfinity(duration) || duration < 0 ||
                (baseline.HasValue && (double.IsNaN(baseline.Value) || double.IsInfinity(baseline.Value) || baseline.Value < 0)))
                throw new ArgumentOutOfRangeException("duration", "Performance measurements must be finite and nonnegative.");
            if (!success) return "Failed";
            if (!baseline.HasValue || baseline.Value <= 0) return "No baseline";
            double ratio = duration / baseline.Value;
            if (ratio <= 0.9) return "Faster";
            if (ratio <= 1.1) return "Typical";
            if (ratio <= 1.25) return "Slower";
            return "Much slower";
        }

        public static Brush Accent(FrameworkElement owner, string rating)
        {
            string key = rating == "Faster" ? "Diagnostic.Success" :
                rating == "Slower" ? "Diagnostic.Warning" :
                rating == "Much slower" || rating == "Failed" ? "Diagnostic.Error" : "Diagnostic.Statistics";
            return (Brush)owner.FindResource(key);
        }

        private static string Time(double? value) { return value.HasValue ? DiagnosticPresentation.Duration(value.Value) : "Unavailable"; }

        public string Summary()
        {
            return CommandJournal.Redact(Command + " / " + Project + " / " + Launcher) +
                "\nSuccess: " + (Outcome == "PASS" ? "Yes (PASS)" : "No (FAIL)") +
                " | Time spent: " + DiagnosticPresentation.Duration(Seconds) +
                " | Performance: " + Rating +
                "\nVideo length: " + Time(VideoSeconds) +
                " | File size: " + (FileBytes.HasValue ? FileBytes.Value.ToString("0", CultureInfo.InvariantCulture) + " bytes" : "Unavailable") +
                "\nTime on UI: " + Time(UiSeconds) + " | Time in background: " + Time(BackgroundSeconds);
        }
    }

    public sealed class ReportView : StackPanel, IDisposable
    {
        private sealed class History
        {
            public double Average;
            public int Count;
        }
        private readonly Window owner;
        private readonly string root;
        private readonly DispatcherTimer debounce;
        private CancellationTokenSource cancellation;
        private bool disposed;
        private int limit = 5;
        private bool compact;
        public TextBox Query { get; private set; }
        public ListBox Results { get; private set; }
        public TextBlock Status { get; private set; }
        public Border Diagnostics { get; private set; }
        public Border Statistics { get; private set; }
        public TextBlock DiagnosticText { get; private set; }
        public TextBlock StatisticText { get; private set; }

        public ReportView(Window owner, string root)
        {
            this.owner = owner;
            this.root = root;
            Children.Add(new TextBlock { Text = "Reports", FontSize = 30, Foreground = (Brush)owner.FindResource("Diagnostic.Reference") });
            Children.Add(new TextBlock { Text = "Local command reports. Foreground means the control window was active; background means inactive or minimized.", Margin = new Thickness(0, 9, 0, 12) });
            DiagnosticText = new TextBlock { Text = "Open Reports or Refresh to read saved history.", Foreground = (Brush)owner.FindResource("Diagnostic.Diagnostics") };
            StatisticText = new TextBlock { Text = "Statistics unavailable until reports are loaded.", Foreground = (Brush)owner.FindResource("Diagnostic.Statistics") };
            Diagnostics = Section("Diagnostics", DiagnosticText, "Diagnostic.Diagnostics");
            Statistics = Section("Statistics", StatisticText, "Diagnostic.Statistics");
            var summaries = new WrapPanel();
            summaries.Children.Add(Diagnostics);
            summaries.Children.Add(Statistics);
            summaries.SizeChanged += delegate
            {
                double width = summaries.ActualWidth;
                double itemWidth = width >= 700 ? width / 2 : width;
                Diagnostics.Width = Statistics.Width = Math.Max(0, itemWidth);
            };
            Children.Add(summaries);
            var body = new StackPanel();
            body.Children.Add(new TextBlock { Text = "Report list", FontSize = 20 });
            Query = new TextBox { ToolTip = "Search ID, date, outcome, task, measurements or rating. AND terms, quoted phrases and -exclusions." };
            AutomationProperties.SetName(Query, "Search reports");
            body.Children.Add(Query);
            var actions = new WrapPanel();
            var refresh = Button("_Refresh reports");
            refresh.Click += delegate { Refresh(); };
            var clear = Button("_Clear search");
            clear.Click += delegate { Query.Clear(); Query.Focus(); };
            actions.Children.Add(refresh);
            actions.Children.Add(clear);
            body.Children.Add(actions);
            Status = new TextBlock { Text = "No reports loaded.", Margin = new Thickness(0, 8, 0, 8) };
            AutomationProperties.SetLiveSetting(Status, AutomationLiveSetting.Polite);
            body.Children.Add(Status);
            Results = new ListBox { MaxHeight = 650, HorizontalContentAlignment = HorizontalAlignment.Stretch };
            AutomationProperties.SetName(Results, "Saved report results");
            ScrollViewer.SetHorizontalScrollBarVisibility(Results, ScrollBarVisibility.Disabled);
            body.Children.Add(Results);
            Children.Add(new Border { Child = body, Style = (Style)owner.FindResource("Panel"), Margin = new Thickness(0, 12, 0, 0) });
            debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
            debounce.Tick += delegate { Refresh(); };
            Query.TextChanged += delegate
            {
                if (cancellation != null) cancellation.Cancel();
                Results.Items.Clear();
                debounce.Stop();
                Status.Text = "Waiting to search reports...";
                debounce.Start();
            };
            Query.PreviewKeyDown += delegate(object sender, System.Windows.Input.KeyEventArgs e)
            {
                if (e.Key == System.Windows.Input.Key.Escape) { Query.Clear(); e.Handled = true; }
                if (e.Key == System.Windows.Input.Key.Enter && Results.Items.Count > 0)
                {
                    Results.SelectedIndex = (Results.SelectedIndex + 1) % Results.Items.Count;
                    Results.ScrollIntoView(Results.SelectedItem);
                    e.Handled = true;
                }
            };
        }

        private Button Button(string label) { return new Button { Content = label, Style = (Style)owner.FindResource("QuietButton"), Margin = new Thickness(0, 6, 8, 6) }; }

        private Border Section(string heading, UIElement content, string color)
        {
            var body = new StackPanel();
            body.Children.Add(new TextBlock { Text = heading, FontWeight = FontWeights.SemiBold, Foreground = (Brush)owner.FindResource(color), Margin = new Thickness(0, 0, 0, 6) });
            body.Children.Add(content);
            var section = new Border { Child = body, Padding = new Thickness(12), BorderBrush = (Brush)owner.FindResource("Border"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(9) };
            AutomationProperties.SetName(section, heading);
            return section;
        }

        public void Configure(int hits, bool timeOnly)
        {
            if (hits < 1 || hits > 10) throw new ArgumentOutOfRangeException("hits");
            limit = hits;
            compact = timeOnly;
        }

        public void Refresh()
        {
            if (disposed) return;
            debounce.Stop();
            if (cancellation != null) { cancellation.Cancel(); cancellation.Dispose(); }
            cancellation = new CancellationTokenSource();
            CancellationToken token = cancellation.Token;
            Func<string, bool> matches;
            try { matches = TerminalSearch.Matcher(Query.Text); }
            catch (ArgumentException error) { Results.Items.Clear(); Status.Text = error.Message; return; }
            int maximum = limit;
            bool timeOnly = compact;
            Results.Items.Clear();
            Status.Text = "Loading reports...";
            Task.Factory.StartNew(delegate
            {
                var reports = new List<SavedReport>();
                string failure = null;
                int incomplete = 0;
                try
                {
                    string runs = Path.Combine(root, ".moorkyz", "control-center", "runs");
                    if (Directory.Exists(runs))
                    {
                        TerminalSearch.ValidateDirectory(root, runs);
                        foreach (string directory in Directory.GetDirectories(runs))
                        {
                            token.ThrowIfCancellationRequested();
                            TerminalSearch.ValidateDirectory(root, directory);
                            string file = Path.Combine(directory, "report.xml");
                            if (File.Exists(file))
                            {
                                try { reports.Add(SavedReport.Read(root, file)); }
                                catch (Exception error) { throw new IOException("Cannot read report " + file + ": " + error.Message, error); }
                            }
                            else incomplete++;
                        }
                    }
                    reports.Sort(delegate(SavedReport left, SavedReport right) {
                        int order = left.Started.CompareTo(right.Started);
                        return order != 0 ? order : StringComparer.Ordinal.Compare(left.Id, right.Id);
                    });
                    var histories = new Dictionary<string, History>(StringComparer.Ordinal);
                    foreach (SavedReport report in reports)
                    {
                        token.ThrowIfCancellationRequested();
                        string key = report.Launcher.Length + ":" + report.Launcher + report.Project.Length + ":" + report.Project + report.Command;
                        History samples;
                        if (!histories.TryGetValue(key, out samples)) histories.Add(key, samples = new History());
                        report.Rating = SavedReport.Grade(report.Seconds, samples.Count == 0 ? (double?)null : samples.Average, report.Outcome == "PASS");
                        if (report.Outcome == "PASS") { samples.Count++; samples.Average += (report.Seconds - samples.Average) / samples.Count; }
                    }
                }
                catch (OperationCanceledException) { return; }
                catch (Exception error) { failure = "Reports failed: " + CommandJournal.Redact(error.Message); }
                owner.Dispatcher.BeginInvoke(new Action(delegate
                {
                    if (disposed || token.IsCancellationRequested) return;
                    if (failure != null)
                    {
                        Results.Items.Clear();
                        Status.Text = failure;
                        Status.Foreground = (Brush)owner.FindResource("Diagnostic.Error");
                        DiagnosticText.Text = failure;
                        StatisticText.Text = "Statistics unavailable: report validation failed.";
                        return;
                    }
                    DiagnosticText.Text = reports.Count + " validated reports; " + incomplete + " folders without a completed report. Newest reports first. Ratings use prior successful runs of the same launcher/project/task.";
                    Status.Foreground = (Brush)owner.FindResource("Text");
                    double average = 0;
                    for (int i = 0; i < reports.Count; i++) average += (reports[i].Seconds - average) / (i + 1);
                    StatisticText.Text = reports.Count == 0 ? "No completed reports; averages unavailable." :
                        reports.Count + " reports | PASS " + reports.Count(r => r.Outcome == "PASS") + " | FAIL " + reports.Count(r => r.Outcome == "FAIL") +
                        " | Average time spent: " + DiagnosticPresentation.Duration(average);
                    int count = 0;
                    foreach (SavedReport report in reports.AsEnumerable().Reverse())
                    {
                        string summary = report.Summary();
                        string timestamp = report.Started.ToString(timeOnly ? "HH:mm:ss" : "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                        if (!matches(timestamp + " [" + report.Outcome + "] " + report.Id + " " + summary)) continue;
                        count++;
                        if (count > maximum) continue;
                        Brush accent = SavedReport.Accent(owner, report.Rating);
                        var text = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = accent };
                        text.Inlines.Add(new Run("[" + timestamp + " UTC] ") { Foreground = (Brush)owner.FindResource("Diagnostic.Timestamp") });
                        text.Inlines.Add(new Run("[" + report.Outcome + "] ") { Foreground = DiagnosticPresentation.Level(owner, report.Outcome) });
                        text.Inlines.Add(new Run("ID " + report.Id) { Foreground = (Brush)owner.FindResource("Diagnostic.Reference") });
                        text.Inlines.Add(new LineBreak());
                        text.Inlines.Add(new Run(summary) { Foreground = accent });
                        var border = new Border { BorderBrush = accent, BorderThickness = new Thickness(3, 0, 0, 0), Padding = new Thickness(10, 4, 4, 4), Child = text };
                        var item = new ListBoxItem { Tag = report, Content = border, ToolTip = report.File };
                        AutomationProperties.SetName(item, "Report " + report.Id + " " + report.Outcome + " " + report.Rating + " " + summary);
                        Results.Items.Add(item);
                    }
                    Status.Text = "Showing " + Results.Items.Count + " of " + count + " matching reports (limit " + maximum + ").";
                }));
            });
        }

        public void Dispose()
        {
            disposed = true;
            debounce.Stop();
            if (cancellation != null) { cancellation.Cancel(); cancellation.Dispose(); }
        }
    }
}
