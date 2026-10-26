using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace VisoryBulkDownloader.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel vm;
    private bool exiting;

    public MainWindow(MainViewModel vm)
    {
        InitializeComponent();
        this.vm = vm;
        DataContext = vm;
        Icon = WindowIcon();
        ApplyLanguage();
        Loc.Instance.PropertyChanged += (_, _) => ApplyLanguage();
        vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(MainViewModel.SelectedPage)) AnimatePage(); };
        Closing += OnClosing;
    }

    private void ApplyLanguage() =>
        Language = System.Windows.Markup.XmlLanguage.GetLanguage(Loc.Instance.Language == "de" ? "de-DE" : "en-US");

    public static ImageSource? WindowIcon()
    {
        try { return BitmapFrame.Create(new Uri("pack://application:,,,/app.ico")); }
        catch (IOException) { return null; }
    }

    private void AnimatePage()
    {
        if (!ThemeManager.AnimationsEnabled)
        {
            PageHost.Opacity = 1;
            PageOffset.Y = 0;
            return;
        }
        var duration = TimeSpan.FromMilliseconds(180);
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        PageHost.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, duration) { EasingFunction = ease });
        PageOffset.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(8, 0, duration) { EasingFunction = ease });
    }

    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        if (exiting || vm.IsExiting) return;
        e.Cancel = true;
        if (vm.IsRunning && vm.Settings.MinimizeToTrayOnClose)
        {
            Hide();
            if (!vm.Settings.CloseBehaviorExplained)
            {
                vm.Tray?.Notify(Loc.T("AppName"), Loc.T("TrayStillRunning") + " " + Loc.T("CloseExplained"));
                vm.MarkCloseExplained();
            }
            return;
        }
        if (vm.Settings.ScheduleEnabled && !vm.IsRunning)
        {
            Hide();
            return;
        }
        exiting = true;
        exiting = await vm.ExitAsync(confirm: true);
    }
}
