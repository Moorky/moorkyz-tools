param([switch]$CheckOnly)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'diagnostics.ps1')
$script:ProjectRoot = Split-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) -Parent
$script:Started = [DateTimeOffset]::Now
$script:Clock = [Diagnostics.Stopwatch]::StartNew()
$script:RunId = $script:Started.ToString('yyyy-MM-dd_HH-mm-ss-fff') + '_' + [Guid]::NewGuid().ToString('N')
$runRoot = Join-Path $script:ProjectRoot "runtime\runs\$script:RunId"
$logs = Join-Path $runRoot 'logs'
$reports = Join-Path $runRoot 'reports'
$failed = Join-Path $runRoot 'failed'
$script:LogPath = Join-Path $logs 'startup.md'
$script:ErrorPath = Join-Path $failed 'startup-errors.md'
$script:CsvPath = Join-Path $reports 'startup.csv'
$script:MarkdownPath = Join-Path $reports 'startup.md'
$script:HtmlPath = Join-Path $reports 'startup.html'
$script:Finished = $false
$script:Outcome = 'FAIL'
$exitCode = 1
$action = 'initialize'
$step = [Diagnostics.Stopwatch]::StartNew()
try {
    foreach ($path in @($logs, $reports, $failed)) { Assert-VisoryPath $path }
    [void][IO.Directory]::CreateDirectory($logs)
    [void][IO.Directory]::CreateDirectory($reports)
    [void][IO.Directory]::CreateDirectory($failed)
    [IO.File]::WriteAllBytes($script:ErrorPath, [byte[]]@())
    Add-VisoryLine $script:CsvPath 'run_id,timestamp,area,action,status,elapsed_seconds,message'
    Write-VisoryEvent 'INFO' "RUN started | Source: $script:ProjectRoot | Logs: $logs | Reports: $reports"
    Add-VisoryResult 'initialize' 'SUCCESS' $step.Elapsed.TotalSeconds 'Logging is active before prerequisite checks.'
    $action = 'prerequisites'
    $step.Restart()
    if ($env:OS -ne 'Windows_NT') { throw 'Visory requires Windows 10/11 x64.' }
    if (-not [Environment]::Is64BitOperatingSystem) { throw 'Visory requires 64-bit Windows.' }
    $dotnet = Get-Command dotnet -CommandType Application -ErrorAction SilentlyContinue
    if ($null -eq $dotnet) { throw 'The .NET 10 SDK is missing. Install it explicitly, then run RUN.cmd again. No prerequisite was installed.' }
    Invoke-VisoryProcess $dotnet.Source @('--list-sdks') 'sdk'
    if (-not ($script:ProcessOutput -match '^10\.')) { throw 'The .NET 10 SDK is required. No SDK was installed.' }
    Write-VisoryEvent 'INFO' '.NET 10 SDK found. WebView2 availability is checked by the app before sign-in; nothing is installed automatically.'
    Add-VisoryResult $action 'SUCCESS' $step.Elapsed.TotalSeconds 'Windows x64 and .NET 10 SDK available.'
    if (-not $CheckOnly) {
        $action = 'build'
        $step.Restart()
        Write-VisoryEvent 'INFO' 'Building with MSBuild incremental reuse. NuGet restore runs only as required; no native tools are installed.'
        Invoke-VisoryProcess $dotnet.Source @('build', (Join-Path $script:ProjectRoot 'src\MoorkszVisoryMediaDownloader.sln'), '-c', 'Debug', '--nologo') 'build'
        Add-VisoryResult $action 'SUCCESS' $step.Elapsed.TotalSeconds 'Build ready (unchanged outputs reused by MSBuild).'
        $action = 'app'
        $step.Restart()
        $env:VISORY_RUN_DIRECTORY = $runRoot
        $env:VISORY_RUN_ID = $script:RunId
        $exe = Join-Path $script:ProjectRoot 'src\tools\desktop\bin\Debug\net10.0-windows\win-x64\MoorkszVisoryMediaDownloader.exe'
        Write-VisoryEvent 'INFO' 'Starting app. This terminal stays attached, including while the hub is in the tray.'
        Invoke-VisoryProcess $exe @() 'app'
        Add-VisoryResult $action 'SUCCESS' $step.Elapsed.TotalSeconds 'App exited normally.'
    }
    $exitCode = 0
    $script:Outcome = if ($CheckOnly) { 'CHECKED - app not started' } else { 'SUCCESS' }
}
catch {
    [Console]::Error.WriteLine((Protect-VisoryText $_.Exception.Message))
    if ([IO.Directory]::Exists($logs)) {
        Write-VisoryEvent 'ERROR' "$action failed: $($_.Exception.Message)"
        Add-VisoryResult $action 'FAIL' $step.Elapsed.TotalSeconds $_.Exception.Message
    }
}
finally {
    $script:Finished = $true
    if ([IO.Directory]::Exists($logs)) {
        Write-VisoryEvent 'INFO' "RUN finished | Status: $script:Outcome | Duration: $($script:Clock.Elapsed) | Report: $script:CsvPath | Error log: $script:ErrorPath"
        Write-VisoryReport
    }
}
exit $exitCode
