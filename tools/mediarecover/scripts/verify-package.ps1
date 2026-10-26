$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
Add-Type -AssemblyName System.IO.Compression.FileSystem
$root = (Resolve-Path .\artifacts\portable-win-x64).Path
$path = (Resolve-Path .\artifacts\Moorkyz-Media-Recover-0.1.0-win-x64.zip).Path
$archive = [IO.Compression.ZipFile]::OpenRead($path)
$hash = [Security.Cryptography.SHA256]::Create()
$count = 0
try {
    foreach ($entry in $archive.Entries) {
        $local = [IO.Path]::GetFullPath((Join-Path $root $entry.FullName.Replace('/', '\')))
        if (-not $local.StartsWith($root + '\', [StringComparison]::OrdinalIgnoreCase) -or -not (Test-Path -LiteralPath $local -PathType Leaf)) {
            throw "Unexpected ZIP entry: $($entry.FullName)"
        }
        $stream = $entry.Open()
        try { $actual = ([BitConverter]::ToString($hash.ComputeHash($stream))).Replace('-', '') }
        finally { $stream.Dispose() }
        $expected = (Get-FileHash -LiteralPath $local -Algorithm SHA256).Hash
        if ($actual -ne $expected) { throw "ZIP hash mismatch: $($entry.FullName)" }
        $count++
    }
    if ($count -ne @(Get-ChildItem $root -File -Recurse).Count) { throw 'Archive file count mismatch.' }
}
finally { $archive.Dispose(); $hash.Dispose() }
$runtime = Get-Content (Join-Path $root 'MoorkyzMediaRecover.runtimeconfig.json') -Raw | ConvertFrom-Json
if (@($runtime.runtimeOptions.includedFrameworks).Count -ne 2) { throw 'Expected self-contained .NET + WindowsDesktop frameworks.' }
$zipHash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
if (-not (Get-Content "$path.sha256" -Raw).StartsWith($zipHash)) { throw 'ZIP SHA256 file differs.' }
Write-Output "PASS all $count portable ZIP entry hashes, self-contained runtime metadata, and archive SHA256."
