param([string]$Executable = '')
$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
if (-not $Executable) { $Executable = Join-Path (Get-Location) 'artifacts\portable-win-x64\MoorkyzMediaRecover.exe' }
if (-not (Test-Path $Executable)) { throw "Build/publish first: $Executable" }
$fixture = Join-Path (Get-Location) ('.fixtures\ui-' + [guid]::NewGuid().ToString('N'))
$state = Join-Path $fixture 'state'
$source = Join-Path $fixture 'source'
$saved = Join-Path $fixture 'saved'
New-Item -ItemType Directory -Path $state, $source -Force | Out-Null
$bytes = [byte[]](0..255) * 257
[IO.File]::WriteAllBytes((Join-Path $source 'sample.mp4'), $bytes)
[IO.File]::WriteAllBytes((Join-Path $source 'sound.m4a'), $bytes)
Get-ChildItem $source -File | ForEach-Object { $_.CreationTimeUtc = [datetime]'2026-10-12T12:00:00Z'; $_.LastWriteTimeUtc = [datetime]'2026-10-12T12:00:00Z' }
@{
    Sources = @(@{ Kind = 'LocalFolder'; Id = $source })
    Destination = $saved
    Language = 'en'
    Theme = 'Light'
    IntroductionSeen = $true
    Retry = $false
} | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $state 'settings.json') -Encoding utf8
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class MediaRecoverSmokeNative {
 [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
 [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out Rect r);
 [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr dc, uint flags);
 [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
}
'@
function Wait-For([scriptblock]$Test, [int]$Seconds = 45) {
    $until = [datetime]::UtcNow.AddSeconds($Seconds)
    do { $value = & $Test; if ($value) { return $value }; Start-Sleep -Milliseconds 150 } while ([datetime]::UtcNow -lt $until)
    throw 'Timed out waiting for the app UI condition.'
}
function Find-Name($Root, [string]$Name) {
    $condition = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $Name)
    $Root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
}
function Invoke-Control($Element) {
    if (-not $Element) { throw 'Automation element missing.' }
    $pattern = $Element.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
    $pattern.Invoke()
}
function Find-Status($Root, [string]$Text) {
    $elements = $Root.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
    foreach ($element in $elements) {
        if ($element.Current.ControlType -eq [System.Windows.Automation.ControlType]::Text -and $element.Current.Name.Contains($Text)) { return $true }
    }
    return $false
}
$process = $null
try {
    $process = Start-Process -FilePath $Executable -ArgumentList @('--smoke', ('"' + $state + '"')) -PassThru
    Wait-For { if ($process.HasExited) { throw "App exited: $($process.ExitCode)" }; Test-Path (Join-Path $state 'ready.txt') } | Out-Null
    Wait-For { $process.Refresh(); $process.MainWindowHandle -ne [IntPtr]::Zero } | Out-Null
    $window = [System.Windows.Automation.AutomationElement]::FromHandle($process.MainWindowHandle)
    Invoke-Control (Find-Name $window '1. Scan')
    Wait-For { Find-Status $window 'Ready to recover' } | Out-Null
    Invoke-Control (Find-Name $window '2. Start recovery')
    $processCondition = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $process.Id)
    $yes = Wait-For {
        $windows = [System.Windows.Automation.AutomationElement]::RootElement.FindAll([System.Windows.Automation.TreeScope]::Children, $processCondition)
        foreach ($candidate in $windows) {
            $condition = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty, '6')
            $button = $candidate.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
            if ($button) { return $button }
        }
    }
    Invoke-Control $yes
    Wait-For { Find-Status $window 'Completed | Saved to:' } | Out-Null
    $media = @(Get-ChildItem $saved -File | Where-Object { $_.Extension -in '.mp4', '.m4a' })
    if ($media.Count -ne 2) { throw "Expected 2 copied media; got $($media.Count)" }
    $hash = (Get-FileHash (Join-Path $source 'sample.mp4')).Hash
    if (@($media | Where-Object { (Get-FileHash $_.FullName).Hash -ne $hash }).Count -ne 0) { throw 'UI recovered bytes do not match synthetic source.' }
    $summary = Get-ChildItem (Join-Path $saved '.mediarecover\reports') -Filter 'summary_*.json' -Recurse | Select-Object -First 1
    $data = Get-Content $summary.FullName -Raw | ConvertFrom-Json
    if ($data.NewCopies -ne 2 -or $data.SavedTo -ne $saved -or $data.Status -ne 'Completed') { throw 'UI run summary is inconsistent.' }
    Invoke-Control (Find-Name $window 'Verify saved files')
    Wait-For { Find-Status $window 'Local integrity checked. Changed/missing: 0' } | Out-Null
    $artifactRoot = Join-Path (Get-Location) 'artifacts'
    New-Item -ItemType Directory -Path $artifactRoot -Force | Out-Null
    $rectangle = New-Object MediaRecoverSmokeNative+Rect
    if (-not [MediaRecoverSmokeNative]::GetWindowRect($process.MainWindowHandle, [ref]$rectangle)) { throw 'GetWindowRect failed.' }
    $bitmap = New-Object System.Drawing.Bitmap(($rectangle.Right - $rectangle.Left), ($rectangle.Bottom - $rectangle.Top))
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $dc = $graphics.GetHdc()
    try { $captured = [MediaRecoverSmokeNative]::PrintWindow($process.MainWindowHandle, $dc, 2) }
    finally { $graphics.ReleaseHdc($dc) }
    if (-not $captured) { throw 'PrintWindow could not capture the app (desktop capture is deliberately forbidden).' }
    $bitmap.Save((Join-Path $artifactRoot 'mediarecover-overview.png'), [System.Drawing.Imaging.ImageFormat]::Png)
    $graphics.Dispose(); $bitmap.Dispose()
    [MediaRecoverSmokeNative]::PostMessage($process.MainWindowHandle, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
    if (-not $process.WaitForExit(15000)) { throw 'App did not exit after normal close.' }
    @{
        Result = 'PASS'
        Tests = @('actual self-contained WPF launch', 'UI Automation Scan', 'review status', 'recovery confirmation', 'two actual copies', 'source/target SHA256 equality', 'exact saved/report paths', 'verify saved files action', 'normal close leaves no process')
        Copies = 2
        Screenshot = 'artifacts\mediarecover-overview.png'
        PersonalFilesInspected = $false
    } | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $artifactRoot 'ui-smoke-result.json') -Encoding utf8
    Write-Output 'PASS real WPF scan, confirmation, recovery, hash, reports, verify and exit; app-only screenshot saved.'
}
finally {
    if ($process -and -not $process.HasExited) { Stop-Process -Id $process.Id -Force }
    if (Test-Path $fixture) { Remove-Item -LiteralPath $fixture -Recurse -Force }
}
