param([string]$ScreenshotDirectory, [string]$LauncherExe)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase, System.Xaml, UIAutomationTypes, UIAutomationProvider, UIAutomationClient
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..'))
$source = Join-Path $root 'src\.moorkyz\gui'
$references = @(
    [System.Windows.Window].Assembly.Location,
    [System.Windows.Media.Brush].Assembly.Location,
    [System.Windows.Threading.Dispatcher].Assembly.Location,
    [System.Xaml.XamlReader].Assembly.Location,
    [System.Xml.XmlReader].Assembly.Location
)
$sources = @('Motion.cs', 'TerminalSearch.cs', 'ReportView.cs', 'CommandJournal.cs', 'MoorkyzGui.cs') | ForEach-Object { Join-Path $source $_ }
Add-Type -Path $sources -ReferencedAssemblies $references
$fixture = Join-Path ([IO.Path]::GetTempPath()) ('moorkyz gui fixture ' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixture | Out-Null
$gui = $null
$secondGui = $null

function Assert-True([bool]$condition, [string]$message) {
    if (-not $condition) { throw $message }
}

function Pump([int]$milliseconds = 80) {
    $clock = [Diagnostics.Stopwatch]::StartNew()
    while ($clock.ElapsedMilliseconds -lt $milliseconds) {
        [System.Windows.Threading.Dispatcher]::CurrentDispatcher.Invoke(
            [Action]{}, [System.Windows.Threading.DispatcherPriority]::Background
        )
        Start-Sleep -Milliseconds 10
    }
}

function Wait-Command {
    $clock = [Diagnostics.Stopwatch]::StartNew()
    while ($gui.IsBusy) {
        if ($clock.ElapsedMilliseconds -gt 10000) { throw 'Fixture command timed out.' }
        Pump 20
    }
    Pump
}

function Search-Terminal($terminal, [string]$query) {
    $terminal.Query.Text = $query
    $terminal.SearchNow()
    $clock = [Diagnostics.Stopwatch]::StartNew()
    while ($terminal.Status.Text -eq 'Searching saved history...') {
        if ($clock.ElapsedMilliseconds -gt 10000) { throw 'Terminal search timed out.' }
        Pump 20
    }
}

function Read-Reports($view, [string]$query = '') {
    $view.Query.Text = $query
    $view.Refresh()
    $clock = [Diagnostics.Stopwatch]::StartNew()
    while ($view.Status.Text -eq 'Loading reports...') {
        if ($clock.ElapsedMilliseconds -gt 10000) { throw 'Report loading timed out.' }
        Pump 20
    }
}

function Press-SearchKey($terminal, [System.Windows.Input.Key]$key) {
    $event = [System.Windows.Input.KeyEventArgs]::new(
        [System.Windows.Input.Keyboard]::PrimaryDevice,
        [System.Windows.PresentationSource]::FromVisual($gui.Window), 0, $key
    )
    $event.RoutedEvent = [System.Windows.Input.Keyboard]::PreviewKeyDownEvent
    $terminal.Query.RaiseEvent($event)
}

function Save-View([string]$name) {
    if (-not $ScreenshotDirectory) { return }
    New-Item -ItemType Directory -Force -Path $ScreenshotDirectory | Out-Null
    $content = $gui.Window.Content
    $bitmap = [System.Windows.Media.Imaging.RenderTargetBitmap]::new(
        [int]($content.ActualWidth + $content.Margin.Left + $content.Margin.Right),
        [int]($content.ActualHeight + $content.Margin.Top + $content.Margin.Bottom),
        96, 96, [System.Windows.Media.PixelFormats]::Pbgra32
    )
    $bitmap.Render($gui.Window)
    $encoder = [System.Windows.Media.Imaging.PngBitmapEncoder]::new()
    $encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $stream = [IO.File]::Create((Join-Path $ScreenshotDirectory $name))
    try { $encoder.Save($stream) } finally { $stream.Dispose() }
}

try {
    $cli = Join-Path $fixture "cli & ' fixture.cjs"
    [IO.File]::WriteAllText($cli, @'
"use strict";
const [command, project] = process.argv.slice(2);
setTimeout(() => {
  console.log(JSON.stringify({ command, project, cwd: process.cwd(), headless: process.env.MOORKYZ_HEADLESS }));
  console.log("[TRACE] Synthetic trace");
  console.log("[WARN] Synthetic warning");
  if (command === "test") {
    console.log("x".repeat(125000));
    console.error("Synthetic failure");
    console.log("[ERROR] Synthetic structured error");
    console.error('password=fixture-secret https://user:pass@example.invalid/?token=fixture-token');
    process.exitCode = 3;
  }
}, 180);
'@)
    $gui = [Moorkyz.ControlGui]::new($source, $fixture, $cli, (Get-Command node.exe).Source, [string[]]@('demo-project', 'second'), 'demo-project')
    $gui.Window.Show()
    Pump 200
    $motionMilliseconds = $gui.Window.FindResource('Motion.Duration').TimeSpan.TotalMilliseconds
    $bounceMilliseconds = $gui.Window.FindResource('Motion.BounceDuration').TimeSpan.TotalMilliseconds
    $entrancesField = $gui.GetType().GetField('entrancePanels', [Reflection.BindingFlags]'Instance,NonPublic')
    $initialEntrances = $entrancesField.GetValue($gui)
    if ($gui.MotionEnabled) {
        Assert-True ($initialEntrances.Count -eq 11 -and $initialEntrances[0].Opacity -gt $initialEntrances[10].Opacity) 'The initial Overview must stagger its project panel and every action card too.'
    }
    Assert-True ($motionMilliseconds -eq 320 -and $bounceMilliseconds -eq 560) 'Visual feedback must use the calmer transition durations, independently of command execution.'
    $cards = $gui.Window.FindName('ActionCards')
    $output = $gui.Window.FindName('Output')
    $status = $gui.Window.FindName('RunStatus')
    $errors = $gui.Window.FindName('Errors')
    $progress = $gui.Window.FindName('CommandProgress')
    Assert-True (-not $gui.IsBusy) 'Opening the GUI must not run a command.'
    Assert-True ((Get-ChildItem -LiteralPath $fixture).Count -eq 1) 'Opening the GUI must not write workspace files.'
    $reportView = $gui.Window.FindName('ReportsHost').Content
    $gui.ShowReports()
    Pump 45
    if ($gui.MotionEnabled) {
        Assert-True ($reportView.Diagnostics.Opacity -gt $reportView.Statistics.Opacity -and $reportView.Statistics.Opacity -eq 0) 'First visits must reveal panels sequentially, not all at once.'
    }
    $gui.Window.FindName('MotionToggle').IsChecked = $false
    Assert-True ($reportView.Diagnostics.Opacity -eq 1 -and $reportView.Statistics.Opacity -eq 1 -and $reportView.Diagnostics.RenderTransform.Y -eq 0) 'Reduced motion must immediately settle every staggered panel, including delayed panels.'
    $gui.Window.FindName('MotionToggle').IsChecked = $true
    Read-Reports $reportView
    $reportView.Status.Text = 'Same-page navigation sentinel'
    $gui.ShowReports()
    Assert-True ($reportView.Status.Text -ceq 'Same-page navigation sentinel') 'Selecting the current Reports page must not reload history.'
    Assert-True ($reportView.Results.Items.Count -eq 0 -and $reportView.StatisticText.Text -match 'averages unavailable') 'Empty report history must show an explicit no-samples state.'
    Assert-True ((Get-ChildItem -LiteralPath $fixture).Count -eq 1) 'Reading an empty Reports page must not create files.'
    Pump
    Assert-True ($reportView.Diagnostics.IsVisible -and $reportView.Statistics.IsVisible) 'Report summaries must always be visible without hide/show controls.'
    $diagnosticPosition = $reportView.Diagnostics.TranslatePoint([System.Windows.Point]::new(0,0), $reportView)
    $statisticPosition = $reportView.Statistics.TranslatePoint([System.Windows.Point]::new(0,0), $reportView)
    Assert-True ($diagnosticPosition.Y -eq $statisticPosition.Y -and $statisticPosition.X -gt $diagnosticPosition.X) 'Wide windows must show compact summaries side by side.'
    $originalWidth = $gui.Window.Width
    $gui.Window.Width = 940
    Pump
    $diagnosticPosition = $reportView.Diagnostics.TranslatePoint([System.Windows.Point]::new(0,0), $reportView)
    $statisticPosition = $reportView.Statistics.TranslatePoint([System.Windows.Point]::new(0,0), $reportView)
    Assert-True ($statisticPosition.Y -gt $diagnosticPosition.Y -and $statisticPosition.X -eq $diagnosticPosition.X) 'Narrow windows must stack summaries without clipping their text.'
    $gui.Window.Width = $originalWidth
    Assert-True ($cards.Children.Count -eq 10) 'Expected ten real CLI action cards.'
    Assert-True ($gui.Window.Icon -ne $null) 'Expected the supplied Moorkyz window icon.'
    foreach ($name in @('Output', 'Errors', 'ReportPath')) {
        Assert-True ($gui.Window.FindName($name).IsReadOnly) 'All diagnostic text panels must be read only.'
    }
    $gui.ShowDiagnostics()
    Pump 220
    foreach ($section in @('Overview', 'Effects', 'Diagnostics', 'Reports')) {
        switch ($section) {
            'Overview' { $gui.ShowView($false) }
            'Effects' { $gui.ShowView($true) }
            'Diagnostics' { $gui.ShowDiagnostics() }
            'Reports' { $gui.ShowReports() }
        }
        $sectionScroll = $gui.Window.FindName($section)
        $sectionScroll.MaxHeight = 160
        Pump
        $sectionScroll.ScrollToEnd()
        Pump
        Assert-True ($sectionScroll.VerticalOffset -gt 0) 'Scroll-reset coverage must actually scroll each section before leaving.'
        if ($section -eq 'Overview') { $gui.ShowDiagnostics() } else { $gui.ShowView($false) }
        switch ($section) {
            'Overview' { $gui.ShowView($false) }
            'Effects' { $gui.ShowView($true) }
            'Diagnostics' { $gui.ShowDiagnostics() }
            'Reports' { $gui.ShowReports() }
        }
        Pump
        Assert-True ($sectionScroll.VerticalOffset -eq 0) 'Returning to every section must reset its scroll position to the top.'
        Assert-True ($entrancesField.GetValue($gui).Count -eq 0) 'Returning to a visited section must not replay its first-visit panel sequence.'
        $sectionScroll.MaxHeight = [double]::PositiveInfinity
    }
    $gui.ShowDiagnostics()
    Pump 400
    $logsSearch = $gui.Window.FindName('LogsSearchHost').Content
    $errorsSearch = $gui.Window.FindName('ErrorsSearchHost').Content
    Assert-True ($logsSearch.GetType() -eq $errorsSearch.GetType()) 'Logs and errors must use the same search component.'
    Assert-True ($gui.Window.FindName('SearchHitLimit').SelectedItem -eq 5) 'The default search hit limit must be exactly five.'
    Assert-True (-not $logsSearch.Query.IsReadOnly -and -not $errorsSearch.Query.IsReadOnly) 'Search queries are editable controls; the terminal content remains read-only.'
    Assert-True ([System.Windows.Automation.AutomationProperties]::GetName($logsSearch.Query) -eq 'Search logs') 'Each query needs an explicit accessible name.'
    Assert-True ($gui.Window.FindName('ErrorElapsedStatus').Text -match 'unavailable - no errors' -and $gui.Window.FindName('ErrorAverageStatus').Text -match 'needs two errors') 'An empty error history must not display invented current or average intervals.'
    foreach ($level in @('TRACE', 'DEBUG', 'INFO', 'SUCCESS', 'WARN', 'WARNING', 'ERROR', 'FATAL', 'CRITICAL', 'PASS', 'FAIL')) {
        Assert-True ([Moorkyz.LogRecord]::DetectLevel("[$level] synthetic", $false) -eq $level) 'Supported structured levels must be recognized without executing or parsing message code.'
    }
    Assert-True ([Moorkyz.LogRecord]::DetectLevel('[TRACE] stderr', $true) -eq 'ERROR') 'stderr must remain an error even if its message claims a lower severity.'
    Search-Terminal $logsSearch 'missing'
    Assert-True ($logsSearch.ResultCount -eq 0 -and $logsSearch.Status.Text -eq 'No matching saved lines.') 'Empty history must show an explicit zero-result state.'
    $logsSearch.Query.Clear()
    Assert-True ($gui.Window.FindName('Diagnostics').Visibility -eq 'Visible') 'Logs and Errors must share one accessible tab.'
    $logsPane = $gui.Window.FindName('LogsPane')
    $errorsPane = $gui.Window.FindName('ErrorsPane')
    $initialLogsHeight = $logsPane.ActualHeight
    $logsHandle = $gui.Window.FindName('LogsHandle')
    $errorsHandle = $gui.Window.FindName('ErrorsHandle')
    foreach ($handle in @($logsHandle, $errorsHandle)) {
        $peer = [System.Windows.Automation.Peers.ButtonAutomationPeer]::new($handle)
        Assert-True ($peer.GetPattern([System.Windows.Automation.Peers.PatternInterface]::Invoke) -ne $null) 'Terminal handles must expose native keyboard/automation invocation.'
    }
    $output.Focus() | Out-Null
    $gui.SetTerminalVisible($false, $false)
    if ($gui.MotionEnabled) {
        Pump 45
        Assert-True ($logsPane.Height -gt 0 -and $logsPane.Height -lt $initialLogsHeight) 'Hiding must smoothly reduce the entire terminal and search layout height.'
        Assert-True ($logsPane.Opacity -gt 0 -and $logsPane.Opacity -lt 1) 'Hiding must fade the terminal.'
        Assert-True ($logsPane.RenderTransform.Y -gt 0) 'Hiding must slide the terminal.'
    }
    Pump ($motionMilliseconds + 60)
    Assert-True ($logsPane.Visibility -eq 'Collapsed') 'Hidden terminals must release layout space.'
    Assert-True ($logsHandle.IsKeyboardFocusWithin) 'Hiding a focused terminal must move focus to its persistent handle.'
    Assert-True ([System.Windows.Automation.AutomationProperties]::GetName($logsHandle) -eq 'Show logs') 'The handle must announce its current action.'
    Assert-True ($errorsPane.Visibility -eq 'Visible') 'Log and error panes must collapse independently.'
    $gui.SetTerminalVisible($false, $true)
    Pump ($motionMilliseconds + 60)
    Assert-True ([double]::IsNaN($logsPane.Height) -and $logsPane.ActualHeight -eq $initialLogsHeight -and $logsPane.Visibility -eq 'Visible') 'Showing must restore natural terminal/search height and allow responsive result expansion.'
    $gui.SetTerminalVisible($true, $false)
    $gui.SetTerminalVisible($true, $true)
    $gui.SetTerminalVisible($true, $false)
    Pump ($motionMilliseconds + 60)
    Assert-True ($errorsPane.Visibility -eq 'Collapsed') 'Rapid toggle completion must follow the latest requested state.'
    $gui.SetTerminalVisible($true, $true)
    Pump 30
    $gui.Window.FindName('MotionToggle').IsChecked = $false
    Assert-True ($errorsPane.Visibility -eq 'Visible' -and $errorsPane.Opacity -eq 1 -and [double]::IsNaN($errorsPane.Height)) 'Reduced motion must settle an active transition immediately at its natural content height.'
    $gui.SetTerminalVisible($true, $false)
    Assert-True ($errorsPane.Visibility -eq 'Collapsed' -and $errorsPane.Height -eq 0) 'Reduced-motion collapse must be immediate.'
    $gui.SetTerminalVisible($true, $true)
    $gui.Window.FindName('MotionToggle').IsChecked = $true
    foreach ($card in $cards.Children) {
        Assert-True ($card.Style -eq $gui.Window.FindResource('ActionCard')) 'Action cards must share the same component style.'
        Assert-True (-not [string]::IsNullOrWhiteSpace([System.Windows.Automation.AutomationProperties]::GetName($card))) 'Every action must have an accessible name.'
        $peer = [System.Windows.Automation.Peers.ButtonAutomationPeer]::new($card)
        Assert-True ($peer.GetPattern([System.Windows.Automation.Peers.PatternInterface]::Invoke) -ne $null) 'Buttons must expose native keyboard/automation invocation.'
    }
    $selectors = $gui.Window.FindName('PresetSelectors')
    Assert-True ($selectors.Children.Count -eq 4) 'Expected four reusable effect families.'
    foreach ($selector in $selectors.Children) {
        $combo = $selector.Child.Children[1]
        Assert-True ($combo.Items.Count -eq 3) 'Each interaction family must offer exactly three presets.'
        foreach ($index in 0..2) { $combo.SelectedIndex = $index; Pump 10 }
        $combo.SelectedIndex = 0
    }
    $gui.ShowView($true)
    $sample = $gui.Window.FindName('SampleButtons').Children[0]
    $flags = [Reflection.BindingFlags]'Instance,NonPublic'
    $press = $gui.GetType().GetMethod('Press', $flags)
    $release = $gui.GetType().GetMethod('ReleaseButton', $flags)
    $reset = $gui.GetType().GetMethod('ResetButton', $flags)
    foreach ($index in 0..2) {
        $selectors.Children[0].Child.Children[1].SelectedIndex = $index
        $enter = [System.Windows.Input.MouseEventArgs]::new([System.Windows.Input.Mouse]::PrimaryDevice, 0)
        $enter.RoutedEvent = [System.Windows.Input.Mouse]::MouseEnterEvent
        $sample.RaiseEvent($enter)
        Pump ($motionMilliseconds + 60)
        if ($gui.MotionEnabled) {
            if ($index -eq 0) { Assert-True ($sample.RenderTransform.Children[0].ScaleX -gt 1.02) 'Hover must slightly scale up the shared control.' }
            if ($index -eq 1) { Assert-True ($sample.Effect.Opacity -gt 0.5) 'The glow preset must visibly light the control.' }
            if ($index -eq 2) {
                $color = $sample.Background.Color
                Assert-True ($color.R -eq 44 -and $color.G -eq 59 -and $color.B -eq 96) 'The surface preset must reach the shared highlight color.'
            }
        }
        $reset.Invoke($gui, @($sample)) | Out-Null
        $selectors.Children[1].Child.Children[1].SelectedIndex = $index
        $press.Invoke($gui, @($sample)) | Out-Null
        Pump 45
        if ($gui.MotionEnabled) {
            if ($index -eq 0) { Assert-True ($sample.RenderTransform.Children[0].ScaleX -lt 1) 'The press preset must compress before bouncing back.' }
            if ($index -eq 1) { Assert-True ($sample.Template.FindName('Ripple', $sample).Opacity -gt 0) 'The ripple preset must animate its own bounded layer.' }
            if ($index -eq 2) { Assert-True ($sample.Opacity -lt 1) 'The inset preset must give visible press feedback.' }
        }
        $release.Invoke($gui, @($sample)) | Out-Null
        $peak = 1.0
        foreach ($frame in 1..22) {
            Pump 30
            $peak = [Math]::Max($peak, $sample.RenderTransform.Children[0].ScaleX)
        }
        if ($gui.MotionEnabled -and $index -eq 0) { Assert-True ($peak -gt 1.0001) 'The spring click must visibly overshoot before settling.' }
        Assert-True ([Math]::Abs($sample.RenderTransform.Children[0].ScaleX - 1) -lt 0.001) 'Press feedback must settle at its normal size.'
    }
    $selectors.Children[0].Child.Children[1].SelectedIndex = 0
    $selectors.Children[1].Child.Children[1].SelectedIndex = 0
    Pump 200
    Save-View 'moorkyz-interaction-lab.png'
    $material = $gui.Window.FindName('MaterialCards').Children[0]
    $material.SetMotionEnabled($true)
    $material.SetPointer(1, 0)
    Pump 240
    Assert-True ($material.TiltX -gt 0 -and $material.TiltY -gt 0) 'Material samples must use real 3D rotations.'
    Assert-True ($material.Content.ActualWidth -gt 300 -and $material.Content.ActualHeight -gt 180) 'Material content must occupy the actual surface, not a zero-size 3D viewport.'
    $material.BringIntoView()
    Pump
    Save-View 'moorkyz-materials.png'
    $material.SetMotionEnabled($false)
    Assert-True ($material.TiltX -eq 0 -and $material.TiltY -eq 0) 'Disabling motion must remove active 3D animation.'
    $toggle = $gui.Window.FindName('MotionToggle')
    $selectors.Children[1].Child.Children[1].SelectedIndex = 1
    $press.Invoke($gui, @($sample)) | Out-Null
    Pump 30
    $toggle.IsChecked = $false
    Assert-True (-not $gui.MotionEnabled) 'The reduced-motion switch must disable nonessential movement.'
    foreach ($card in $cards.Children) {
        Assert-True ($card.RenderTransform.Children[0].ScaleX -eq 1) 'Reduced motion must reset scaling.'
        Assert-True (-not $card.RenderTransform.Children[0].HasAnimatedProperties) 'Reduced motion must remove active animation clocks.'
    }
    Assert-True (-not $sample.Template.FindName('Ripple', $sample).RenderTransform.HasAnimatedProperties) 'Reduced motion must remove active ripple clocks too.'
    $gui.ShowView($false)
    $first = $cards.Children[0]
    $hostElement = [System.Windows.UIElement]$gui.Window.Content
    $layer = [System.Windows.Documents.AdornerLayer]::GetAdornerLayer($hostElement)
    Assert-True ($layer -ne $null) 'The native drag preview must have an adorner surface.'
    $previewType = [Moorkyz.ControlGui].Assembly.GetType('Moorkyz.DragPreview')
    $preview = [Activator]::CreateInstance($previewType, [object[]]@($hostElement, $first, [System.Windows.Point]::new(10, 10), $layer))
    Assert-True ($layer.GetAdorners($hostElement).Count -eq 1) 'Dragging must attach a real cursor-following preview.'
    Pump 60
    $preview.Dispose()
    $preview.Dispose()
    Assert-True ($layer.GetAdorners($hostElement).Count -eq 0) 'Drag cleanup must remove the preview and be safe to repeat.'
    $gui.Reorder($first, 2)
    Assert-True ($cards.Children[2] -eq $first) 'Shared pointer/keyboard reordering must move the intended card.'
    Assert-True ($status.Text -match 'position 3') 'Reordering must announce its result.'
    $gui.Reorder($first, 0)
    $gui.Reorder($first, -1)
    Assert-True ($cards.Children[0] -eq $first) 'Out-of-range reorders must not alter the order.'
    $project = $gui.Window.FindName('ProjectSelector')
    $project.SelectedIndex = -1
    Assert-True (-not $first.IsEnabled) 'Actions must be disabled until a project is selected.'
    Assert-True (-not $gui.RunCommand('status')) 'No project must never execute a command.'
    $project.SelectedItem = 'demo-project'
    $rejected = $false
    try { $gui.RunCommand('install') | Out-Null } catch { $rejected = $true }
    Assert-True $rejected 'The GUI must reject commands outside its fixed action allowlist.'
    $first.RaiseEvent([System.Windows.RoutedEventArgs]::new([System.Windows.Controls.Button]::ClickEvent))
    Assert-True $gui.IsBusy 'Clicking a real action must execute the CLI asynchronously.'
    Assert-True ($progress.IsIndeterminate -eq $gui.MotionEnabled) 'Unknown command progress must use activity, never a fabricated percentage, respecting reduced motion.'
    $gui.SetTerminalVisible($false, $false)
    $gui.SetTerminalVisible($true, $false)
    Assert-True (-not $gui.RunCommand('status')) 'Overlapping commands must be prevented.'
    $gui.Window.Close()
    Assert-True $gui.Window.IsVisible 'Closing must not abandon an in-flight command.'
    $gui.Window.WindowState = [System.Windows.WindowState]::Minimized
    Pump 40
    $gui.Window.WindowState = [System.Windows.WindowState]::Normal
    $gui.Window.Activate() | Out-Null
    Wait-Command
    Assert-True ($gui.LastExitCode -eq 0) 'The fixture command should succeed.'
    Assert-True (-not $progress.IsIndeterminate -and $progress.Value -eq 1) 'Successful completion must settle progress.'
    Assert-True (Test-Path -LiteralPath $gui.LastReportPath) 'Every completed command must have a durable report.'
    $firstReportPath = $gui.LastReportPath
    [xml]$firstReport = [IO.File]::ReadAllText($firstReportPath)
    Assert-True ([double]$firstReport.commandReport.durationSeconds -gt 0) 'Reports must record measured elapsed time.'
    $duration = [double]::Parse($firstReport.commandReport.durationSeconds, [Globalization.CultureInfo]::InvariantCulture)
    $foreground = [double]::Parse($firstReport.commandReport.uiSeconds, [Globalization.CultureInfo]::InvariantCulture)
    $background = [double]::Parse($firstReport.commandReport.backgroundSeconds, [Globalization.CultureInfo]::InvariantCulture)
    Assert-True ($foreground -ge 0 -and $background -gt 0 -and [Math]::Abs($foreground + $background - $duration) -lt 0.000001) 'Measured foreground/background time must be nonnegative and sum exactly to task duration; minimizing must record background time.'
    Assert-True ($firstReport.commandReport.status -eq 'PASS') 'Successful reports must preserve the outcome.'
    Assert-True ($gui.Window.FindName('ElapsedStatus').Text -match '^Time spent: [\d.]+ s$') 'Only current task time must be displayed, with one adaptive unit and no session total.'
    foreach ($case in @(
        @{ Seconds = 0; Expected = '0 s' },
        @{ Seconds = 119.99; Expected = '119.9 s' },
        @{ Seconds = 120; Expected = '2 m' },
        @{ Seconds = 7199; Expected = '119.9 m' },
        @{ Seconds = 7200; Expected = '2 h' },
        @{ Seconds = 431999; Expected = '119.9 h' },
        @{ Seconds = 432000; Expected = '5 d' }
    )) {
        Assert-True ([Moorkyz.DiagnosticPresentation]::Duration($case.Seconds) -ceq $case.Expected) 'Durations must promote at exactly 120 of the current unit, with one unit only.'
    }
    foreach ($invalidSeconds in @(-1, [double]::NaN, [double]::PositiveInfinity)) {
        $rejectedTime = $false
        try { [Moorkyz.DiagnosticPresentation]::Duration($invalidSeconds) | Out-Null } catch { $rejectedTime = $true }
        Assert-True $rejectedTime 'Invalid durations must throw explicitly rather than display a success-shaped zero.'
    }
    Assert-True ($output.Text -match '"command":"start","project":"demo-project"') 'The exact command and project must reach the CLI without shell interpretation.'
    Assert-True ($output.Text.Contains($fixture.Replace('\', '\\'))) 'The process must execute in the selected workspace.'
    $project.SelectedItem = 'second'
    Assert-True ($gui.RunCommand('test')) 'The second command should start.'
    Wait-Command
    Assert-True ($gui.LastExitCode -eq 3) 'The actual CLI failure exit code must be retained.'
    Assert-True ($status.Text -match 'failed.*exit 3') 'The GUI must explicitly report a failed command.'
    Assert-True ($output.Text -match 'Synthetic failure') 'The GUI must show stderr.'
    Assert-True ($errors.Text -match 'Synthetic failure') 'The separate read-only error pane must receive stderr even when hidden.'
    Assert-True ($errorsPane.Visibility -eq 'Collapsed') 'Errors must not override the user hidden-pane preference.'
    Assert-True ($progress.Value -eq 0 -and -not $progress.IsIndeterminate) 'Failed commands must not show successful completion progress.'
    Assert-True ($output.Text.Length -le 120000) 'Visible command output, including its truncation notice, must stay within 120000 characters.'
    Assert-True ($first.IsEnabled) 'Actions must recover after failure.'
    [xml]$failedReport = [IO.File]::ReadAllText($gui.LastReportPath)
    Assert-True ($failedReport.commandReport.exitCode -eq '3' -and $failedReport.commandReport.status -eq 'FAIL') 'Failure reports must retain the actual exit code.'
    $fullLogs = [IO.File]::ReadAllText((Join-Path (Split-Path -Parent $gui.LastReportPath) 'logs.log'))
    $fullErrors = [IO.File]::ReadAllText((Join-Path (Split-Path -Parent $gui.LastReportPath) 'errors.log'))
    Assert-True ($fullLogs.Contains(('x' * 125000))) 'Full logs must preserve output beyond the 120000-character preview limit.'
    Assert-True ($fullLogs -match '\[\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\] \[ERROR\] \[[a-f0-9]{8}:L\d+\] Synthetic failure') 'Saved logs must use UTC timestamps, bracketed levels and a compact source reference.'
    Assert-True ($gui.Window.FindName('ErrorElapsedStatus').Text -match '^Time since last error: [\d.]+ s$') 'Errors must show current time since the last error first.'
    Assert-True ($gui.Window.FindName('ErrorAverageStatus').Text -match '^Average interval: [\d.]+ s \(this session\)$') 'Errors must then show measured average intervals, scoped explicitly to this session.'
    $binding = [Reflection.BindingFlags]'Instance,NonPublic'
    $intervalSum = [double]$gui.GetType().GetField('errorIntervalSeconds', $binding).GetValue($gui)
    $intervalCount = [int]$gui.GetType().GetField('errorIntervalCount', $binding).GetValue($gui)
    $expectedInterval = [Moorkyz.DiagnosticPresentation]::Duration($intervalSum / $intervalCount)
    Assert-True ($gui.Window.FindName('ErrorAverageStatus').Text -ceq "Average interval: $expectedInterval (this session)") 'Error averages must derive from measured intervals and use the exact same unit formatter as task durations.'
    Assert-True ($gui.Window.FindName('RunStatus').Foreground -eq $gui.Window.FindResource('Diagnostic.Error')) 'Failed outcomes must be explicitly red while retaining the failure text and exit code.'
    Assert-True ($failedReport.commandReport.errorCount -eq '3' -and $failedReport.commandReport.errorIntervalCount -eq '2') 'Reports must preserve measured error counts and interval sample counts, including structured stdout errors.'
    Assert-True ([double]$failedReport.commandReport.averageErrorIntervalSeconds -ge 0) 'Report error averages must be nonnegative measured durations.'
    Assert-True ($fullErrors -match 'Synthetic failure') 'All stderr must be retained separately on disk.'
    Assert-True ($fullLogs -notmatch 'fixture-secret|fixture-token|user:pass' -and $errors.Text -notmatch 'fixture-secret|fixture-token') 'Secrets and credential-bearing URLs must be redacted before persistence and display.'
    Assert-True (Test-Path -LiteralPath $firstReportPath) 'Later commands must never overwrite earlier logs or reports.'
    $project.SelectedItem = 'demo-project'
    Assert-True ($gui.RunCommand('start')) 'A repeated command must run.'
    Wait-Command
    Assert-True ($gui.Window.FindName('AverageStatus').Text -match 'across 2 completed reports') 'Averages must derive from persisted reports for the same launcher, project and command.'
    Assert-True ($output.Text -match 'Synthetic failure') 'Running another command must not erase the existing terminal history.'
    Assert-True ($errors.Text -match 'Synthetic structured error') 'Recognized structured ERROR output must also reach the error terminal.'
    $runs = @($output.Document.Blocks.FirstBlock.Inlines | Where-Object { $_ -is [System.Windows.Documents.Run] })
    foreach ($level in @('TRACE', 'WARN', 'INFO', 'ERROR')) {
        $levelRun = $runs | Where-Object Text -eq "[$level] " | Select-Object -First 1
        Assert-True ($null -ne $levelRun -and $levelRun.Foreground -eq [Moorkyz.DiagnosticPresentation]::Level($gui.Window, $level)) "The real $level field must have its shared level color."
    }
    foreach ($stampRun in @($runs | Where-Object Text -Match '^\[\d{4}-\d{2}-\d{2} ')) {
        Assert-True ($stampRun.Foreground -eq $gui.Window.FindResource('Diagnostic.Timestamp')) 'Timestamps must keep one color regardless of severity.'
    }
    Assert-True ($gui.Window.FindName('RunStatus').Foreground -eq $gui.Window.FindResource('Diagnostic.Success')) 'Successful diagnostic outcomes must have their labeled success color.'
    Assert-True ($gui.Window.FindName('ReportPath').Foreground -eq $gui.Window.FindResource('Diagnostic.Success')) 'Report locations must share the actual command outcome color.'
    $gui.SetTerminalVisible($false, $true)
    $gui.SetTerminalVisible($true, $true)
    $gui.ShowDiagnostics()
    Pump 220
    Read-Reports $reportView
    Assert-True ($reportView.Results.Items.Count -eq 3 -and $reportView.StatisticText.Text -match 'PASS 2 \| FAIL 1') 'Report list and statistics must reflect actual persisted outcomes.'
    $reportItem = $reportView.Results.Items[0]
    $savedReport = $reportItem.Tag
    Assert-True ($savedReport.File -eq $gui.LastReportPath) 'Reports must be ordered by persisted UTC start, newest first.'
    Assert-True ($savedReport.Summary() -match 'Video length: Unavailable \| File size: Unavailable') 'Generic commands must not invent video duration or file size.'
    Assert-True ($savedReport.Summary() -match 'Time on UI: [\d.]+ s \| Time in background: [\d.]+ s') 'Measured window-active and background time must be visible.'
    foreach ($case in @(
        @{ Duration = 90; Baseline = 100; Success = $true; Grade = 'Faster' },
        @{ Duration = 100; Baseline = 100; Success = $true; Grade = 'Typical' },
        @{ Duration = 110; Baseline = 100; Success = $true; Grade = 'Typical' },
        @{ Duration = 125; Baseline = 100; Success = $true; Grade = 'Slower' },
        @{ Duration = 126; Baseline = 100; Success = $true; Grade = 'Much slower' },
        @{ Duration = 1; Baseline = 100; Success = $false; Grade = 'Failed' }
    )) {
        Assert-True ([Moorkyz.SavedReport]::Grade($case.Duration, $case.Baseline, $case.Success) -eq $case.Grade) 'Report performance grades must obey exact documented boundaries and never reward failed tasks.'
    }
    Assert-True ([Moorkyz.SavedReport]::Grade(10, $null, $true) -eq 'No baseline') 'No-baseline reports must remain neutral.'
    $accent = [Moorkyz.SavedReport]::Accent($gui.Window, $savedReport.Rating)
    Assert-True ($reportItem.Content.BorderBrush -eq $accent -and $reportItem.Content.Child.Inlines.LastInline.Foreground -eq $accent) 'The report message must match the overall item performance accent.'
    Read-Reports $reportView '"test" level:FAIL -absent'
    Assert-True ($reportView.Results.Items.Count -eq 1 -and $reportView.Results.Items[0].Tag.Outcome -eq 'FAIL') 'Report search must share phrases, AND, exclusion and exact level grammar.'
    Read-Reports $reportView ([IO.Path]::GetFileName((Split-Path -Parent $firstReportPath)))
    Assert-True ($reportView.Results.Items.Count -eq 1) 'Reports must be searchable by their persistent full reference ID.'
    Read-Reports $reportView '"unclosed'
    Assert-True ($reportView.Results.Items.Count -eq 0 -and $reportView.Status.Text -match 'Close the quoted') 'Malformed report queries must clear stale hits and show actionable errors.'
    Read-Reports $reportView
    $gui.ShowReports()
    Read-Reports $reportView
    Pump
    Save-View 'moorkyz-reports.png'
    $reportRoot = Split-Path -Parent (Split-Path -Parent $firstReportPath)
    foreach ($sample in @(
        @{ Id = 'rating-1'; Seconds = 100; Status = 'PASS'; Expected = 'No baseline' },
        @{ Id = 'rating-2'; Seconds = 80; Status = 'PASS'; Expected = 'Faster' },
        @{ Id = 'rating-3'; Seconds = 130; Status = 'PASS'; Expected = 'Much slower' },
        @{ Id = 'rating-4'; Seconds = 1; Status = 'FAIL'; Expected = 'Failed' },
        @{ Id = 'rating-5'; Seconds = 100; Status = 'PASS'; Expected = 'Typical' }
    )) {
        $sampleFolder = Join-Path $reportRoot $sample.Id
        New-Item -ItemType Directory -Path $sampleFolder | Out-Null
        $date = '2020-01-0' + $sample.Id.Substring(7) + 'T00:00:00.0000000Z'
        $exitCode = if ($sample.Status -eq 'PASS') { 0 } else { 1 }
        $metadata = if ($sample.Id -eq 'rating-2') { '<videoDurationSeconds>240</videoDurationSeconds><fileSizeBytes>4096</fileSizeBytes><uiSeconds>30</uiSeconds><backgroundSeconds>50</backgroundSeconds>' } else { '' }
        [IO.File]::WriteAllText((Join-Path $sampleFolder 'report.xml'), "<commandReport version=`"1`"><launcher>fixture</launcher><project>demo</project><command>rating-fixture</command><startedUtc>$date</startedUtc><exitCode>$exitCode</exitCode><status>$($sample.Status)</status><durationSeconds>$($sample.Seconds)</durationSeconds>$metadata</commandReport>")
        Read-Reports $reportView $sample.Id
        Assert-True ($reportView.Results.Items.Count -eq 1 -and $reportView.Results.Items[0].Tag.Rating -eq $sample.Expected) 'Persisted ratings must exclude their own duration, later reports, failed samples and other task histories.'
        $sampleItem = $reportView.Results.Items[0]
        Assert-True ($sampleItem.Content.BorderBrush -eq $sampleItem.Content.Child.Inlines.LastInline.Foreground) 'Every report rating must use the same accent for its row and message.'
        if ($sample.Id -eq 'rating-1') {
            Assert-True ($sampleItem.Tag.Summary() -match 'Time on UI: Unavailable \| Time in background: Unavailable') 'Older reports must remain readable without invented foreground/background measurements.'
        }
        if ($sample.Id -eq 'rating-2') {
            Assert-True ($sampleItem.Tag.Summary() -match 'Video length: 4 m \| File size: 4096 bytes') 'Supplied tool measurements must be displayed with real values, not placeholders.'
        }
    }
    $reportView.Configure(1, $false)
    Read-Reports $reportView 'rating-fixture'
    Assert-True ($reportView.Results.Items.Count -eq 1 -and $reportView.Status.Text -match 'of 5 matching') 'Report search must obey shared result limits and disclose the complete match count.'
    $reportView.Configure(5, $false)
    $reportView.Query.Text = 'rating-fixture'
    $reportView.Refresh()
    Read-Reports $reportView '"Synthetic-no-match"'
    Assert-True ($reportView.Results.Items.Count -eq 0) 'Cancelled report refreshes must not repopulate stale results.'
    $invalidReport = Join-Path $reportRoot 'invalid-report-metadata'
    New-Item -ItemType Directory -Path $invalidReport | Out-Null
    $invalidFile = Join-Path $invalidReport 'report.xml'
    $validSample = [IO.File]::ReadAllText((Join-Path $reportRoot 'rating-2\report.xml'))
    foreach ($badValue in @(
        $validSample.Replace('<fileSizeBytes>4096</fileSizeBytes>', '<fileSizeBytes>NaN</fileSizeBytes>'),
        $validSample.Replace('<backgroundSeconds>50</backgroundSeconds>', '<backgroundSeconds>51</backgroundSeconds>'),
        $validSample.Replace('<videoDurationSeconds>240</videoDurationSeconds>', '<videoDurationSeconds>-1</videoDurationSeconds>')
    )) {
        [IO.File]::WriteAllText($invalidFile, $badValue)
        Read-Reports $reportView
        Assert-True ($reportView.Results.Items.Count -eq 0 -and $reportView.Status.Text -match 'Reports failed:.*invalid-report-metadata') 'Invalid measurements must fail explicitly with the affected report path, not silently produce a partial successful list.'
        Assert-True ($reportView.StatisticText.Text -match 'unavailable') 'Invalid report history must invalidate its aggregate statistics.'
    }
    Remove-Item -LiteralPath $invalidFile
    Read-Reports $reportView
    Assert-True ($reportView.Results.Items.Count -eq 5) 'Explicit Refresh must recover after a bad report is removed.'
    Assert-True ([IO.File]::ReadAllText((Join-Path $reportRoot 'rating-2\report.xml')) -ceq $validSample) 'Report browsing, grading and searching must not rewrite persisted report bytes.'
    Pump
    Save-View 'moorkyz-reports-summary.png'
    $gui.ShowDiagnostics()
    $logBytesBeforeSearch = [IO.File]::ReadAllText((Join-Path (Split-Path -Parent $firstReportPath) 'logs.log'))
    Search-Terminal $logsSearch '"Synthetic failure" level:error'
    Assert-True ($logsSearch.ResultCount -eq 1) 'Phrase and level filters must match saved full logs, including earlier commands.'
    $logsSearch.Results.SelectedIndex = 0
    Assert-True ($logsSearch.OpenLine.Visibility -eq 'Visible' -and $logsSearch.OpenDirectory.Visibility -eq 'Visible') 'Selecting a hit must expose exact-line and directory icon buttons.'
    $selectedHit = $logsSearch.Results.SelectedItem.Tag
    $hitRuns = @($logsSearch.Results.SelectedItem.Content.Inlines | Where-Object { $_ -is [System.Windows.Documents.Run] })
    $referenceRun = $hitRuns | Where-Object Text -Match '^\[[a-f0-9]{8}:L\d+\] ' | Select-Object -First 1
    Assert-True ($null -ne $referenceRun -and $referenceRun.Foreground -eq $gui.Window.FindResource('Diagnostic.Reference')) 'Search references must retain their own shared color independently of message severity.'
    $logsSearch.OpenSelectedSource()
    Pump
    Assert-True ($logsSearch.LastSourceWindow.IsVisible) 'The source icon must open the actual saved file at its exact line.'
    $sourceBox = $logsSearch.LastSourceWindow.Content.Children[1]
    Assert-True ($sourceBox.IsReadOnly -and $sourceBox.SelectedText.StartsWith("$($selectedHit.Line): ")) 'The source viewer must select the exact physical saved line read-only.'
    Assert-True ($sourceBox.SelectedText -match 'Synthetic failure') 'The selected source line must be the search result, not a guessed line offset.'
    $logsSearch.LastSourceWindow.Close()
    Search-Terminal $errorsSearch 'SYNTHETIC failure -absent'
    Assert-True ($errorsSearch.ResultCount -eq 1) 'Terms are case-insensitive AND matches with exclusions.'
    $errorsSearch.Results.SelectedIndex = 0
    $errorHit = $errorsSearch.Results.SelectedItem.Tag
    Assert-True ([IO.Path]::GetFileName($errorHit.File) -eq 'errors.log') 'Error results must map to the error file, not combined-log line numbers.'
    $errorsSearch.OpenSelectedSource()
    Pump
    Assert-True ($errorsSearch.LastSourceWindow.Content.Children[1].SelectedText.StartsWith("$($errorHit.Line): ")) 'Error source navigation must preserve exact error-file physical line.'
    $errorsSearch.LastSourceWindow.Close()
    $logsSearch.Query.Clear()
    $logsSearch.SelectAt($output.Text.IndexOf('Synthetic failure'))
    Assert-True ($logsSearch.OpenLine.Visibility -eq 'Visible') 'Selecting a live terminal line must expose the same exact-source action.'
    $logsSearch.OpenSelectedSource()
    Pump
    Assert-True ($logsSearch.LastSourceWindow.Content.Children[1].SelectedText -match 'Synthetic failure') 'Live preview lines must retain source mapping after preview truncation and later commands.'
    $logsSearch.LastSourceWindow.Close()
    $failureRun = $output.Document.Blocks.FirstBlock.Inlines | Where-Object { $_ -is [System.Windows.Documents.Run] -and $_.Text -eq 'Synthetic failure' } | Select-Object -Last 1
    $output.Focus() | Out-Null
    $output.CaretPosition = $failureRun.ContentStart
    Pump
    $logsSearch.OpenSelectedSource()
    Pump
    Assert-True ($logsSearch.LastSourceWindow.Content.Children[1].SelectedText -match 'Synthetic failure') 'Rich-text keyboard caret offsets must still locate the exact saved line.'
    $logsSearch.LastSourceWindow.Close()
    $searchFolder = Join-Path (Split-Path -Parent (Split-Path -Parent $firstReportPath)) 'zz-search-fixture'
    New-Item -ItemType Directory -Path $searchFolder | Out-Null
    $searchFile = Join-Path $searchFolder 'logs.log'
    $searchLines = @()
    foreach ($number in 1..15) {
        $searchLines += "[2026-10-28 04:20:00] [INFO] needle test $number"
    }
    $searchLines += '[2026-10-28 04:20:00] [ERROR] needle test ignore'
    $searchLines += '[2026-10-28 04:20:00] [INFO] needle test with message text [ERROR]'
    [IO.File]::WriteAllLines($searchFile, $searchLines)
    Search-Terminal $logsSearch 'needle test -ignore'
    Assert-True ($logsSearch.ResultCount -eq 5 -and $logsSearch.Status.Text -match '5\+') 'Search must show at most five hits by default and disclose additional matches.'
    foreach ($limit in @((1..10) + 7)) {
        $documentBeforeSetting = $output.Document
        $selectionBeforeSetting = $output.Selection.Start
        $gui.Window.FindName('SearchHitLimit').SelectedItem = $limit
        Assert-True ([object]::ReferenceEquals($output.Document, $documentBeforeSetting) -and $output.Selection.Start.CompareTo($selectionBeforeSetting) -eq 0) 'Search limit settings must preserve the live document and selection instead of reloading the terminal.'
        Search-Terminal $logsSearch 'needle test -ignore'
        Assert-True ($logsSearch.ResultCount -eq $limit) 'Every selected hit limit from 1 through 10 must be enforced.'
    }
    $gui.Window.FindName('TimestampFormat').SelectedIndex = 1
    Assert-True ($output.Text -match '\[\d{2}:\d{2}:\d{2}\] \[(INFO|ERROR)\]') 'Compact timestamp settings must rerender existing terminal history.'
    Assert-True ($output.Text -notmatch '\[\d{4}-\d{2}-\d{2} \d{2}:') 'Compact preview must not retain old full timestamps.'
    Search-Terminal $logsSearch 'needle level:info -ignore'
    Assert-True ($logsSearch.ResultCount -eq 7) 'Level filters must compose with other terms and configured result limits.'
    $hitContent = $logsSearch.Results.Items[0].Content
    $hitText = [System.Windows.Documents.TextRange]::new($hitContent.ContentStart, $hitContent.ContentEnd).Text
    Assert-True ($hitText -match '\[04:20:00\] \[INFO\]') "Search hit timestamps must follow the same display setting. Actual: $hitText"
    Press-SearchKey $logsSearch Enter
    Assert-True ($logsSearch.Results.SelectedIndex -eq 0) 'Enter must select the first hit when none is selected.'
    Press-SearchKey $logsSearch Enter
    Assert-True ($logsSearch.Results.SelectedIndex -eq 1) 'Enter must advance through keyboard-accessible search hits.'
    Press-SearchKey $logsSearch Escape
    Assert-True ($logsSearch.Query.Text.Length -eq 0 -and $logsSearch.ResultCount -eq 0) 'Escape must clear search and hide its results.'
    Search-Terminal $logsSearch 'needle level:error'
    Assert-True ($logsSearch.ResultCount -eq 1 -and $logsSearch.Results.Items[0].Tag.Line -eq 16) 'Level filters must inspect the level field, not matching bracketed message content.'
    Search-Terminal $logsSearch '"unclosed'
    Assert-True ($logsSearch.ResultCount -eq 0 -and $logsSearch.Status.Text -match 'Close the quoted') 'Invalid queries must report actionable syntax errors without stale matches.'
    Search-Terminal $logsSearch 'level:'
    Assert-True ($logsSearch.Status.Text -match 'Enter a level') 'Empty level filters must be diagnosed.'
    Search-Terminal $logsSearch '[INFO]'
    Assert-True ($logsSearch.ResultCount -gt 0) 'Search punctuation must be literal, not an unsafe regex.'
    Search-Terminal $logsSearch 'needle -test'
    Assert-True ($logsSearch.ResultCount -eq 0) 'Exclusions must reject otherwise matching lines.'
    $logsSearch.Query.Text = 'needle'
    $logsSearch.SearchNow()
    $logsSearch.Query.Text = '"Synthetic failure"'
    $logsSearch.SearchNow()
    Pump 300
    Assert-True ($logsSearch.ResultCount -eq 1 -and $logsSearch.Results.Items[0].Tag.Message -match 'Synthetic failure') 'Cancelled stale searches must not replace the latest query results.'
    $logsSearch.Query.Clear()
    $errorsSearch.Query.Clear()
    Assert-True ($logsSearch.ResultCount -eq 0 -and $logsSearch.Results.Visibility -eq 'Collapsed') 'Clearing search must restore the empty search state without altering terminal history.'
    Assert-True ([IO.File]::ReadAllText((Join-Path (Split-Path -Parent $firstReportPath) 'logs.log')) -ceq $logBytesBeforeSearch) 'Searching and timestamp settings must never rewrite saved log bytes.'
    $gui.Window.FindName('TimestampFormat').SelectedIndex = 0
    Assert-True ($output.Text -match '\[\d{4}-\d{2}-\d{2} \d{2}:') 'Switching back must restore full date-time display.'
    $gui.Window.FindName('TimestampFormat').SelectedIndex = 1
    Pump
    Save-View 'moorkyz-logs-errors.png'
    $errors.BringIntoView()
    Pump
    Save-View 'moorkyz-error-statistics.png'
    Search-Terminal $logsSearch 'needle test'
    $logsSearch.Results.SelectedIndex = 0
    Pump
    Assert-True ($logsSearch.Results.Background -eq $gui.Window.FindResource('Background')) 'Search results must use the shared dark palette, not an unreadable system-white surface.'
    Assert-True ($logsSearch.Results.Items[0].BorderBrush -eq $gui.Window.FindResource('Focus')) 'Selected search hits need a visible themed selection outline.'
    Save-View 'moorkyz-search-results.png'
    $sourceBeforeRemoval = $logsSearch.LastSourceWindow
    Remove-Item -LiteralPath $searchFile
    $logsSearch.OpenSelectedSource()
    Assert-True ($logsSearch.Status.Text -match 'Cannot open saved line:' -and $logsSearch.LastSourceWindow -eq $sourceBeforeRemoval) 'Missing source files must produce an explicit error, never a fabricated source view.'
    $logsSearch.Query.Clear()
    $invalidRoot = Join-Path $fixture 'invalid-settings'
    $invalidSettingsFolder = Join-Path $invalidRoot '.moorkyz\control-center'
    New-Item -ItemType Directory -Path $invalidSettingsFolder | Out-Null
    [IO.File]::WriteAllText((Join-Path $invalidSettingsFolder 'settings.xml'), '<diagnostics><hits>11</hits><compact>false</compact></diagnostics>')
    $invalidRejected = $false
    $settingsFailure = ''
    try { [Moorkyz.ControlGui]::new($source, $invalidRoot, $cli, (Get-Command node.exe).Source, [string[]]@('demo-project'), 'demo-project') | Out-Null } catch {
        $settingsFailure = $_.Exception.ToString()
        $invalidRejected = $settingsFailure -match 'Invalid diagnostic settings'
    }
    Assert-True $invalidRejected "Out-of-range persisted hit limits must fail explicitly, not silently clamp or default. $settingsFailure"
    $gui.ShowView($false)
    $gui.Window.FindName('Overview').ScrollToTop()
    Pump
    Save-View 'moorkyz-overview.png'
    $gui.Window.Width = 940
    $gui.Window.Height = 680
    Pump 100
    foreach ($card in $cards.Children) {
        $point = $card.TranslatePoint([System.Windows.Point]::new(0, 0), $cards)
        Assert-True ($point.X -ge -1 -and $point.X + $card.ActualWidth -le $cards.ActualWidth + 1) 'Action cards must wrap without horizontal clipping at minimum window width.'
    }
    $gui.ShowView($true)
    Pump 100
    $materials = $gui.Window.FindName('MaterialCards')
    foreach ($card in $materials.Children) {
        $point = $card.TranslatePoint([System.Windows.Point]::new(0, 0), $materials)
        Assert-True ($point.X -ge -1 -and $point.X + $card.ActualWidth -le $materials.ActualWidth + 1) '3D materials must wrap without horizontal clipping at minimum width.'
    }
    $gui.ShowDiagnostics()
    Pump 220
    $diagnostics = $gui.Window.FindName('Diagnostics')
    Assert-True ($diagnostics.ExtentWidth -le $diagnostics.ViewportWidth + 1) 'Diagnostic panes must reflow without horizontal page scrolling at minimum size.'
    $spring = [Moorkyz.InertialPoint]::new([System.Windows.Point]::new(0, 0))
    $spring.Step([System.Windows.Point]::new(100, 0), 1.0 / 60)
    Assert-True ($spring.Position.X -gt 0 -and $spring.Position.X -lt 100) 'Drag inertia must lag the pointer instead of teleporting.'
    $velocity = $spring.Velocity.X
    $spring.Step([System.Windows.Point]::new(-100, 0), 1.0 / 60)
    Assert-True ($spring.Velocity.X -lt $velocity) 'A direction change must brake the existing velocity.'
    foreach ($index in 1..300) { $spring.Step([System.Windows.Point]::new(-100, 0), 1.0 / 60) }
    Assert-True ([Math]::Abs($spring.Position.X + 100) -lt 0.01) 'Drag inertia must settle at the target.'
    Assert-True ([Math]::Abs($spring.Velocity.X) -lt 0.01) 'Drag inertia must stop moving after settling.'
    foreach ($case in @(
        @{ Value = 'plain'; Quoted = '"plain"' },
        @{ Value = 'space value'; Quoted = '"space value"' },
        @{ Value = 'C:\fixture\'; Quoted = '"C:\fixture\\"' },
        @{ Value = 'a"b'; Quoted = '"a\"b"' },
        @{ Value = "apostrophe's & file"; Quoted = '"apostrophe''s & file"' }
    )) {
        Assert-True ([Moorkyz.ControlGui]::QuoteArgument($case.Value) -ceq $case.Quoted) 'Process arguments must retain the exact Windows escaping shape.'
    }
    $gui.Window.Close()
    $secondGui = [Moorkyz.ControlGui]::new($source, $fixture, $cli, (Get-Command node.exe).Source, [string[]]@('demo-project'), 'demo-project')
    $secondGui.Window.Show()
    Assert-True ($secondGui.Window.FindName('SearchHitLimit').SelectedItem -eq 7 -and $secondGui.Window.FindName('TimestampFormat').SelectedIndex -eq 1) 'Search hit and timestamp settings must persist across sessions.'
    Assert-True ($secondGui.RunCommand('start')) 'A new control-center session must be able to run the same command.'
    $clock = [Diagnostics.Stopwatch]::StartNew()
    while ($secondGui.IsBusy) {
        if ($clock.ElapsedMilliseconds -gt 10000) { throw 'Second session timed out.' }
        Pump 20
    }
    Assert-True ($secondGui.Window.FindName('AverageStatus').Text -match 'across 3 completed reports') 'Report-derived averages must survive control-center sessions.'
    $sum = 0.0
    foreach ($reportFile in Get-ChildItem -LiteralPath (Split-Path -Parent (Split-Path -Parent $secondGui.LastReportPath)) -Filter report.xml -Recurse -File) {
        [xml]$record = [IO.File]::ReadAllText($reportFile.FullName)
        if ($record.commandReport.command -eq 'start' -and $record.commandReport.project -eq 'demo-project') {
            $sum += [double]::Parse($record.commandReport.durationSeconds, [Globalization.CultureInfo]::InvariantCulture)
        }
    }
    $expectedAverage = [Moorkyz.DiagnosticPresentation]::Duration($sum / 3)
    Assert-True ($secondGui.Window.FindName('AverageStatus').Text.Contains(": $expectedAverage across 3")) 'The displayed average must equal the actual persisted durations, formatted with the same single-unit rules.'
    $reportRoot = Split-Path -Parent (Split-Path -Parent $secondGui.LastReportPath)
    $malformed = Join-Path $reportRoot 'malformed-report-fixture'
    New-Item -ItemType Directory -Path $malformed | Out-Null
    [IO.File]::WriteAllText((Join-Path $malformed 'report.xml'), '<commandReport/>')
    Assert-True ($secondGui.RunCommand('start')) 'The fixture command must still be able to start before report aggregation.'
    $clock = [Diagnostics.Stopwatch]::StartNew()
    while ($secondGui.IsBusy) {
        if ($clock.ElapsedMilliseconds -gt 10000) { throw 'Report-failure fixture timed out.' }
        Pump 20
    }
    Assert-True ($secondGui.LastExitCode -eq 1) 'A malformed report must not silently produce a successful logging result.'
    Assert-True ($secondGui.Window.FindName('RunStatus').Text -match 'logging/report FAILED') 'Report failures must be explicit in run status.'
    Assert-True ($secondGui.Window.FindName('Errors').Text -match 'Full logging/report failed') 'Report failures must appear in the error terminal.'
    Assert-True ($secondGui.Window.FindName('AverageStatus').Text -match 'unavailable') 'An invalid history must not produce a made-up average.'
    Assert-True (Test-Path -LiteralPath $secondGui.LastReportPath) 'A valid current report must not be deleted because historical aggregation failed.'
    $secondReports = $secondGui.Window.FindName('ReportsHost').Content
    Read-Reports $secondReports
    Assert-True ($secondReports.Status.Text -match 'Reports failed:.*malformed-report-fixture') 'The report browser must expose malformed historical XML instead of hiding the bad row.'
    $secondGui.Window.Close()
    $secondGui = $null
    $blocked = Join-Path $fixture 'blocked-logging'
    New-Item -ItemType Directory -Path $blocked | Out-Null
    [IO.File]::WriteAllText((Join-Path $blocked '.moorkyz'), 'Not a directory; never replace this fixture.')
    $secondGui = [Moorkyz.ControlGui]::new($source, $blocked, $cli, (Get-Command node.exe).Source, [string[]]@('demo-project'), 'demo-project')
    $secondGui.Window.Show()
    Assert-True (-not $secondGui.RunCommand('start')) 'A log setup failure must prevent command execution.'
    Pump
    Assert-True ($secondGui.LastExitCode -eq 1 -and $secondGui.Window.FindName('Errors').Text -match 'Logging setup failed') 'Log setup failure must be explicit, not a success-shaped fallback.'
    $secondGui.Window.FindName('SearchHitLimit').SelectedItem = 8
    Pump
    Assert-True ($secondGui.Window.FindName('SearchHitLimit').SelectedItem -eq 5) 'A failed settings save must restore the prior value.'
    Assert-True ($secondGui.Window.FindName('Errors').Text -match 'Settings failed') 'Settings write failures must appear in the error terminal.'
    Assert-True ([IO.File]::ReadAllText((Join-Path $blocked '.moorkyz')) -match 'never replace') 'Logging must not overwrite an unexpected file.'
    $secondGui.Window.Close()
    $secondGui = $null
    $outside = Join-Path $fixture 'linked-report-target'
    $linkedRoot = Join-Path $fixture 'linked-root'
    New-Item -ItemType Directory -Path $outside, $linkedRoot | Out-Null
    New-Item -ItemType Junction -Path (Join-Path $linkedRoot '.moorkyz') -Target $outside | Out-Null
    $secondGui = [Moorkyz.ControlGui]::new($source, $linkedRoot, $cli, (Get-Command node.exe).Source, [string[]]@('demo-project'), 'demo-project')
    $secondGui.Window.Show()
    Assert-True (-not $secondGui.RunCommand('start')) 'Logging must refuse junction destinations.'
    Pump
    Assert-True (@(Get-ChildItem -LiteralPath $outside -Force).Count -eq 0) 'No files may be written through a log junction.'
    $linkedSearch = $secondGui.Window.FindName('LogsSearchHost').Content
    New-Item -ItemType Directory -Path (Join-Path $outside 'control-center\runs') | Out-Null
    Search-Terminal $linkedSearch 'needle'
    Assert-True ($linkedSearch.Status.Text -match 'Search failed:.*linked') 'Search must explicitly refuse junction history instead of reading outside the checkout.'
    $linkedReports = $secondGui.Window.FindName('ReportsHost').Content
    Read-Reports $linkedReports
    Assert-True ($linkedReports.Status.Text -match 'Reports failed:.*linked') 'Reports must refuse linked history directories before reading any external XML.'
    $secondGui.Window.Close()
    $secondGui = $null
    $autoSuccessRoot = Join-Path $fixture 'successful-auto-startup'
    New-Item -ItemType Directory -Path $autoSuccessRoot | Out-Null
    $secondGui = [Moorkyz.ControlGui]::new($source, $autoSuccessRoot, $cli, (Get-Command node.exe).Source, [string[]]@('demo-project'), 'demo-project', $true)
    Assert-True (-not $secondGui.Window.FindName('ActionCards').Children[0].IsEnabled) 'GUI-first project actions must be gated until startup has passed.'
    $secondGui.Window.Show()
    Pump 20
    $clock = [Diagnostics.Stopwatch]::StartNew()
    while ($secondGui.IsBusy -or -not $secondGui.LastReportPath) {
        if ($clock.ElapsedMilliseconds -gt 10000) { throw 'Automatic successful startup fixture timed out.' }
        Pump 20
    }
    Assert-True ($secondGui.LastExitCode -eq 0 -and $secondGui.Window.FindName('ActionCards').Children[0].IsEnabled) 'Passing GUI startup must enable project actions.'
    Assert-True ($secondGui.Window.FindName('Output').Text -match '"command":"__startup".*"headless":"1"') 'GUI children must explicitly use the headless worker to prevent recursive windows.'
    Assert-True ($secondGui.Window.FindName('StartupRetry').Visibility -eq 'Collapsed') 'Successful startup must not show a failure-retry control.'
    $secondGui.Window.Close()
    $secondGui = $null
    $autoRoot = Join-Path $fixture 'gui-first'
    $autoSource = Join-Path $autoRoot 'src'
    New-Item -ItemType Directory -Path $autoSource | Out-Null
    [IO.File]::WriteAllText((Join-Path $autoSource 'package.json'), '{}')
    $core = Join-Path $root 'src\.moorkyz\scripts\moorkyz.cjs'
    $escapedCore = $core.Replace('\', '\\').Replace("'", "\'")
    $automaticEntry = Join-Path $autoRoot 'moorkyz'
    [IO.File]::WriteAllText($automaticEntry, "require('$escapedCore');")
    $bootstrapFolder = Join-Path $autoRoot 'src\.moorkyz\app'
    New-Item -ItemType Directory -Path $bootstrapFolder | Out-Null
    Copy-Item -LiteralPath (Join-Path $root 'src\.moorkyz\app\launch.ps1') -Destination (Join-Path $bootstrapFolder 'launch.ps1')
    Copy-Item -LiteralPath (Join-Path $root 'RUN.cmd') -Destination (Join-Path $autoRoot 'RUN.cmd')
    $launchCommands = @('', 'start', 'launch', 'RUN')
    $shortcutScripts = Join-Path $autoRoot 'src\.moorkyz\scripts'
    $shortcutAssets = Join-Path $autoRoot 'src\.moorkyz\public'
    New-Item -ItemType Directory -Path $shortcutScripts, $shortcutAssets | Out-Null
    $shortcutScript = Join-Path $shortcutScripts 'create-moorkyz-shortcut.ps1'
    Copy-Item -LiteralPath (Join-Path $root 'src\.moorkyz\scripts\create-moorkyz-shortcut.ps1') -Destination $shortcutScript
    Copy-Item -LiteralPath (Join-Path $root 'src\.moorkyz\public\favicon.ico') -Destination (Join-Path $shortcutAssets 'favicon.ico')
    & $shortcutScript | Out-Null
    $shortcutShell = New-Object -ComObject WScript.Shell
    $shortcut = $shortcutShell.CreateShortcut((Join-Path $shortcutScripts 'moorkyz.lnk'))
    Assert-True ([IO.Path]::GetFileName($shortcut.TargetPath) -eq 'powershell.exe' -and $shortcut.Arguments.Contains('-WindowStyle Hidden') -and $shortcut.Arguments.Contains((Join-Path $bootstrapFolder 'launch.ps1'))) 'Explorer shortcuts must target the GUI bootstrap, never an interactive command prompt.'
    if ($LauncherExe) {
        $LauncherExe = [IO.Path]::GetFullPath($LauncherExe)
        if (-not (Test-Path -LiteralPath $LauncherExe -PathType Leaf)) { throw "Launcher fixture EXE is missing: $LauncherExe" }
        $fakeScripts = Join-Path $autoRoot 'src\.moorkyz\scripts\helpers'
        New-Item -ItemType Directory -Path $fakeScripts | Out-Null
        Copy-Item -LiteralPath $core -Destination (Join-Path $autoRoot 'src\.moorkyz\scripts\moorkyz.cjs')
        Copy-Item -LiteralPath (Join-Path $root 'src\.moorkyz\scripts\helpers\startup.cjs') -Destination (Join-Path $fakeScripts 'startup.cjs')
        $launchCommands += 'EXE'
    }
    foreach ($launchCommand in $launchCommands) {
        $autoStart = [Diagnostics.ProcessStartInfo]::new(
            (Get-Command node.exe).Source,
            [Moorkyz.ControlGui]::QuoteArgument($automaticEntry) + ' ' + $launchCommand
        )
        if ($launchCommand -eq 'RUN') {
            $autoStart.FileName = $env:ComSpec
            $autoStart.Arguments = '/d /c .\RUN.cmd'
        }
        if ($launchCommand -eq 'EXE') {
            $autoStart.FileName = $LauncherExe
            $autoStart.Arguments = '--root ' + [Moorkyz.ControlGui]::QuoteArgument($autoRoot)
        }
        $autoStart.WorkingDirectory = $autoRoot
        $autoStart.UseShellExecute = $false
        $autoStart.CreateNoWindow = $true
        $autoStart.RedirectStandardOutput = $true
        $autoStart.RedirectStandardError = $true
        $autoStart.EnvironmentVariables.Remove('MOORKYZ_HEADLESS')
        $automatic = [Diagnostics.Process]::Start($autoStart)
        $automaticChild = $null
        try {
            $clock = [Diagnostics.Stopwatch]::StartNew()
            $autoWindow = $null
            $retryControl = $null
            while ($clock.ElapsedMilliseconds -lt 15000 -and ($launchCommand -eq 'RUN' -or -not $automatic.HasExited)) {
                if (-not $automaticChild) {
                    if ($launchCommand -eq 'RUN' -or $launchCommand -eq 'EXE') {
                        $child = Get-CimInstance Win32_Process -Filter "Name = 'powershell.exe'" |
                            Where-Object { $_.CommandLine -and $_.CommandLine.Contains($automaticEntry) -and $_.CommandLine.Contains('-ProjectData') } | Select-Object -First 1
                    } else {
                        $child = Get-CimInstance Win32_Process -Filter "ParentProcessId = $($automatic.Id)" |
                            Where-Object Name -eq 'powershell.exe' | Select-Object -First 1
                    }
                    if ($child) { $automaticChild = [int]$child.ProcessId }
                }
                if ($automaticChild -and -not $autoWindow) {
                    $condition = [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $automaticChild)
                    $autoWindow = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Children, $condition)
                }
                if ($autoWindow) {
                    $condition = [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty, 'Retry failed startup checklist')
                    $retryControl = $autoWindow.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
                    if ($retryControl -and $retryControl.Current.IsEnabled -and -not $retryControl.Current.IsOffscreen) { break }
                }
                Start-Sleep -Milliseconds 30
            }
            Assert-True ($null -ne $autoWindow -and $null -ne $retryControl -and $retryControl.Current.IsEnabled -and -not $retryControl.Current.IsOffscreen) "Every launcher must open GUI before failed preflight, with a visible explicit Retry action. Command: '$launchCommand'"
            $autoWindow.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close()
            Assert-True ($automatic.WaitForExit(5000) -and $automatic.ExitCode -eq 0) ('Closing the failed-check GUI must release the launcher: ' + $automatic.StandardError.ReadToEnd())
            $captured = $automatic.StandardOutput.ReadToEnd()
            Assert-True ($captured -notmatch 'Moorkyz - local project actions|No package.json') 'GUI-first launches must not print a console menu or execute a root service script.'
            $autoReports = Get-ChildItem -LiteralPath (Join-Path $autoRoot '.moorkyz\control-center\runs') -Filter report.xml -Recurse -File
            Assert-True (@($autoReports).Count -gt 0) 'Startup checks must create a recoverable full diagnostic report inside the GUI.'
        } finally {
            if ($automaticChild -and (Get-Process -Id $automaticChild -ErrorAction SilentlyContinue)) {
                $remainingChild = Get-Process -Id $automaticChild -ErrorAction SilentlyContinue
                if ($remainingChild -and -not $remainingChild.WaitForExit(3000)) { Stop-Process -Id $automaticChild -Force }
            }
            if (-not $automatic.HasExited) {
                Stop-Process -Id $automatic.Id -Force
            }
            $automatic.Dispose()
        }
    }
    $beforeOpen = @(Get-ChildItem -LiteralPath $fixture -Recurse -File).Count
    $start = [Diagnostics.ProcessStartInfo]::new(
        (Get-Command node.exe).Source,
        [Moorkyz.ControlGui]::QuoteArgument((Join-Path $root 'moorkyz')) + ' gui'
    )
    $start.WorkingDirectory = $fixture
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $launcher = [Diagnostics.Process]::Start($start)
    $childPid = $null
    try {
        $clock = [Diagnostics.Stopwatch]::StartNew()
        $nativeWindow = $null
        while ($clock.ElapsedMilliseconds -lt 15000 -and -not $launcher.HasExited) {
            $child = Get-CimInstance Win32_Process -Filter "ParentProcessId = $($launcher.Id)" |
                Where-Object Name -eq 'powershell.exe' | Select-Object -First 1
            if ($child) {
                $childPid = [int]$child.ProcessId
                $condition = [System.Windows.Automation.PropertyCondition]::new(
                    [System.Windows.Automation.AutomationElement]::ProcessIdProperty, $childPid
                )
                $nativeWindow = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst(
                    [System.Windows.Automation.TreeScope]::Children, $condition
                )
                if ($nativeWindow) { break }
            }
            Start-Sleep -Milliseconds 100
        }
        if (-not $nativeWindow) {
            if ($launcher.HasExited) { throw ('Real GUI launcher failed: ' + $launcher.StandardError.ReadToEnd()) }
            throw 'Real GUI launcher did not expose a responsive native window.'
        }
        Assert-True ($nativeWindow.Current.Name -eq 'Moorkyz local control') 'The real CLI must open the Moorkyz GUI.'
        $pattern = $nativeWindow.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern)
        Assert-True ($pattern.WaitForInputIdle(3000)) 'The launched GUI must be responsive.'
        $pattern.Close()
        Assert-True ($launcher.WaitForExit(5000)) 'Closing the GUI must release the launcher process.'
        Assert-True ($launcher.ExitCode -eq 0) ('Launcher exit failed: ' + $launcher.StandardError.ReadToEnd())
        Assert-True (@(Get-ChildItem -LiteralPath $fixture -Recurse -File).Count -eq $beforeOpen) 'Opening the real GUI must not create new logs, reports or commands.'
    } catch {
        Write-Output ('Native fixture failure: ' + $_.Exception.ToString())
        throw
    } finally {
        $cleanupClock = [Diagnostics.Stopwatch]::StartNew()
        while ((($gui -ne $null -and $gui.IsBusy) -or ($secondGui -ne $null -and $secondGui.IsBusy)) -and $cleanupClock.ElapsedMilliseconds -lt 10000) { Pump 20 }
        if (-not $launcher.HasExited) {
            if ($childPid -and (Get-Process -Id $childPid -ErrorAction SilentlyContinue)) { Stop-Process -Id $childPid -Force }
            Stop-Process -Id $launcher.Id -Force
        }
        $launcher.Dispose()
    }
    Write-Output 'PASS: native Moorkyz GUI - shared components, presets, motion, commands, full logs, reports/averages, bounded previews, saved-history search grammar/limits/keyboard, exact source lines, persistent UTC timestamp settings, safe storage failures and junction refusal.'
} finally {
    if ($gui -ne $null -and -not $gui.IsBusy) { $gui.Window.Close() }
    if ($secondGui -ne $null -and -not $secondGui.IsBusy) { $secondGui.Window.Close() }
    Remove-Item -LiteralPath $fixture -Recurse -Force
}
