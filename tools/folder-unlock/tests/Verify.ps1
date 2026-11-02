#requires -Version 5.1
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$tool = Split-Path -Parent $PSScriptRoot
. (Join-Path $tool 'helpers\FolderUnlock.ps1')
$root = Join-Path ([IO.Path]::GetTempPath()) ('moorkyz-unlock-test-' + [Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($root)
$checks = 0

function Assert-True([bool]$Value, [string]$Message) {
    if (-not $Value) { throw $Message }
    $script:checks++
}
function Assert-Throws([scriptblock]$Action, [string]$Pattern) {
    $message = ''
    try { & $Action | Out-Null } catch { $message = $_.Exception.Message }
    Assert-True ($message -match $Pattern) "Expected error matching '$Pattern'; received '$message'."
}

try {
    $target = Join-Path $root 'folder with spaces'
    [void][IO.Directory]::CreateDirectory($target)
    $sentinel = Join-Path $target 'keep.txt'
    [IO.File]::WriteAllText($sentinel, 'unchanged')
    Assert-True ((Resolve-UnlockTarget ($target + '\')) -eq $target) 'Canonical folder path differs.'
    Assert-Throws { Resolve-UnlockTarget 'C:\' } 'roots'
    Assert-Throws { Resolve-UnlockTarget '\\server\share' } 'absolute local'
    Assert-Throws { Resolve-UnlockTarget 'relative' } 'absolute local'
    Assert-Throws { Resolve-UnlockTarget ($target + ':stream') } 'absolute local'
    Assert-Throws { Resolve-UnlockTarget ($target + '*') } 'absolute local'
    Assert-Throws { Resolve-UnlockTarget $sentinel } 'folder'
    Assert-Throws { Resolve-UnlockTarget $env:USERPROFILE } 'Protected'
    Assert-Throws { Resolve-UnlockTarget $env:WINDIR } 'Protected'
    $junction = Join-Path $root 'linked'
    New-Item -ItemType Junction -Path $junction -Target $target | Out-Null
    Assert-Throws { Resolve-UnlockTarget $junction } 'Linked'
    [IO.Directory]::Delete($junction)

    $csv = @"
Process,PID,Type,Handle,Name
editor.exe,1234,File,10,"$target\file,one.txt"
editor.exe,1234,File,11,"$target"
unrelated.exe,5678,File,12,"${target}-other\file.txt"
unrelated.exe,5678,File,13,"C:\elsewhere\$target"
"@
    $rows = @(ConvertFrom-UnlockOutput $csv 0 $target)
    Assert-True ($rows.Count -eq 2) 'Path boundary filter included unrelated handles.'
    Assert-True ($rows[0].ProcessId -eq 1234) 'PID parsing failed.'
    Assert-True ($rows[0].Path.EndsWith('file,one.txt')) 'CSV quoting failed.'
    Assert-True (@(ConvertFrom-UnlockOutput 'No matching handles found.' 1 $target).Count -eq 0) 'Known no-match output failed.'
    Assert-True (@(ConvertFrom-UnlockOutput 'Process,PID,Type,Handle,Name' 0 $target).Count -eq 0) 'Empty CSV failed.'
    Assert-Throws { ConvertFrom-UnlockOutput 'Access denied' 1 $target } 'failed'
    Assert-Throws { ConvertFrom-UnlockOutput '' 0 $target } 'Unrecognized'
    Assert-Throws { ConvertFrom-UnlockOutput "Process,PID,Type,Handle,Name`neditor,invalid,File,10,$target" 0 $target } 'Malformed'
    Assert-Throws { Resolve-UnlockScanner '' } 'manually installed'
    Assert-Throws { Resolve-UnlockScanner $sentinel } 'original Microsoft'

    function Invoke-UnlockScanner { return [pscustomobject]@{ Text = 'Process,PID,Type,Handle,Name'; ExitCode = 0 } }
    $emptyReport = Get-UnlockReport $target 'fixture-only'
    Assert-True ($emptyReport.Handles.Count -eq 0 -and $emptyReport.Processes.Count -eq 0) 'Empty report failed.'
    function Invoke-UnlockScanner { return [pscustomobject]@{ Text = $csv; ExitCode = 0 } }
    function Get-UnlockProcess { throw 'Fixture process has exited.' }
    $unavailable = Get-UnlockReport $target 'fixture-only'
    Assert-True (-not $unavailable.Processes[0].CanRequestClose) 'Unavailable PID became actionable.'
    Assert-True ($unavailable.Processes[0].Reason -match 'exited') 'Process failure was hidden.'

    $script:closeCalls = 0
    $start = [DateTime]::UtcNow
    $candidate = [pscustomobject]@{ Id = 1234; Name = 'fixture'; StartTicks = $start.Ticks; CanRequestClose = $true }
    $script:freshCandidate = $candidate
    function Get-UnlockReport { return [pscustomobject]@{ Processes = @($script:freshCandidate) } }
    $script:fakeProcess = [pscustomobject]@{ StartTime = $start }
    $script:fakeProcess | Add-Member ScriptMethod CloseMainWindow { $script:closeCalls++; return $true }
    $script:fakeProcess | Add-Member ScriptMethod WaitForExit { param($Milliseconds) return $false }
    $script:fakeProcess | Add-Member ScriptMethod Dispose {}
    function Get-Process { return $script:fakeProcess }
    $script:freshCandidate = [pscustomobject]@{ Id = 1234; StartTicks = 1; CanRequestClose = $true }
    Assert-Throws { Request-UnlockClose $candidate $target 'fixture-only' } 'changed'
    Assert-True ($script:closeCalls -eq 0) 'Reused PID received a close request.'
    $script:freshCandidate = [pscustomobject]@{ Id = 1234; StartTicks = $start.Ticks; CanRequestClose = $false }
    Assert-Throws { Request-UnlockClose $candidate $target 'fixture-only' } 'changed'
    $script:freshCandidate = $candidate
    $message = Request-UnlockClose $candidate $target 'fixture-only'
    Assert-True ($script:closeCalls -eq 1 -and $message -match 'not force-terminated') 'Normal close reporting failed.'

    $engine = (Microsoft.PowerShell.Management\Get-Process -Id $PID).Path
    foreach ($preview in @('-DryRun', '-WhatIf')) {
        $output = & $engine -NoProfile -NonInteractive -File (Join-Path $tool 'Unlock-Folder.ps1') -Target $target $preview 2>&1
        Assert-True ($LASTEXITCODE -eq 0 -and "$output" -match 'Preview only') 'Preview attempted an external action.'
    }
    $startInfo = New-Object Diagnostics.ProcessStartInfo
    $startInfo.FileName = $engine
    $startInfo.Arguments = '-NoProfile -NonInteractive -File "' + (Join-Path $tool 'Unlock-Folder.ps1') + '" -Target "' + $target + '" -CloseProcessId 1234 -NonInteractive'
    $startInfo.UseShellExecute = $false
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $child = [Diagnostics.Process]::Start($startInfo)
    try {
        $out = $child.StandardOutput.ReadToEndAsync()
        $err = $child.StandardError.ReadToEndAsync()
        if (-not $child.WaitForExit(15000)) { $child.Kill(); throw 'Non-interactive test timed out.' }
        Assert-True ($child.ExitCode -ne 0 -and $err.GetAwaiter().GetResult() -match 'interactive confirmation') 'Non-interactive closure was not refused.'
        [void]$out.GetAwaiter().GetResult()
    }
    finally { $child.Dispose() }
    Assert-True ([IO.File]::ReadAllText($sentinel) -eq 'unchanged') 'Fixture source changed.'
    Assert-True (@(Get-ChildItem -LiteralPath $target -Force).Count -eq 1) 'Tool left state in target.'
    Write-Output "PASS: $checks folder-unlock assertions; no user applications or files changed."
}
finally {
    if (Test-Path -LiteralPath $junction -ErrorAction SilentlyContinue) { [IO.Directory]::Delete($junction) }
    if ($root -notlike (Join-Path ([IO.Path]::GetTempPath()) 'moorkyz-unlock-test-*')) { throw 'Invalid fixture cleanup path.' }
    Remove-Item -LiteralPath $root -Recurse -Force
}
