$ErrorActionPreference = 'Stop'

function Protect-VisoryText([string]$Text) {
    $value = $Text -replace 'eyJ[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}', '[REDACTED-TOKEN]'
    $value = $value -replace '(?i)\b((?:proxy-)?authorization["'']?\s*[:=]\s*["'']?(?:(?:bearer|firebase|basic)\s+)?)[^\s,;|"''\\]+', '$1[REDACTED]'
    $value = $value -replace '(?i)\b((?:set-)?cookie["'']?\s*[:=]\s*["'']?)[^|\r\n]+', '$1[REDACTED]'
    $value = $value -replace '(?i)\b(bearer\s+)[A-Za-z0-9._~+/=-]{8,}', '$1[REDACTED]'
    $value = $value -replace '(?i)(https?://[^\s?#|"''<>]+)[?#][^\s|"''<>]*', '$1?[REDACTED]'
    $value = $value -replace '(?i)(https?://)[^/\s@]+@', '$1[REDACTED]@'
    $value = $value -replace '(?i)(\b(?:password|passwd|token|access_token|refresh_token|id_token|api[_-]?key|secret)\b["'']?\s*[:=]\s*)(?:\[REDACTED\]|"[^"\r\n]*"|''[^''\r\n]*''|[^\s,;|}\]]+)', '$1[REDACTED]'
    return $value -replace '[\x00-\x08\x0B\x0C\x0E-\x1F\x7F]', ''
}

function ConvertTo-VisoryMarkdown([string]$Text) {
    return [System.Net.WebUtility]::HtmlEncode($Text).Replace('[', '&#91;').Replace(']', '&#93;').Replace('`', '&#96;')
}

function Assert-VisoryPath([string]$Path) {
    $current = [IO.Path]::GetFullPath($Path)
    while ($current) {
        if ([IO.File]::Exists($current) -or [IO.Directory]::Exists($current)) {
            if (([IO.File]::GetAttributes($current) -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "Logs/reports cannot follow links or junctions: $current"
            }
        }
        $current = [IO.Path]::GetDirectoryName($current)
    }
}

function Add-VisoryLine([string]$Path, [string]$Line) {
    Assert-VisoryPath $Path
    $bytes = [System.Text.UTF8Encoding]::new($false).GetBytes($Line + "`r`n")
    $stream = [System.IO.File]::Open($Path, [System.IO.FileMode]::Append, [System.IO.FileAccess]::Write, [System.IO.FileShare]::Read)
    try { $stream.Write($bytes, 0, $bytes.Length); $stream.Flush($true) }
    finally { $stream.Dispose() }
}

function Write-VisoryEvent([string]$Level, [string]$Message, [string]$Area = 'startup') {
    $clean = Protect-VisoryText $Message
    foreach ($part in ($clean -split '\r\n|\r|\n')) {
        $prefix = '[{0}] [{1}] ' -f [DateTimeOffset]::Now.ToString('yyyy-MM-dd HH:mm:ss.fff zzz'), $Level
        $text = '{0} | Run: {1} | Area: {2}' -f $part, $script:RunId, $Area
        $line = $prefix + (ConvertTo-VisoryMarkdown $text)
        Add-VisoryLine $script:LogPath $line
        if ($Level -in @('ERROR', 'FATAL', 'FAIL')) { Add-VisoryLine $script:ErrorPath $line }
        [Console]::WriteLine($prefix + $text)
    }
}

function Write-VisoryReport {
    foreach ($path in @($script:CsvPath, $script:MarkdownPath, $script:HtmlPath)) { Assert-VisoryPath $path }
    $rows = @(Import-Csv -LiteralPath $script:CsvPath)
    $columns = @('run_id', 'timestamp', 'area', 'action', 'status', 'elapsed_seconds', 'message')
    $status = if ($script:Finished) { $script:Outcome } else { 'INCOMPLETE - no successful completion recorded' }
    $ended = if ($script:Finished) { [DateTimeOffset]::Now.ToString('O') } else { 'not finished' }
    $summary = 'Started: {0} | Ended: {1} | Status: {2} | Duration: {3}s' -f $script:Started.ToString('O'), $ended, $status, $script:Clock.Elapsed.TotalSeconds.ToString('0.000', [Globalization.CultureInfo]::InvariantCulture)
    $md = [System.Text.StringBuilder]::new()
    [void]$md.AppendLine("# Visory startup report - $script:RunId").AppendLine().AppendLine($summary).AppendLine()
    [void]$md.AppendLine('[CSV report](startup.csv)').AppendLine()
    [void]$md.AppendLine('| run_id | timestamp | area | action | status | elapsed_seconds | message |').AppendLine('| --- | --- | --- | --- | --- | --- | --- |')
    $html = [System.Text.StringBuilder]::new()
    [void]$html.Append('<!doctype html><html lang="en"><head><meta charset="utf-8"><meta http-equiv="Content-Security-Policy" content="default-src ''none''; base-uri ''none''; form-action ''none''"><meta name="viewport" content="width=device-width, initial-scale=1"><title>Visory startup report</title></head><body><h1>Visory startup report</h1><p>')
    [void]$html.Append([System.Net.WebUtility]::HtmlEncode($summary)).Append('</p><p><a href="startup.csv">CSV report</a></p><table><caption>CSV report contents</caption><thead><tr>')
    foreach ($column in $columns) { [void]$html.Append('<th scope="col">').Append($column).Append('</th>') }
    [void]$html.Append('</tr></thead><tbody>')
    foreach ($row in $rows) {
        $cells = foreach ($column in $columns) { ConvertTo-VisoryMarkdown ([string]$row.$column) }
        [void]$md.AppendLine('| ' + (($cells | ForEach-Object { $_.Replace('|', '&#124;').Replace("`r", '').Replace("`n", '<br>') }) -join ' | ') + ' |')
        [void]$html.Append('<tr>')
        foreach ($column in $columns) { [void]$html.Append('<td>').Append([System.Net.WebUtility]::HtmlEncode([string]$row.$column)).Append('</td>') }
        [void]$html.Append('</tr>')
    }
    [void]$html.Append('</tbody></table></body></html>')
    foreach ($item in @(@{ Path = $script:MarkdownPath; Text = $md.ToString() }, @{ Path = $script:HtmlPath; Text = $html.ToString() })) {
        $temp = $item.Path + '.tmp'
        try {
            [System.IO.File]::WriteAllText($temp, $item.Text, [System.Text.UTF8Encoding]::new($false))
            if ([System.IO.File]::Exists($item.Path)) { [System.IO.File]::Replace($temp, $item.Path, [System.Management.Automation.Language.NullString]::Value) }
            else { [System.IO.File]::Move($temp, $item.Path) }
        }
        finally { if ([System.IO.File]::Exists($temp)) { [System.IO.File]::Delete($temp) } }
    }
}

function Add-VisoryResult([string]$Action, [string]$Status, [double]$Seconds, [string]$Message) {
    $cells = @($script:RunId, [DateTimeOffset]::Now.ToString('O'), 'startup', $Action, $Status, $Seconds.ToString('0.000', [Globalization.CultureInfo]::InvariantCulture), (Protect-VisoryText $Message))
    $escaped = foreach ($cell in $cells) {
        if ($cell -match '^[=+\-@\t\r]') { $cell = "'" + $cell }
        '"' + $cell.Replace('"', '""') + '"'
    }
    Add-VisoryLine $script:CsvPath ($escaped -join ',')
    Write-VisoryReport
}

function Invoke-VisoryProcess([string]$File, [string[]]$Arguments, [string]$Area) {
    $script:ProcessOutput = [System.Collections.Generic.List[string]]::new()
    $info = [System.Diagnostics.ProcessStartInfo]::new()
    $info.FileName = $File
    $info.Arguments = ($Arguments | ForEach-Object { '"' + $_.Replace('"', '\"') + '"' }) -join ' '
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    $info.WorkingDirectory = $script:ProjectRoot
    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $info
    try {
        if (-not $process.Start()) { throw "Could not start $Area." }
        $stdout = $process.StandardOutput.ReadLineAsync()
        $stderr = $process.StandardError.ReadLineAsync()
        while ($null -ne $stdout -or $null -ne $stderr) {
            if ($null -ne $stdout -and $stdout.IsCompleted) {
                $line = $stdout.GetAwaiter().GetResult()
                if ($null -eq $line) { $stdout = $null }
                else {
                    if ($Area -eq 'sdk') { $script:ProcessOutput.Add((Protect-VisoryText $line)) }
                    if ($Area -eq 'app' -and $line -match '^\[[^\]]+\] \[(INFO|WARN|ERROR|FATAL|FAIL|SUCCESS|SKIP|DEBUG|RETRY)\] ') {
                        $level = $Matches[1]
                        $clean = Protect-VisoryText $line
                        if ($clean -notmatch '^(\[[^\]]+\] \[[A-Z]+\] )(.*)$') { throw 'Malformed app diagnostic line.' }
                        $markdown = $Matches[1] + (ConvertTo-VisoryMarkdown $Matches[2])
                        Add-VisoryLine $script:LogPath $markdown
                        if ($level -in @('ERROR', 'FATAL', 'FAIL')) { Add-VisoryLine $script:ErrorPath $markdown }
                        [Console]::WriteLine($clean)
                    }
                    else {
                        $level = if ($Area -eq 'build' -and $line -match '(?i)\berror\s+[A-Z]+\d+|:\s*Fehler\b') { 'ERROR' } else { 'INFO' }
                        Write-VisoryEvent $level $line $Area
                    }
                    $stdout = $process.StandardOutput.ReadLineAsync()
                }
            }
            if ($null -ne $stderr -and $stderr.IsCompleted) {
                $line = $stderr.GetAwaiter().GetResult()
                if ($null -eq $line) { $stderr = $null }
                else { Write-VisoryEvent 'ERROR' $line $Area; $stderr = $process.StandardError.ReadLineAsync() }
            }
            if ($null -ne $stdout -or $null -ne $stderr) { Start-Sleep -Milliseconds 10 }
        }
        $process.WaitForExit()
        if ($process.ExitCode -ne 0) { throw "$Area exited with code $($process.ExitCode)." }
    }
    finally { $process.Dispose() }
}
