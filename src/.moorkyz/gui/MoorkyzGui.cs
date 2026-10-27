using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Moorkyz
{
    public sealed class ControlGui
    {
        private const int OutputLimit = 120000;
        private readonly string root;
        private readonly string entryPoint;
        private readonly string nodePath;
        private readonly List<Button> motionControls = new List<Button>();
        private readonly List<MaterialCard> materialCards = new List<MaterialCard>();
        private readonly Dictionary<Button, string> actions = new Dictionary<Button, string>();
        private readonly object outputLock = new object();
        private readonly StringBuilder pendingOutput = new StringBuilder();
        private readonly StringBuilder recentErrors = new StringBuilder();
        private readonly TextBox output;
        private readonly TextBlock runStatus;
        private readonly CheckBox motionToggle;
        private readonly ComboBox projectSelector;
        private readonly WrapPanel cards;
        private readonly TimeSpan duration;
        private readonly TimeSpan bounceDuration;
        private bool outputScheduled;
        private bool closed;
        private Point dragOrigin;
        private Button dragSource;
        private DragPreview dragPreview;
        public Window Window { get; private set; }
        public bool IsBusy { get; private set; }
        public bool MotionEnabled { get { return motionToggle.IsChecked == true && SystemParameters.ClientAreaAnimation; } }
        public int HoverPreset { get; private set; }
        public int PressPreset { get; private set; }
        public int DragPreset { get; private set; }
        public int MovePreset { get; private set; }
        public int LastExitCode { get; private set; }

        public ControlGui(string source, string root, string entryPoint, string nodePath, string[] projects, string defaultProject)
        {
            this.root = System.IO.Path.GetFullPath(root);
            this.entryPoint = System.IO.Path.GetFullPath(entryPoint);
            this.nodePath = System.IO.Path.GetFullPath(nodePath);
            if (!File.Exists(this.entryPoint) || !File.Exists(this.nodePath))
                throw new FileNotFoundException("The selected CLI entrypoint or Node runtime is missing.");
            using (var stream = File.OpenRead(System.IO.Path.Combine(source, "MainWindow.xaml")))
                Window = (Window)XamlReader.Load(stream);
            using (var stream = File.OpenRead(System.IO.Path.Combine(source, "Theme.xaml")))
                Window.Resources.MergedDictionaries.Add((ResourceDictionary)XamlReader.Load(stream));
            duration = ((Duration)Window.FindResource("Motion.Duration")).TimeSpan;
            bounceDuration = ((Duration)Window.FindResource("Motion.BounceDuration")).TimeSpan;
            output = Find<TextBox>("Output");
            runStatus = Find<TextBlock>("RunStatus");
            motionToggle = Find<CheckBox>("MotionToggle");
            projectSelector = Find<ComboBox>("ProjectSelector");
            cards = Find<WrapPanel>("ActionCards");
            var icon = new BitmapImage(new Uri(System.IO.Path.GetFullPath(System.IO.Path.Combine(source, "..", "public", "favicon.ico"))));
            icon.Freeze();
            Window.Icon = icon;
            Find<Image>("BrandIcon").Source = new BitmapImage(new Uri(System.IO.Path.GetFullPath(System.IO.Path.Combine(source, "..", "public", "android-chrome-192x192.png"))));
            Find<TextBlock>("RootLabel").Text = this.root;
            AutomationProperties.SetName(Window, "Moorkyz local control");
            foreach (string project in projects)
            {
                if (!Regex.IsMatch(project, @"^[a-zA-Z][a-zA-Z0-9_-]{0,50}$") || project == "all")
                    throw new ArgumentException("Invalid configured project name: " + project);
                projectSelector.Items.Add(project);
            }
            projectSelector.SelectedItem = defaultProject;
            Find<TextBlock>("ProjectHint").Text = projects.Length == 0 ? "No projects are configured." : "Choose the project before running an action.";
            projectSelector.SelectionChanged += delegate { UpdateActions(); };
            AddAction("Start", "Launch the configured dev service.", "start", true);
            AddAction("Status", "Inspect tracked processes and URLs.", "status", false);
            AddAction("Restart", "Replace this CLI's managed service.", "restart", false);
            AddAction("Stop", "Stop only this CLI's verified service.", "stop", false);
            AddAction("Logs", "Read the latest 80 service log lines.", "logs", false);
            AddAction("Doctor", "Check local setup. Installs nothing.", "doctor", false);
            AddAction("Build", "Run the existing build script.", "build", false);
            AddAction("Test", "Run the existing test script.", "test", false);
            AddAction("Lint", "Run the existing lint script.", "lint", false);
            AddAction("Typecheck", "Run the existing typecheck script.", "typecheck", false);
            var overviewNav = Find<Button>("OverviewNav");
            var effectsNav = Find<Button>("EffectsNav");
            AttachMotion(overviewNav);
            AttachMotion(effectsNav);
            var brandHome = Find<Button>("BrandHome");
            AttachMotion(brandHome);
            brandHome.Click += delegate { ShowView(false); };
            overviewNav.Click += delegate { ShowView(false); };
            effectsNav.Click += delegate { ShowView(true); };
            AddSelector("HOVER", new[] { "Scale + lift", "Violet glow", "Surface light" }, delegate(int value) { HoverPreset = value; ResetMotion(); });
            AddSelector("PRESS", new[] { "Spring bounce", "Ripple", "Inset pulse" }, delegate(int value) { PressPreset = value; ResetMotion(); });
            AddSelector("DRAG", new[] { "Lift", "Ghost", "Outline" }, delegate(int value) { DragPreset = value; });
            AddSelector("MOVE", new[] { "Slide + fade", "Reposition", "Soft settle" }, delegate(int value) { MovePreset = value; });
            AddSample("Primary action", "PrimaryButton");
            AddSample("Secondary action", "Button");
            AddSample("Replay transition", "QuietButton");
            AddSample("\uE713", "IconButton", "Settings icon preview");
            AddSample("LOCAL", "BadgeButton", "Local badge preview");
            AddMaterial("MIDNIGHT / MICROTEXTURE", Color.FromRgb(27, 54, 106));
            AddMaterial("VIOLET / EMBOSSED LIGHT", Color.FromRgb(66, 38, 119));
            motionToggle.Checked += delegate { UpdateMotion(); };
            motionToggle.Unchecked += delegate { UpdateMotion(); };
            Window.Closing += OnClosing;
            Window.Closed += delegate
            {
                closed = true;
                ResetMotion();
                if (dragPreview != null) dragPreview.Dispose();
                SystemParameters.StaticPropertyChanged -= OnSystemSettingChanged;
            };
            SystemParameters.StaticPropertyChanged += OnSystemSettingChanged;
            Window.PreviewKeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.Key == Key.Escape)
                {
                    if (projectSelector.IsDropDownOpen) projectSelector.IsDropDownOpen = false;
                }
            };
            UpdateActions();
            UpdateMotion();
        }

        public T Find<T>(string name) where T : FrameworkElement
        {
            var element = Window.FindName(name) as T;
            if (element == null) throw new InvalidOperationException("Missing GUI component: " + name);
            return element;
        }

        private Button NewButton(string text, string style)
        {
            var button = new Button { Content = text, Style = (Style)Window.FindResource(style) };
            AttachMotion(button);
            return button;
        }

        private void AddAction(string title, string description, string command, bool primary)
        {
            var button = NewButton(title, "ActionCard");
            if (primary) button.BorderBrush = (Brush)Window.FindResource("Accent");
            var content = new StackPanel();
            content.Children.Add(new TextBlock { Text = title, FontSize = 18, FontWeight = FontWeights.SemiBold });
            content.Children.Add(new TextBlock { Text = description, Style = (Style)Window.FindResource("MutedText"), FontSize = 12, Margin = new Thickness(0, 7, 0, 10) });
            content.Children.Add(new TextBlock { Text = command.ToUpperInvariant() + "  /  DRAG TO MOVE", FontSize = 9, Foreground = (Brush)Window.FindResource("Focus") });
            button.Content = content;
            button.ToolTip = description + " Reorder with drag or Alt + arrow keys.";
            AutomationProperties.SetName(button, title);
            AutomationProperties.SetHelpText(button, description + " Alt + arrow keys reorders this card.");
            actions.Add(button, command);
            button.Click += delegate { RunCommand(command); };
            button.AllowDrop = true;
            button.PreviewMouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
            {
                dragOrigin = e.GetPosition(Window);
                dragSource = button;
            };
            button.PreviewMouseMove += OnDragMove;
            button.DragOver += delegate(object sender, DragEventArgs e)
            {
                if (!IsOwnDrag(e)) return;
                button.BorderBrush = (Brush)Window.FindResource("Focus");
                e.Effects = DragDropEffects.Move;
                e.Handled = true;
            };
            button.DragLeave += delegate { button.ClearValue(Control.BorderBrushProperty); };
            button.Drop += delegate(object sender, DragEventArgs e)
            {
                if (!IsOwnDrag(e)) return;
                var sourceButton = (Button)e.Data.GetData("Moorkyz.ActionCard");
                Reorder(sourceButton, cards.Children.IndexOf(button));
                e.Handled = true;
                button.ClearValue(Control.BorderBrushProperty);
            };
            button.PreviewKeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (Keyboard.Modifiers != ModifierKeys.Alt) return;
                var key = e.Key == Key.System ? e.SystemKey : e.Key;
                int direction = key == Key.Left || key == Key.Up ? -1 : key == Key.Right || key == Key.Down ? 1 : 0;
                if (direction == 0) return;
                Reorder(button, cards.Children.IndexOf(button) + direction);
                e.Handled = true;
            };
            cards.Children.Add(button);
        }

        private bool IsOwnDrag(DragEventArgs e)
        {
            var sourceButton = e.Data.GetData("Moorkyz.ActionCard") as Button;
            return !IsBusy && sourceButton != null && actions.ContainsKey(sourceButton);
        }

        private void OnDragMove(object sender, MouseEventArgs e)
        {
            var button = (Button)sender;
            if (IsBusy || dragSource != button || e.LeftButton != MouseButtonState.Pressed) return;
            Point current = e.GetPosition(Window);
            if (Math.Abs(current.X - dragOrigin.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(current.Y - dragOrigin.Y) < SystemParameters.MinimumVerticalDragDistance) return;
            dragSource = null;
            Mouse.Capture(null);
            ResetMotion();
            if (MotionEnabled)
            {
                var host = (UIElement)Window.Content;
                var layer = AdornerLayer.GetAdornerLayer(host);
                if (layer != null)
                    dragPreview = new DragPreview(host, button, e.GetPosition(button), layer);
                var transform = (TransformGroup)button.RenderTransform;
                if (DragPreset == 0)
                {
                    ((ScaleTransform)transform.Children[0]).ScaleX = 1.035;
                    ((ScaleTransform)transform.Children[0]).ScaleY = 1.035;
                    ((DropShadowEffect)button.Effect).Opacity = 0.5;
                }
                else if (DragPreset == 1) button.Opacity = 0.35;
                else button.BorderBrush = (Brush)Window.FindResource("Focus");
            }
            try { DragDrop.DoDragDrop(button, new DataObject("Moorkyz.ActionCard", button), DragDropEffects.Move); }
            finally
            {
                if (dragPreview != null) dragPreview.Dispose();
                dragPreview = null;
                ResetMotion();
            }
            e.Handled = true;
        }

        public void Reorder(Button button, int destination)
        {
            if (IsBusy || !actions.ContainsKey(button)) return;
            int source = cards.Children.IndexOf(button);
            if (destination < 0 || destination >= cards.Children.Count || destination == source) return;
            var previous = new Dictionary<Button, Point>();
            foreach (Button card in cards.Children) previous[card] = card.TranslatePoint(new Point(), cards);
            cards.Children.Remove(button);
            cards.Children.Insert(destination, button);
            cards.UpdateLayout();
            foreach (Button card in cards.Children)
            {
                Point next = card.TranslatePoint(new Point(), cards);
                AnimateMove(card, previous[card].X - next.X, previous[card].Y - next.Y);
            }
            button.Focus();
            runStatus.Text = "Moved " + actions[button] + " to position " + (destination + 1) + ".";
        }

        private void AddSelector(string label, string[] values, Action<int> changed)
        {
            var panel = new StackPanel { Width = 170 };
            panel.Children.Add(new TextBlock { Text = label, Style = (Style)Window.FindResource("Eyebrow") });
            var combo = new ComboBox { ItemsSource = values, SelectedIndex = 0 };
            AutomationProperties.SetName(combo, label.ToLowerInvariant() + " effect");
            combo.SelectionChanged += delegate { changed(combo.SelectedIndex); };
            panel.Children.Add(combo);
            var border = new Border { Style = (Style)Window.FindResource("Panel"), Padding = new Thickness(14), Margin = new Thickness(0, 0, 14, 14), Child = panel };
            Find<WrapPanel>("PresetSelectors").Children.Add(border);
        }

        private void AddSample(string title, string style, string name = null)
        {
            var button = NewButton(title, style);
            AutomationProperties.SetName(button, name ?? title);
            button.Margin = new Thickness(0, 0, 12, 12);
            button.Click += delegate
            {
                Find<TextBlock>("PreviewStatus").Text = "Preview only - no command was run.";
                if (title == "Replay transition") AnimateMove(Find<Border>("MotionPreview"), 0, 16);
            };
            Find<WrapPanel>("SampleButtons").Children.Add(button);
        }

        private void AddMaterial(string label, Color accent)
        {
            var card = new MaterialCard(label, accent, duration) { Style = (Style)Window.FindResource("Button") };
            AutomationProperties.SetName(card, label + " material preview");
            card.ToolTip = "Move the pointer for tilt and sunlight. Click for spring feedback. Preview only.";
            AttachMotion(card);
            card.Click += delegate { Find<TextBlock>("PreviewStatus").Text = "Material preview - no command was run."; };
            materialCards.Add(card);
            Find<WrapPanel>("MaterialCards").Children.Add(card);
        }

        public void ShowView(bool effects)
        {
            Find<ScrollViewer>("Overview").Visibility = effects ? Visibility.Collapsed : Visibility.Visible;
            Find<ScrollViewer>("Effects").Visibility = effects ? Visibility.Visible : Visibility.Collapsed;
            Find<Button>("OverviewNav").Style = (Style)Window.FindResource(effects ? "QuietButton" : "PrimaryButton");
            Find<Button>("EffectsNav").Style = (Style)Window.FindResource(effects ? "PrimaryButton" : "QuietButton");
            AnimateMove(effects ? (FrameworkElement)Find<ScrollViewer>("Effects") : Find<ScrollViewer>("Overview"), 0, 12);
        }

        private void AttachMotion(Button button)
        {
            var transforms = new TransformGroup();
            transforms.Children.Add(new ScaleTransform(1, 1));
            transforms.Children.Add(new TranslateTransform());
            button.RenderTransform = transforms;
            button.Effect = new DropShadowEffect { Color = (Color)Window.FindResource("Color.Accent"), BlurRadius = 20, ShadowDepth = 0, Opacity = 0 };
            motionControls.Add(button);
            button.MouseEnter += delegate
            {
                if (!MotionEnabled) return;
                if (HoverPreset == 0)
                {
                    var transform = (TransformGroup)button.RenderTransform;
                    Animate(transform.Children[0], ScaleTransform.ScaleXProperty, 1.025);
                    Animate(transform.Children[0], ScaleTransform.ScaleYProperty, 1.025);
                    Animate(transform.Children[1], TranslateTransform.YProperty, -2);
                }
                else if (HoverPreset == 1) Animate(button.Effect, DropShadowEffect.OpacityProperty, 0.55);
                else
                {
                    var original = button.Background as SolidColorBrush;
                    if (original == null) return;
                    var brush = original.Clone();
                    button.Background = brush;
                    brush.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation((Color)Window.FindResource("Color.Border"), duration));
                }
            };
            button.MouseLeave += delegate { if (dragPreview == null) ResetButton(button); };
            button.PreviewMouseLeftButtonDown += delegate { Press(button); };
            button.PreviewMouseLeftButtonUp += delegate { ReleaseButton(button); };
            button.PreviewKeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.Key == Key.Space || e.Key == Key.Enter) Press(button);
            };
            button.PreviewKeyUp += delegate(object sender, KeyEventArgs e)
            {
                if (e.Key == Key.Space || e.Key == Key.Enter) ReleaseButton(button);
            };
            button.LostKeyboardFocus += delegate { ResetButton(button); };
        }

        private void Press(Button button)
        {
            if (!MotionEnabled || !button.IsEnabled) return;
            var scale = ((TransformGroup)button.RenderTransform).Children[0];
            if (PressPreset == 0)
            {
                Animate(scale, ScaleTransform.ScaleXProperty, 0.965);
                Animate(scale, ScaleTransform.ScaleYProperty, 0.965);
            }
            else if (PressPreset == 1)
            {
                button.ApplyTemplate();
                var ripple = button.Template.FindName("Ripple", button) as Ellipse;
                if (ripple == null) return;
                if (ripple.RenderTransform.IsFrozen) ripple.RenderTransform = ripple.RenderTransform.Clone();
                var rippleScale = (ScaleTransform)ripple.RenderTransform;
                rippleScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.1, 1.6, duration));
                rippleScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.1, 1.6, duration));
                ripple.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.4, 0, duration));
            }
            else
            {
                Animate(button, UIElement.OpacityProperty, 0.65);
                Animate(scale, ScaleTransform.ScaleYProperty, 0.975);
            }
        }

        private void Animate(DependencyObject target, DependencyProperty property, double value)
        {
            var animation = new DoubleAnimation(value, duration) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            var animatable = target as System.Windows.Media.Animation.Animatable;
            if (animatable != null) animatable.BeginAnimation(property, animation);
            else ((UIElement)target).BeginAnimation(property, animation);
        }

        private void ResetButton(Button button)
        {
            var transform = (TransformGroup)button.RenderTransform;
            foreach (var child in transform.Children)
            {
                if (child is ScaleTransform)
                {
                    ((ScaleTransform)child).BeginAnimation(ScaleTransform.ScaleXProperty, null);
                    ((ScaleTransform)child).BeginAnimation(ScaleTransform.ScaleYProperty, null);
                    ((ScaleTransform)child).ScaleX = ((ScaleTransform)child).ScaleY = 1;
                }
                else
                {
                    ((TranslateTransform)child).BeginAnimation(TranslateTransform.XProperty, null);
                    ((TranslateTransform)child).BeginAnimation(TranslateTransform.YProperty, null);
                    ((TranslateTransform)child).X = ((TranslateTransform)child).Y = 0;
                }
            }
            button.BeginAnimation(UIElement.OpacityProperty, null);
            button.ClearValue(UIElement.OpacityProperty);
            ((DropShadowEffect)button.Effect).BeginAnimation(DropShadowEffect.OpacityProperty, null);
            ((DropShadowEffect)button.Effect).Opacity = 0;
            button.ClearValue(Control.BorderBrushProperty);
            button.ClearValue(Control.BackgroundProperty);
            if (actions.ContainsKey(button) && actions[button] == "start") button.BorderBrush = (Brush)Window.FindResource("Accent");
            if (button.Template != null)
            {
                var ripple = button.Template.FindName("Ripple", button) as Ellipse;
                if (ripple != null)
                {
                    ripple.BeginAnimation(UIElement.OpacityProperty, null);
                    ripple.Opacity = 0;
                    var rippleScale = (ScaleTransform)ripple.RenderTransform;
                    if (!rippleScale.IsFrozen)
                    {
                        rippleScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                        rippleScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                        rippleScale.ScaleX = rippleScale.ScaleY = 0.1;
                    }
                }
            }
        }

        private void ReleaseButton(Button button)
        {
            var scale = (ScaleTransform)((TransformGroup)button.RenderTransform).Children[0];
            double x = scale.ScaleX;
            double y = scale.ScaleY;
            ResetButton(button);
            if (!MotionEnabled || PressPreset == 1) return;
            var spring = new ElasticEase { Oscillations = 2, Springiness = 5, EasingMode = EasingMode.EaseOut };
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(x, 1, bounceDuration) { EasingFunction = spring });
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(y, 1, bounceDuration) { EasingFunction = spring });
        }

        private void ResetMotion()
        {
            foreach (Button button in motionControls) ResetButton(button);
        }

        private void AnimateMove(FrameworkElement element, double x, double y)
        {
            if (!MotionEnabled) return;
            TransformGroup group = element.RenderTransform as TransformGroup;
            TranslateTransform translate;
            if (group == null)
            {
                translate = element.RenderTransform as TranslateTransform;
                if (translate == null) element.RenderTransform = translate = new TranslateTransform();
            }
            else translate = (TranslateTransform)group.Children[1];
            IEasingFunction easing = MovePreset == 2
                ? (IEasingFunction)new BackEase { Amplitude = 0.3, EasingMode = EasingMode.EaseOut }
                : new CubicEase { EasingMode = EasingMode.EaseOut };
            translate.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(x, 0, duration) { EasingFunction = easing });
            translate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(y, 0, duration) { EasingFunction = easing });
            if (MovePreset == 0) element.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.5, 1, duration));
        }

        private void UpdateMotion()
        {
            motionToggle.IsEnabled = SystemParameters.ClientAreaAnimation;
            foreach (MaterialCard card in materialCards) card.SetMotionEnabled(MotionEnabled);
            Find<TextBlock>("MotionStatus").Text = !SystemParameters.ClientAreaAnimation ? "Windows animations are off." : MotionEnabled ? "Subtle / " + duration.TotalMilliseconds + " ms" : "Reduced motion";
            if (!MotionEnabled)
            {
                ResetMotion();
                if (dragPreview != null) dragPreview.Dispose();
                foreach (string name in new[] { "Overview", "Effects", "MotionPreview" })
                {
                    var element = Find<FrameworkElement>(name);
                    element.BeginAnimation(UIElement.OpacityProperty, null);
                    element.RenderTransform = Transform.Identity;
                }
            }
        }

        private void OnSystemSettingChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "ClientAreaAnimation" && !closed)
                Window.Dispatcher.BeginInvoke(new Action(UpdateMotion));
        }

        private void UpdateActions()
        {
            bool selected = projectSelector.SelectedItem != null;
            foreach (Button button in actions.Keys) button.IsEnabled = !IsBusy && selected;
            projectSelector.IsEnabled = !IsBusy;
        }

        public static string QuoteArgument(string value)
        {
            return "\"" + Regex.Replace(value, @"(\\*)(""|$)", delegate(Match match)
            {
                return match.Groups[1].Value + match.Groups[1].Value +
                    (match.Groups[2].Value == "\"" ? "\\\"" : "");
            }) + "\"";
        }

        public bool RunCommand(string command)
        {
            if (IsBusy) return false;
            if (!actions.ContainsValue(command)) throw new ArgumentException("Unsupported GUI action: " + command);
            if (projectSelector.SelectedItem == null)
            {
                runStatus.Text = "Choose a project before running an action.";
                return false;
            }
            string project = (string)projectSelector.SelectedItem;
            IsBusy = true;
            LastExitCode = -1;
            UpdateActions();
            ResetMotion();
            output.Clear();
            lock (outputLock) recentErrors.Clear();
            output.AppendText("> " + System.IO.Path.GetFileNameWithoutExtension(entryPoint) + " " + command + " " + project + Environment.NewLine);
            output.BringIntoView();
            runStatus.Text = "Running " + command + " for " + project + "...";
            Task.Factory.StartNew(delegate
            {
                int code;
                try
                {
                    var start = new ProcessStartInfo(nodePath, QuoteArgument(entryPoint) + " " + command + " " + project)
                    {
                        WorkingDirectory = root,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8
                    };
                    using (var process = new Process { StartInfo = start })
                    {
                        process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e) { if (e.Data != null) QueueOutput(e.Data); };
                        process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e) { if (e.Data != null) QueueOutput(e.Data, true); };
                        process.Start();
                        process.BeginOutputReadLine();
                        process.BeginErrorReadLine();
                        process.WaitForExit();
                        code = process.ExitCode;
                    }
                }
                catch (Exception error)
                {
                    QueueOutput("Could not run command: " + error.Message, true);
                    code = 1;
                }
                Window.Dispatcher.BeginInvoke(new Action(delegate
                {
                    if (code != 0)
                    {
                        string errors;
                        lock (outputLock) errors = recentErrors.ToString();
                        if (errors.Length > 0) QueueOutput("Recent stderr:" + Environment.NewLine + errors);
                    }
                    FlushOutput();
                    LastExitCode = code;
                    IsBusy = false;
                    runStatus.Text = command + (code == 0 ? " completed" : " failed") + " (exit " + code + ").";
                    runStatus.Foreground = (Brush)Window.FindResource(code == 0 ? "Text" : "Focus");
                    UpdateActions();
                }));
            });
            return true;
        }

        private void QueueOutput(string line, bool error = false)
        {
            lock (outputLock)
            {
                if (error)
                {
                    recentErrors.AppendLine(line);
                    if (recentErrors.Length > 8000) recentErrors.Remove(0, recentErrors.Length - 8000);
                }
                pendingOutput.AppendLine(line);
                if (pendingOutput.Length > OutputLimit) pendingOutput.Remove(0, pendingOutput.Length - OutputLimit);
                if (outputScheduled) return;
                outputScheduled = true;
            }
            Window.Dispatcher.BeginInvoke(new Action(FlushOutput));
        }

        private void FlushOutput()
        {
            string text;
            lock (outputLock)
            {
                text = pendingOutput.ToString();
                pendingOutput.Clear();
                outputScheduled = false;
            }
            output.AppendText(text);
            if (output.Text.Length > OutputLimit)
            {
                string notice = "[Earlier output omitted from this window]" + Environment.NewLine;
                output.Text = notice + output.Text.Substring(output.Text.Length - OutputLimit + notice.Length);
            }
            output.ScrollToEnd();
        }

        private void OnClosing(object sender, CancelEventArgs e)
        {
            if (!IsBusy) return;
            e.Cancel = true;
            runStatus.Text = "A command is still running. Wait for it to finish before closing.";
        }
    }
}
