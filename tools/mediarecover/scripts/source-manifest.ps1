$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
$root = (Get-Location).Path
$definition = Get-Content .\source-files.json -Raw | ConvertFrom-Json
$paths = New-Object 'System.Collections.Generic.SortedSet[string]' ([StringComparer]::Ordinal)
foreach ($path in $definition.files) {
    if (-not $paths.Add([string]$path)) { throw "Duplicate source path: $path" }
}
$files = @()
$tree = New-Object System.Text.StringBuilder
$utf8 = New-Object System.Text.UTF8Encoding($false, $true)
foreach ($relative in $paths) {
    if ($relative -match '(^/|\\|(^|/)\.\.(/|$)|^[A-Za-z]:|(^|/)(bin|obj|artifacts|\.fixtures|\.git)(/|$))') { throw "Unsafe source path: $relative" }
    $path = [IO.Path]::GetFullPath((Join-Path $root $relative.Replace('/', '\')))
    if (-not $path.StartsWith($root + '\', [StringComparison]::OrdinalIgnoreCase)) { throw "Source escaped root: $relative" }
    $file = Get-Item -LiteralPath $path
    if (($file.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw "Source link blocked: $relative" }
    for ($current = $file.Directory; $current -and $current.FullName -ne $root; $current = $current.Parent) {
        if (($current.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw "Source link blocked: $relative" }
    }
    if ($file.PSIsContainer -or $file.Length -gt 1048576) { throw "Not a bounded source file: $relative" }
    $text = [IO.File]::ReadAllText($path, $utf8)
    if ($text.Contains([string][char]0)) { throw "Binary/NUL source blocked: $relative" }
    $sha = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
    $files += @{ path = $relative; sha256 = $sha; bytes = $file.Length }
    [void]$tree.Append($relative).Append("`n").Append($sha).Append("`n")
}
$sha256 = [Security.Cryptography.SHA256]::Create()
try { $treeHash = ([BitConverter]::ToString($sha256.ComputeHash($utf8.GetBytes($tree.ToString())))).Replace('-', '').ToLowerInvariant() }
finally { $sha256.Dispose() }
New-Item -ItemType Directory -Path .\artifacts -Force | Out-Null
@{
    product = 'Moorkyz Media Recover'
    repository = 'Moorky/moorkyz-tools'
    sourceDirectory = 'tools/mediarecover'
    version = '0.1.0'
    provenance = 'Canonical source maintained in tools/mediarecover in the tools repository. This manifest does not assert a public release.'
    treeHashAlgorithm = 'SHA-256 over ordinal-sorted UTF-8 path LF lowercase-file-SHA256 LF'
    treeSha256 = $treeHash
    files = $files
} | ConvertTo-Json -Depth 6 | Set-Content .\artifacts\source-manifest.json -Encoding utf8
Write-Output "PASS $($files.Count) bounded UTF-8 distributable text files; source tree SHA256: $treeHash"
