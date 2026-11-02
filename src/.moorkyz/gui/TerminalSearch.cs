using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml;

namespace Moorkyz
{
    public sealed class DiagnosticTerminal : RichTextBox
    {
        public string Text
        {
            get
            {
                string text = new TextRange(Document.ContentStart, Document.ContentEnd).Text;
                return text.EndsWith(Environment.NewLine, StringComparison.Ordinal) ? text.Substring(0, text.Length - Environment.NewLine.Length) : text;
            }
        }

        public int CharacterAt(Point point)
        {
            TextPointer position = GetPositionFromPoint(point, true);
            return position == null ? -1 : new TextRange(Document.ContentStart, position).Text.Length;
        }

        public int CaretIndex { get { return new TextRange(Document.ContentStart, CaretPosition).Text.Length; } }
    }

    public static class DiagnosticPresentation
    {
        public static string Duration(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0)
                throw new ArgumentOutOfRangeException("seconds");
            string[] units = { "s", "m", "h", "d" };
            double[] divisors = { 60, 60, 24 };
            int unit = 0;
            while (seconds >= 120 && unit < divisors.Length) seconds /= divisors[unit++];
            // Truncate rather than round into the next unit before its threshold.
            double displayed = Math.Floor(seconds * 10) / 10;
            return displayed.ToString("0.#", CultureInfo.InvariantCulture) + " " + units[unit];
        }

        public static Brush Level(FrameworkElement owner, string level)
        {
            string key;
            switch (level.ToUpperInvariant())
            {
                case "TRACE": case "DEBUG": key = "Diagnostic.Trace"; break;
                case "INFO": case "SUCCESS": case "PASS": key = "Diagnostic.Success"; break;
                case "WARN": case "WARNING": key = "Diagnostic.Warning"; break;
                case "ERROR": case "FATAL": case "CRITICAL": case "FAIL": key = "Diagnostic.Error"; break;
                default: key = "Diagnostic.Trace"; break;
            }
            return (Brush)owner.FindResource(key);
        }

        public static void AddFields(InlineCollection target, FrameworkElement owner, string timestamp, string level, string reference, string message)
        {
            target.Add(new Run(timestamp + " ") { Foreground = (Brush)owner.FindResource("Diagnostic.Timestamp") });
            target.Add(new Run("[" + level + "] ") { Foreground = Level(owner, level) });
            if (!string.IsNullOrEmpty(reference))
                target.Add(new Run("[" + reference + "] ") { Foreground = (Brush)owner.FindResource("Diagnostic.Reference") });
            target.Add(new Run(message) { Foreground = (Brush)owner.FindResource("Text") });
        }
    }

    public sealed class LogRecord
    {
        public DateTime Timestamp;
        public string Level;
        public string Message;
        public string File;
        public int Line;
        public string ErrorFile;
        public int ErrorLine;
        public string Reference;

        public static string DetectLevel(string message, bool error)
        {
            if (error) return "ERROR";
            if (message.StartsWith("[", StringComparison.Ordinal))
            {
                int end = message.IndexOf(']');
                if (end > 1 && end < 10)
                {
                    string level = message.Substring(1, end - 1).ToUpperInvariant();
                    switch (level)
                    {
                        case "TRACE": case "DEBUG": case "INFO": case "SUCCESS": case "PASS":
                        case "WARN": case "WARNING": case "ERROR": case "FATAL": case "CRITICAL": case "FAIL": return level;
                    }
                }
            }
            return "INFO";
        }

        public static bool IsError(string level)
        {
            return level == "ERROR" || level == "FATAL" || level == "CRITICAL" || level == "FAIL";
        }

        public string Display(bool compact)
        {
            return "[" + Timestamp.ToString(compact ? "HH:mm:ss" : "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + "] [" + Level + "] " +
                (string.IsNullOrEmpty(Reference) ? "" : "[" + Reference + "] ") + Message;
        }

        public LogRecord Copy()
        {
            return (LogRecord)MemberwiseClone();
        }
    }

    internal sealed class DiagnosticSettings
    {
        public int Hits = 5;
        public bool Compact;
        private readonly string root;
        public DiagnosticSettings(string root)
        {
            this.root = root;
            string file = Path.Combine(root, ".moorkyz", "control-center", "settings.xml");
            if (!File.Exists(file)) return;
            TerminalSearch.ValidateFile(root, file);
            var document = new XmlDocument { XmlResolver = null };
            using (var reader = XmlReader.Create(file, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
                document.Load(reader);
            int hits;
            bool compact;
            if (!int.TryParse(document.SelectSingleNode("/diagnostics/hits") == null ? "" : document.SelectSingleNode("/diagnostics/hits").InnerText, out hits) ||
                hits < 1 || hits > 10 ||
                !bool.TryParse(document.SelectSingleNode("/diagnostics/compact") == null ? "" : document.SelectSingleNode("/diagnostics/compact").InnerText, out compact))
                throw new IOException("Invalid diagnostic settings: " + file);
            Hits = hits;
            Compact = compact;
        }

        public void Save(int hits, bool compact)
        {
            if (hits < 1 || hits > 10) throw new ArgumentOutOfRangeException("hits", "Search hits must be from 1 to 10.");
            string directory = CommandJournal.SafeFolder(root, Path.Combine(".moorkyz", "control-center"));
            string file = Path.Combine(directory, "settings.xml");
            if (File.Exists(file)) TerminalSearch.ValidateFile(root, file);
            string temporary = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".settings");
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var writer = XmlWriter.Create(stream))
                {
                    writer.WriteStartElement("diagnostics");
                    writer.WriteElementString("hits", hits.ToString(CultureInfo.InvariantCulture));
                    writer.WriteElementString("compact", compact.ToString(CultureInfo.InvariantCulture));
                    writer.WriteEndElement();
                }
                if (File.Exists(file)) File.Replace(temporary, file, null);
                else File.Move(temporary, file);
                Hits = hits;
                Compact = compact;
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }

    public sealed class TerminalSearch : StackPanel, IDisposable
    {
        private const int PreviewLimit = 120000;
        private readonly Window owner;
        private readonly string root;
        private readonly bool errors;
        private readonly DiagnosticTerminal terminal;
        private readonly List<LogRecord> records = new List<LogRecord>();
        private readonly List<int> offsets = new List<int>();
        private readonly DispatcherTimer debounce;
        private CancellationTokenSource cancellation;
        private bool disposed;
        private bool omitted;
        private bool rendering;
        private int version;
        private int limit = 5;
        private bool compact;
        private LogRecord selected;
        public TextBox Query { get; private set; }
        public TextBlock Status { get; private set; }
        public ListBox Results { get; private set; }
        public Button OpenLine { get; private set; }
        public Button OpenDirectory { get; private set; }
        public Window LastSourceWindow { get; private set; }
        public int ResultCount { get { return Results.Items.Count; } }

        public TerminalSearch(Window owner, string root, bool errors, DiagnosticTerminal terminal)
        {
            this.owner = owner;
            this.root = root;
            this.errors = errors;
            this.terminal = terminal;
            string label = errors ? "Errors" : "Logs";
            Margin = new Thickness(0, 6, 0, 0);
            Children.Add(new TextBlock { Text = "Search " + label.ToLowerInvariant() + " (saved history)", Margin = new Thickness(0, 0, 0, 4) });
            Query = new TextBox { MinWidth = 100, ToolTip = "All terms must match. Use \"quoted phrases\", -exclusions and level:ERROR. Enter selects the next hit; Shift+Enter the previous hit; Escape clears." };
            AutomationProperties.SetName(Query, "Search " + label.ToLowerInvariant());
            Children.Add(Query);
            var actions = new WrapPanel();
            OpenLine = new Button { Content = "\uE8A7", Style = (Style)owner.FindResource("IconButton"), Visibility = Visibility.Collapsed, ToolTip = "Open saved file at this exact line", Margin = new Thickness(0, 4, 6, 0) };
            AutomationProperties.SetName(OpenLine, "Open selected " + label.ToLowerInvariant() + " file at line");
            OpenDirectory = new Button { Content = "\uE8B7", Style = (Style)owner.FindResource("IconButton"), Visibility = Visibility.Collapsed, ToolTip = "Show selected file in its directory", Margin = new Thickness(0, 4, 6, 0) };
            AutomationProperties.SetName(OpenDirectory, "Open " + label.ToLowerInvariant() + " directory");
            var clear = new Button { Content = "_Clear search", Style = (Style)owner.FindResource("QuietButton"), Margin = new Thickness(0, 4, 0, 0) };
            clear.Click += delegate { Query.Clear(); Query.Focus(); };
            actions.Children.Add(clear);
            actions.Children.Add(OpenLine);
            actions.Children.Add(OpenDirectory);
            Children.Add(actions);
            Status = new TextBlock { Text = "Search local saved logs; live preview remains read-only.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 5, 0, 5) };
            AutomationProperties.SetLiveSetting(Status, AutomationLiveSetting.Polite);
            Children.Add(Status);
            Results = new ListBox { Visibility = Visibility.Collapsed, MaxHeight = 210, HorizontalContentAlignment = HorizontalAlignment.Stretch };
            AutomationProperties.SetName(Results, label + " search results");
            ScrollViewer.SetHorizontalScrollBarVisibility(Results, ScrollBarVisibility.Disabled);
            Children.Add(Results);
            debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
            debounce.Tick += delegate { debounce.Stop(); SearchNow(); };
            Query.TextChanged += delegate { InvalidateSearch(); };
            Query.PreviewKeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.Key == Key.Escape) { Query.Clear(); e.Handled = true; }
                if (e.Key == Key.Enter && Results.Items.Count > 0)
                {
                    int direction = (Keyboard.Modifiers & ModifierKeys.Shift) != 0 ? -1 : 1;
                    Results.SelectedIndex = Results.SelectedIndex < 0
                        ? (direction > 0 ? 0 : Results.Items.Count - 1)
                        : (Results.SelectedIndex + direction + Results.Items.Count) % Results.Items.Count;
                    Results.ScrollIntoView(Results.SelectedItem);
                    e.Handled = true;
                }
            };
            Results.SelectionChanged += delegate
            {
                var item = Results.SelectedItem as ListBoxItem;
                SelectSource(item == null ? null : item.Tag as LogRecord);
            };
            terminal.PreviewMouseLeftButtonUp += delegate
            {
                int index = terminal.CharacterAt(Mouse.GetPosition(terminal));
                SelectAt(index);
            };
            terminal.SelectionChanged += delegate { if (!rendering && terminal.IsKeyboardFocusWithin) SelectAt(terminal.CaretIndex); };
            terminal.PreviewKeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.Key == Key.F && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
                {
                    Query.Focus();
                    Query.SelectAll();
                    e.Handled = true;
                }
            };
            OpenLine.Click += delegate { OpenSelectedSource(); };
            OpenDirectory.Click += delegate
            {
                try
                {
                    if (selected == null) return;
                    ValidateFile(root, selected.File);
                    Process.Start(new ProcessStartInfo("explorer.exe", "/select," + ControlGui.QuoteArgument(selected.File)) { UseShellExecute = false });
                }
                catch (Exception error) { Status.Text = "Cannot open directory: " + CommandJournal.Redact(error.Message); }
            };
        }

        public void Configure(int hits, bool timeOnly)
        {
            if (hits < 1 || hits > 10) throw new ArgumentOutOfRangeException("hits");
            if (limit == hits && compact == timeOnly) return;
            bool timestampChanged = compact != timeOnly;
            limit = hits;
            compact = timeOnly;
            if (timestampChanged) Render();
            InvalidateSearch();
        }

        public void Append(IList<LogRecord> incoming, bool truncated)
        {
            if (incoming.Count == 0 && !truncated) return;
            omitted |= truncated;
            foreach (LogRecord record in incoming) records.Add(record.Copy());
            Render();
            if (!string.IsNullOrWhiteSpace(Query.Text)) InvalidateSearch();
        }

        private void Render()
        {
            string notice = omitted ? "[Earlier preview omitted; search saved history or open full logs]" + Environment.NewLine : "";
            int length = notice.Length;
            foreach (LogRecord record in records) length += record.Display(compact).Length + Environment.NewLine.Length;
            while (length > PreviewLimit && records.Count > 0)
            {
                omitted = true;
                if (notice.Length == 0)
                {
                    notice = "[Earlier preview omitted; search saved history or open full logs]" + Environment.NewLine;
                    length += notice.Length;
                }
                LogRecord first = records[0];
                int excess = length - PreviewLimit;
                if (first.Message.Length > excess)
                {
                    first.Message = first.Message.Substring(excess);
                    length -= excess;
                }
                else
                {
                    length -= first.Display(compact).Length + Environment.NewLine.Length;
                    records.RemoveAt(0);
                }
            }
            var text = new StringBuilder(notice);
            offsets.Clear();
            foreach (LogRecord record in records)
            {
                offsets.Add(text.Length);
                text.AppendLine(record.Display(compact));
            }
            var paragraph = new Paragraph { Margin = new Thickness(0) };
            Inline selectedInline = null;
            if (notice.Length > 0) paragraph.Inlines.Add(new Run(notice) { Foreground = (Brush)owner.FindResource("Diagnostic.Trace") });
            foreach (LogRecord record in records)
            {
                DiagnosticPresentation.AddFields(paragraph.Inlines, owner,
                    "[" + record.Timestamp.ToString(compact ? "HH:mm:ss" : "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + "]",
                    record.Level, record.Reference, record.Message);
                if (selected != null && selected.File == record.File && selected.Line == record.Line)
                    selectedInline = paragraph.Inlines.LastInline;
                paragraph.Inlines.Add(new LineBreak());
            }
            rendering = true;
            try
            {
                terminal.Document = new FlowDocument(paragraph) { PagePadding = new Thickness(0) };
                if (selectedInline != null) terminal.Selection.Select(selectedInline.ContentStart, selectedInline.ContentStart);
                else terminal.ScrollToEnd();
            }
            finally { rendering = false; }
        }

        public void SelectAt(int character)
        {
            for (int i = offsets.Count - 1; i >= 0; i--)
                if (character >= offsets[i] && character < offsets[i] + records[i].Display(compact).Length)
                {
                    SelectSource(records[i]);
                    return;
                }
            SelectSource(null);
        }

        private void SelectSource(LogRecord record)
        {
            selected = record != null && record.File != null ? record : null;
            OpenLine.Visibility = OpenDirectory.Visibility = selected == null ? Visibility.Collapsed : Visibility.Visible;
            if (selected != null)
                OpenLine.ToolTip = selected.File + " : line " + selected.Line;
        }

        private void InvalidateSearch()
        {
            version++;
            if (cancellation != null) cancellation.Cancel();
            debounce.Stop();
            Results.Items.Clear();
            Results.Visibility = Visibility.Collapsed;
            SelectSource(null);
            if (string.IsNullOrWhiteSpace(Query.Text))
            {
                Status.Text = "Search local saved logs; live preview remains read-only.";
                return;
            }
            Status.Text = "Searching saved history...";
            debounce.Start();
        }

        private sealed class Term
        {
            public string Value;
            public bool Exclude;
            public bool Level;
        }

        private static List<Term> Parse(string query)
        {
            var terms = new List<Term>();
            int i = 0;
            while (i < query.Length)
            {
                while (i < query.Length && char.IsWhiteSpace(query[i])) i++;
                if (i == query.Length) break;
                bool exclude = query[i] == '-';
                if (exclude) i++;
                var value = new StringBuilder();
                bool quoted = false;
                while (i < query.Length && (quoted || !char.IsWhiteSpace(query[i])))
                {
                    char character = query[i++];
                    if (character == '"') quoted = !quoted;
                    else value.Append(character);
                }
                if (quoted) throw new ArgumentException("Close the quoted search phrase.");
                if (value.Length == 0) throw new ArgumentException("Enter a term after '-' or inside quotes.");
                string text = value.ToString();
                bool level = text.StartsWith("level:", StringComparison.OrdinalIgnoreCase);
                if (level) text = text.Substring(6);
                if (text.Length == 0) throw new ArgumentException("Enter a level, for example level:ERROR.");
                terms.Add(new Term { Value = text, Exclude = exclude, Level = level });
            }
            return terms;
        }

        internal static Func<string, bool> Matcher(string query)
        {
            List<Term> terms = Parse(query);
            return delegate(string line)
            {
                foreach (Term term in terms)
                {
                    bool contains = term.Level
                        ? string.Equals(ReadLevel(line), term.Value, StringComparison.OrdinalIgnoreCase)
                        : line.IndexOf(term.Value, StringComparison.OrdinalIgnoreCase) >= 0;
                    if (contains == term.Exclude) return false;
                }
                return true;
            };
        }

        private static string ReadLevel(string line)
        {
            int start = line.StartsWith("[", StringComparison.Ordinal) ? line.IndexOf("] [", StringComparison.Ordinal) : -1;
            start = start >= 0 ? start + 3 : line.IndexOf(" [", StringComparison.Ordinal) + 2;
            if (start < 2) return "";
            int end = line.IndexOf(']', start);
            return end < start ? "" : line.Substring(start, end - start);
        }

        public void SearchNow()
        {
            debounce.Stop();
            if (cancellation != null) { cancellation.Cancel(); cancellation.Dispose(); }
            cancellation = new CancellationTokenSource();
            CancellationToken token = cancellation.Token;
            int requestedVersion = version;
            Func<string, bool> matches;
            try { matches = Matcher(Query.Text); }
            catch (ArgumentException error) { Status.Text = error.Message; return; }
            if (string.IsNullOrWhiteSpace(Query.Text)) return;
            int maximum = limit;
            bool timeOnly = compact;
            string filename = errors ? "errors.log" : "logs.log";
            Task.Factory.StartNew(delegate
            {
                var hits = new List<LogRecord>();
                string failure = null;
                try
                {
                    string runs = Path.Combine(root, ".moorkyz", "control-center", "runs");
                    if (Directory.Exists(runs))
                    {
                        ValidateDirectory(root, runs);
                        string[] directories = Directory.GetDirectories(runs);
                        Array.Sort(directories, StringComparer.Ordinal);
                        Array.Reverse(directories);
                        foreach (string directory in directories)
                        {
                            token.ThrowIfCancellationRequested();
                            ValidateDirectory(root, directory);
                            string file = Path.Combine(directory, filename);
                            if (!File.Exists(file)) continue;
                            ValidateFile(root, file);
                            using (var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                            using (var reader = new StreamReader(stream))
                            {
                                string line;
                                int number = 0;
                                while ((line = reader.ReadLine()) != null)
                                {
                                    token.ThrowIfCancellationRequested();
                                    number++;
                                    if (!matches(line)) continue;
                                    hits.Add(new LogRecord { File = file, Line = number, Message = CommandJournal.Redact(line) });
                                    if (hits.Count > maximum) break;
                                }
                            }
                            if (hits.Count > maximum) break;
                        }
                    }
                }
                catch (OperationCanceledException) { return; }
                catch (Exception error) { failure = "Search failed: " + CommandJournal.Redact(error.Message); }
                owner.Dispatcher.BeginInvoke(new Action(delegate
                {
                    if (disposed || token.IsCancellationRequested || requestedVersion != version) return;
                    Results.Items.Clear();
                    if (failure != null) { Status.Text = failure; return; }
                    bool more = hits.Count > maximum;
                    for (int index = 0; index < Math.Min(maximum, hits.Count); index++)
                    {
                        LogRecord hit = hits[index];
                        string displayed = hit.Message;
                        if (timeOnly && displayed.Length >= 21 && displayed[0] == '[' && displayed[20] == ']')
                            displayed = "[" + displayed.Substring(12, 8) + displayed.Substring(20);
                        string summary = displayed.Length > 300 ? displayed.Substring(0, 300) + "..." : displayed;
                        var content = new TextBlock { TextWrapping = TextWrapping.Wrap };
                        content.Inlines.Add(new Run(Path.GetFileName(Path.GetDirectoryName(hit.File)) + " : " + hit.Line) { Foreground = (Brush)owner.FindResource("Diagnostic.Reference") });
                        content.Inlines.Add(new LineBreak());
                        int stampEnd = summary.IndexOf(']');
                        string stamp = stampEnd >= 0 ? summary.Substring(0, stampEnd + 1) : "";
                        int levelStart = stampEnd + 2;
                        int levelEnd = summary.IndexOf(']', Math.Min(summary.Length, levelStart));
                        if (levelStart < summary.Length && summary[levelStart] == '[' && levelEnd > levelStart)
                        {
                            string message = summary.Substring(Math.Min(summary.Length, levelEnd + 2));
                            Match reference = Regex.Match(message, @"^\[([a-f0-9]{8}:L[0-9]+)\] ");
                            DiagnosticPresentation.AddFields(content.Inlines, owner, stamp, summary.Substring(levelStart + 1, levelEnd - levelStart - 1),
                                reference.Success ? reference.Groups[1].Value : null,
                                reference.Success ? message.Substring(reference.Length) : message);
                        }
                        else content.Inlines.Add(new Run(summary));
                        var item = new ListBoxItem {
                            Tag = hit,
                            Content = content,
                            HorizontalContentAlignment = HorizontalAlignment.Stretch
                        };
                        AutomationProperties.SetName(item, "Line " + hit.Line + ": " + summary);
                        Results.Items.Add(item);
                    }
                    Results.Visibility = hits.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
                    Status.Text = hits.Count == 0 ? "No matching saved lines." : "Showing " + Results.Items.Count + (more ? "+ matches; refine your search." : " matching saved lines.") + " Newest runs first.";
                }));
            });
        }

        public void OpenSelectedSource()
        {
            try
            {
                if (selected == null) return;
                ValidateFile(root, selected.File);
                var text = new StringBuilder();
                int first = Math.Max(1, selected.Line - 12);
                int start = 0;
                int length = 0;
                using (var reader = new StreamReader(new FileStream(selected.File, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
                {
                    string line;
                    int number = 0;
                    while ((line = reader.ReadLine()) != null)
                    {
                        number++;
                        if (number < first) continue;
                        string safe = CommandJournal.Redact(line);
                        if (safe.Length > 16000) safe = safe.Substring(0, 16000) + " [long line excerpt]";
                        string rendered = number + ": " + safe;
                        if (number == selected.Line) { start = text.Length; length = rendered.Length; }
                        text.AppendLine(rendered);
                        if (number >= selected.Line + 12) break;
                    }
                    if (number < selected.Line) throw new IOException("The saved file no longer contains line " + selected.Line + ".");
                }
                var view = new TextBox {
                    IsReadOnly = true, Text = text.ToString(), FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                    TextWrapping = TextWrapping.NoWrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
                };
                AutomationProperties.SetName(view, "Saved file excerpt at line " + selected.Line);
                view.Select(start, length);
                var panel = new DockPanel();
                var heading = new TextBox { IsReadOnly = true, Text = selected.File + Environment.NewLine + "Exact saved line " + selected.Line + " (context excerpt)", TextWrapping = TextWrapping.Wrap };
                DockPanel.SetDock(heading, Dock.Top);
                panel.Children.Add(heading);
                panel.Children.Add(view);
                if (LastSourceWindow != null) LastSourceWindow.Close();
                LastSourceWindow = new Window {
                    Owner = owner, Title = "Saved log - line " + selected.Line, Content = panel,
                    Width = 850, Height = 450, MinWidth = 400, MinHeight = 250,
                    Background = owner.Background, Foreground = owner.Foreground
                };
                foreach (ResourceDictionary dictionary in owner.Resources.MergedDictionaries)
                    LastSourceWindow.Resources.MergedDictionaries.Add(dictionary);
                LastSourceWindow.Show();
                view.Focus();
                view.ScrollToLine(selected.Line - first);
            }
            catch (Exception error) { Status.Text = "Cannot open saved line: " + CommandJournal.Redact(error.Message); }
        }

        public static void ValidateFile(string root, string file)
        {
            ValidateDirectory(root, Path.GetDirectoryName(file));
            if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Refusing linked log file: " + file);
        }

        internal static void ValidateDirectory(string root, string directory)
        {
            string basePath = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string target = Path.GetFullPath(directory);
            if (!target.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
                throw new IOException("Log path escapes checkout.");
            string current = basePath;
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Refusing linked checkout.");
            foreach (string part in target.Substring(basePath.Length).Split(Path.DirectorySeparatorChar))
            {
                current = Path.Combine(current, part);
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Refusing linked log directory: " + current);
            }
        }

        public void Dispose()
        {
            disposed = true;
            debounce.Stop();
            if (cancellation != null) { cancellation.Cancel(); cancellation.Dispose(); }
            if (LastSourceWindow != null) LastSourceWindow.Close();
        }
    }
}
