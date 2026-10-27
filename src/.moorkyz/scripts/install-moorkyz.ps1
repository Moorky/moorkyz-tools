# Optional installation: adds a lightweight `moorkyz` function to your CURRENT
# USER/CURRENT HOST PowerShell profile. Run explicitly and review before use.
# Example: & .\install-moorkyz.ps1
param([switch]$Uninstall)
$ErrorActionPreference = 'Stop'
$profilePath = $PROFILE.CurrentUserCurrentHost
$startMarker = '# >>> moorkyz CLI (managed) >>>'
$endMarker = '# <<< moorkyz CLI (managed) <<<'
# .NET detects BOMs and reads BOM-free files as UTF-8 on every PowerShell host.
# Get-Content defaults to ANSI on Windows PowerShell and corrupts UTF-8 profiles.
$existing = ''
if (Test-Path -LiteralPath $profilePath) {
    try {
        $existing = [IO.File]::ReadAllText($profilePath, ([System.Text.UTF8Encoding]::new($false, $true)))
    } catch [System.Text.DecoderFallbackException] {
        # Profiles created by Windows PowerShell can use the local ANSI page.
        $ansi = [System.Text.Encoding]::GetEncoding([Globalization.CultureInfo]::CurrentCulture.TextInfo.ANSICodePage)
        $existing = [IO.File]::ReadAllText($profilePath, $ansi)
    }
}
$starts = [regex]::Matches($existing, '(?m)^' + [regex]::Escape($startMarker) + '\r?$')
$ends = [regex]::Matches($existing, '(?m)^' + [regex]::Escape($endMarker) + '\r?$')
if ($starts.Count -ne $ends.Count -or $starts.Count -gt 1 -or ($starts.Count -eq 1 -and $starts[0].Index -ge $ends[0].Index)) {
    throw "PowerShell profile contains ambiguous or out-of-order Moorkyz markers. Edit manually: $profilePath"
}
if ($starts.Count -eq 1) {
    $first = $starts[0].Index
    $after = $ends[0].Index + $ends[0].Length
    $existing = $existing.Substring(0, $first) + $existing.Substring($after)
} elseif ($Uninstall) {
    Write-Host "No Moorkyz function found: $profilePath"
    return
}
if (-not $Uninstall) {
    $cli = Join-Path $PSScriptRoot '..\..\..\moorkyz.cjs'
    if (-not (Test-Path -LiteralPath $cli)) { throw "Missing CLI: $cli" }
    $safePath = ([IO.Path]::GetFullPath($cli)).Replace("'", "''")
    $block = @'
# >>> moorkyz CLI (managed) >>>
function global:moorkyz {
    & node '__MOORKYZ_CLI__' @args
}
# <<< moorkyz CLI (managed) <<<
'@
    $block = $block.Replace('__MOORKYZ_CLI__', $safePath)
    $existing = $existing + [Environment]::NewLine + [Environment]::NewLine + $block + [Environment]::NewLine
}
$folder = Split-Path -Parent $profilePath
if (-not (Test-Path -LiteralPath $folder)) { New-Item -ItemType Directory -Force -Path $folder | Out-Null }
# A BOM lets Windows PowerShell 5.1 read the Unicode profile correctly too.
$temporary = Join-Path $folder ('.moorkyz-profile-' + [Guid]::NewGuid().ToString('N') + '.tmp')
try {
    [IO.File]::WriteAllText($temporary, $existing, ([System.Text.UTF8Encoding]::new($true)))
    if (Test-Path -LiteralPath $profilePath) {
        [IO.File]::Replace($temporary, $profilePath, [System.Management.Automation.Language.NullString]::Value)
    } else {
        [IO.File]::Move($temporary, $profilePath)
    }
} finally {
    if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary }
}
Write-Host "Updated: $profilePath"
if ($Uninstall) { Write-Host 'Moorkyz function removed for new PowerShell sessions.' }
else { Write-Host "Restart PowerShell or run: . `$PROFILE" }
