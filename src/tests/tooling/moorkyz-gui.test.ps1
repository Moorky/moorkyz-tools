param([string]$ScreenshotDirectory)

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
$sources = @('Motion.cs', 'MoorkyzGui.cs') | ForEach-Object { Join-Path $source $_ }
Add-Type -Path $sources -ReferencedAssemblies $references
$fixture = Join-Path ([IO.Path]::GetTempPath()) ('moorkyz gui fixture ' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixture | Out-Null
$gui = $null

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
  console.log(JSON.stringify({ command, project, cwd: process.cwd() }));
  if (command === "test") {
    console.log("x".repeat(125000));
    console.error("Synthetic failure");
    process.exitCode = 3;
  }
}, 180);
'@)
    $gui = [Moorkyz.ControlGui]::new($source, $fixture, $cli, (Get-Command node.exe).Source, [string[]]@('demo-project', 'second'), 'demo-project')
    $gui.Window.Show()
    Pump 200
    $cards = $gui.Window.FindName('ActionCards')
    $output = $gui.Window.FindName('Output')
    $status = $gui.Window.FindName('RunStatus')
    Assert-True (-not $gui.IsBusy) 'Opening the GUI must not run a command.'
    Assert-True ((Get-ChildItem -LiteralPath $fixture).Count -eq 1) 'Opening the GUI must not write workspace files.'
    Assert-True ($cards.Children.Count -eq 10) 'Expected ten real CLI action cards.'
    Assert-True ($gui.Window.Icon -ne $null) 'Expected the supplied Moorkyz window icon.'
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
        Pump 220
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
        foreach ($frame in 1..15) {
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
    Assert-True (-not $gui.RunCommand('status')) 'Overlapping commands must be prevented.'
    $gui.Window.Close()
    Assert-True $gui.Window.IsVisible 'Closing must not abandon an in-flight command.'
    Wait-Command
    Assert-True ($gui.LastExitCode -eq 0) 'The fixture command should succeed.'
    Assert-True ($output.Text -match '"command":"start","project":"demo-project"') 'The exact command and project must reach the CLI without shell interpretation.'
    Assert-True ($output.Text.Contains($fixture.Replace('\', '\\'))) 'The process must execute in the selected workspace.'
    $project.SelectedItem = 'second'
    Assert-True ($gui.RunCommand('test')) 'The second command should start.'
    Wait-Command
    Assert-True ($gui.LastExitCode -eq 3) 'The actual CLI failure exit code must be retained.'
    Assert-True ($status.Text -match 'failed.*exit 3') 'The GUI must explicitly report a failed command.'
    Assert-True ($output.Text -match 'Synthetic failure') 'The GUI must show stderr.'
    Assert-True ($output.Text.Length -le 120000) 'Visible command output, including its truncation notice, must stay within 120000 characters.'
    Assert-True ($first.IsEnabled) 'Actions must recover after failure.'
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
        Assert-True ((Get-ChildItem -LiteralPath $fixture).Count -eq 1) 'The real GUI launcher must not create runtime state or start commands on open.'
    } finally {
        if (-not $launcher.HasExited) {
            if ($childPid -and (Get-Process -Id $childPid -ErrorAction SilentlyContinue)) { Stop-Process -Id $childPid -Force }
            Stop-Process -Id $launcher.Id -Force
        }
        $launcher.Dispose()
    }
    Write-Output 'PASS: native Moorkyz GUI - shared components, all 12 presets, 3D materials, reduced motion, command execution/failure, output limits, reordering and drag inertia.'
} finally {
    if ($gui -ne $null -and -not $gui.IsBusy) { $gui.Window.Close() }
    Remove-Item -LiteralPath $fixture -Recurse -Force
}
