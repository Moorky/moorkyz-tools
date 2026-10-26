using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace Moorkyz.Tools.MediaRecover.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel model;
    private bool closeAfterStop;
    public MainWindow(MainViewModel model)
    {
        InitializeComponent(); this.model = model; DataContext = model;
        model.AppearanceChanged += ApplyTheme;
        model.PropertyChanged += ModelChanged;
        SystemEvents.UserPreferenceChanged += SystemPreferenceChanged;
        ApplyTheme();
    }
    private void SystemPreferenceChanged(object sender, UserPreferenceChangedEventArgs args) => Dispatcher.Invoke(ApplyTheme);
    private void ApplyTheme()
    {
        if (SystemParameters.HighContrast)
        {
            Resources["Surface"] = SystemColors.WindowBrush; Resources["Panel"] = SystemColors.WindowBrush;
            Resources["Ink"] = SystemColors.WindowTextBrush; Resources["Line"] = SystemColors.WindowTextBrush; return;
        }
        var dark = model.Settings.Theme == "Dark";
        if (model.Settings.Theme == "System")
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            dark = key?.GetValue("AppsUseLightTheme") is int light && light == 0;
        }
        Resources["Surface"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(dark ? "#171923" : "#FAFAFC"));
        Resources["Panel"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(dark ? "#252836" : "#FFFFFF"));
        Resources["Ink"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(dark ? "#EEF0F8" : "#202332"));
        Resources["Line"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(dark ? "#51566E" : "#D5D8E1"));
    }
    private void ModelChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(MainViewModel.Busy) && !model.Busy && closeAfterStop) Close();
    }
    private void OnClosing(object? sender, CancelEventArgs args)
    {
        if (!model.Busy) return;
        args.Cancel = true;
        var de = model.Settings.Language == "de";
        var answer = MessageBox.Show(de ? "Ja: sicher stoppen und beenden. Nein: im sichtbaren Taskleistenfenster weiterarbeiten. Abbrechen: geöffnet lassen." : "Yes: stop safely and exit. No: continue minimized on the visible taskbar (not tray). Cancel: keep window open.",
            "Moorkyz Media Recover", MessageBoxButton.YesNoCancel, MessageBoxImage.Question, MessageBoxResult.Cancel);
        if (answer == MessageBoxResult.Yes) { closeAfterStop = true; model.StopSafely(); }
        else if (answer == MessageBoxResult.No) WindowState = WindowState.Minimized;
    }
    private void OnClosed(object? sender, EventArgs args)
    {
        SystemEvents.UserPreferenceChanged -= SystemPreferenceChanged;
        model.AppearanceChanged -= ApplyTheme; model.PropertyChanged -= ModelChanged;
        model.Dispose();
    }
}
