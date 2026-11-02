#requires -Version 5.1
Set-StrictMode -Version Latest

function Resolve-UnlockTarget([string]$Path) {
    if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT) {
        throw 'Moorkyz Folder Unlock requires Windows.'
    }
    if ($Path -notmatch '^[A-Za-z]:\\' -or $Path -match '[\x00-\x1f"*?<>|]' -or $Path.Substring(2).Contains(':')) {
        throw 'Use an absolute local Windows folder path without wildcards, device prefixes or alternate streams.'
    }
    $full = [IO.Path]::GetFullPath($Path)
    if ($full.TrimEnd('\') -ieq [IO.Path]::GetPathRoot($full).TrimEnd('\')) {
        throw 'Filesystem roots are not supported.'
    }
    $item = Get-Item -LiteralPath $full -Force -ErrorAction Stop
    if (-not $item.PSIsContainer) { throw 'Target must be an existing folder.' }
    $current = $item
    while ($null -ne $current) {
        if ($current.Attributes -band [IO.FileAttributes]::ReparsePoint) {
            throw 'Linked folders, junctions and their descendants are not supported.'
        }
        $current = $current.Parent
    }
    foreach ($protected in @($env:WINDIR, $env:ProgramFiles, ${env:ProgramFiles(x86)}, $env:ProgramData, $env:USERPROFILE, (Join-Path $env:SystemDrive 'Users'))) {
        if (-not $protected) { continue }
        $boundary = [IO.Path]::GetFullPath($protected).TrimEnd('\')
        if ($item.FullName.TrimEnd('\') -ieq $boundary) { throw 'Protected top-level folder; select a specific working folder.' }
        if ($protected -in @($env:WINDIR, $env:ProgramFiles, ${env:ProgramFiles(x86)}, $env:ProgramData) -and
            $item.FullName.StartsWith($boundary + '\', [StringComparison]::OrdinalIgnoreCase)) {
            throw 'Windows and application installation folders are not supported.'
        }
    }
    return $item.FullName.TrimEnd('\')
}

function Resolve-UnlockScanner([string]$Path) {
    if (-not $Path) {
        throw 'Supply -HandlePath to a manually installed Microsoft Sysinternals Handle executable. Nothing is downloaded automatically.'
    }
    $item = Get-Item -LiteralPath $Path -Force -ErrorAction Stop
    if ($item.PSIsContainer -or $item.Extension -ine '.exe' -or
        $item.Name -notin @('handle.exe', 'handle64.exe', 'handle64a.exe')) {
        throw 'Select the original Microsoft Handle executable.'
    }
    $current = $item
    while ($null -ne $current) {
        if ($current.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'The scanner path must not contain links.' }
        if ($current -is [IO.FileInfo]) { $current = $current.Directory } else { $current = $current.Parent }
    }
    $signature = Get-AuthenticodeSignature -LiteralPath $item.FullName
    if ($signature.Status -ne 'Valid' -or -not $signature.SignerCertificate -or
        $signature.SignerCertificate.Subject -notmatch '(^|,\s*)CN=Microsoft Corporation(,|$)') {
        throw 'Handle must have a valid Microsoft Authenticode signature.'
    }
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'Handle requires an elevated PowerShell. Reopen it yourself if appropriate; this tool never elevates automatically.'
    }
    if (-not (Test-Path -LiteralPath 'HKCU:\Software\Sysinternals\Handle')) {
        throw 'Run Microsoft Handle separately and review its license before using this tool.'
    }
    $eula = Get-ItemProperty -LiteralPath 'HKCU:\Software\Sysinternals\Handle' -Name EulaAccepted -ErrorAction Stop
    if ($eula.EulaAccepted -ne 1) { throw 'Run Microsoft Handle separately and review its license before using this tool.' }
    return $item.FullName
}

function Invoke-UnlockScanner([string]$Scanner, [string]$Target) {
    $start = New-Object Diagnostics.ProcessStartInfo
    $start.FileName = $Scanner
    $start.Arguments = '-nobanner -v "' + $Target + '"'
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $process = New-Object Diagnostics.Process
    $process.StartInfo = $start
    try {
        if (-not $process.Start()) { throw 'Could not start Handle.' }
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit(30000)) {
            $process.Kill()
            $process.WaitForExit()
            throw 'Handle timed out after 30 seconds; only the scanner process was stopped.'
        }
        $text = $stdout.GetAwaiter().GetResult()
        $errorText = $stderr.GetAwaiter().GetResult()
        if ($errorText.Trim()) { throw "Handle reported an error: $($errorText.Trim())" }
        return [pscustomobject]@{ ExitCode = $process.ExitCode; Text = $text }
    }
    finally { $process.Dispose() }
}

function ConvertFrom-UnlockOutput([string]$Text, [int]$ExitCode, [string]$Target) {
    $textValue = $Text.Trim()
    if ($textValue -eq 'No matching handles found.' -and $ExitCode -in @(0, 1)) { return }
    if ($ExitCode -ne 0) { throw "Handle failed with exit code $ExitCode. $textValue" }
    $lines = @($textValue -split '\r?\n' | Where-Object { $_.Trim() })
    if (-not $lines.Count -or $lines[0].Replace('"', '').Trim() -ine 'Process,PID,Type,Handle,Name') {
        throw 'Unrecognized Handle CSV output; absence of readable output is not proof that the folder is unlocked.'
    }
    foreach ($row in @($lines | ConvertFrom-Csv)) {
        $processId = 0
        if (-not [int]::TryParse($row.PID, [ref]$processId) -or $processId -le 0 -or
            -not $row.Process -or -not $row.Name -or -not $row.Type -or -not $row.Handle) {
            throw 'Malformed Handle CSV row; scan results were rejected.'
        }
        if ($row.Type -ine 'File') { continue }
        if ($row.Name -ieq $Target -or $row.Name.StartsWith($Target + '\', [StringComparison]::OrdinalIgnoreCase)) {
            [pscustomobject]@{ ProcessId = $processId; ProcessName = $row.Process; Path = $row.Name }
        }
    }
}

function Get-UnlockProcess([int]$ProcessId) {
    $process = Get-Process -Id $ProcessId -ErrorAction Stop
    try {
        $native = Get-CimInstance Win32_Process -Filter "ProcessId=$ProcessId" -ErrorAction Stop
        if ($null -eq $native) { throw 'Process exited while it was inspected.' }
        $owner = Invoke-CimMethod -InputObject $native -MethodName GetOwnerSid -ErrorAction Stop
        if ($owner.ReturnValue -ne 0 -or -not $owner.Sid) { throw 'Cannot verify process ownership.' }
        $protected = @('system', 'registry', 'idle', 'smss', 'csrss', 'wininit', 'winlogon', 'services', 'lsass',
            'svchost', 'explorer', 'dwm', 'taskhostw', 'powershell', 'pwsh', 'conhost', 'windowsterminal')
        $ownSid = [Security.Principal.WindowsIdentity]::GetCurrent().User.Value
        $reason = ''
        if ($ProcessId -eq $PID -or $process.ProcessName -in $protected) { $reason = 'Protected process; close its windows manually.' }
        elseif ($process.SessionId -ne (Get-Process -Id $PID).SessionId -or $owner.Sid -ne $ownSid) { $reason = 'Not an application owned by this user in this session.' }
        elseif ($process.MainWindowHandle -eq [IntPtr]::Zero) { $reason = 'No main window; close the application manually.' }
        return [pscustomobject]@{
            Id = $ProcessId; Name = $process.ProcessName; StartTicks = $process.StartTime.ToUniversalTime().Ticks
            CanRequestClose = ($reason -eq ''); Reason = $reason
        }
    }
    finally { $process.Dispose() }
}

function Get-UnlockReport([string]$Target, [string]$Scanner) {
    $targetPath = Resolve-UnlockTarget $Target
    $result = Invoke-UnlockScanner $Scanner $targetPath
    $handles = @(ConvertFrom-UnlockOutput $result.Text $result.ExitCode $targetPath)
    $processes = @(
        foreach ($processId in @($handles | ForEach-Object { $_.ProcessId } | Sort-Object -Unique)) {
            try { Get-UnlockProcess $processId }
            catch {
                [pscustomobject]@{ Id = $processId; Name = 'Unavailable'; StartTicks = 0; CanRequestClose = $false; Reason = $_.Exception.Message }
            }
        }
    )
    return [pscustomobject]@{ Target = $targetPath; ScannedAtUtc = [DateTime]::UtcNow.ToString('o'); Handles = $handles; Processes = $processes }
}

function Request-UnlockClose($Candidate, [string]$Target, [string]$Scanner) {
    $fresh = Get-UnlockReport $Target $Scanner
    $current = @($fresh.Processes | Where-Object { $_.Id -eq $Candidate.Id })
    if ($current.Count -ne 1 -or -not $current[0].CanRequestClose -or $current[0].StartTicks -ne $Candidate.StartTicks) {
        throw 'The process identity, eligibility or matching handles changed. Scan and review again.'
    }
    $process = Get-Process -Id $Candidate.Id -ErrorAction Stop
    try {
        if ($process.StartTime.ToUniversalTime().Ticks -ne $Candidate.StartTicks) { throw 'PID was reused; no action taken.' }
        if (-not $process.CloseMainWindow()) { throw 'The application rejected the close request. Close it manually.' }
        if (-not $process.WaitForExit(5000)) { return 'Close requested; application is still running. Save/confirm in its window and rescan. It was not force-terminated.' }
        return 'The selected application exited after a normal close request. A fresh scan follows.'
    }
    finally { $process.Dispose() }
}
