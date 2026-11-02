param(
    [Parameter(Mandatory = $true)][string]$Root
)

$ErrorActionPreference = 'Stop'
try {
    $node = (Get-Command node.exe -ErrorAction Stop).Source
    $entry = Join-Path ([IO.Path]::GetFullPath($Root)) 'moorkyz'
    if (-not (Test-Path -LiteralPath $entry -PathType Leaf)) { throw "Required launcher is missing: $entry" }
    Set-Location -LiteralPath $Root
    $output = & $node $entry launch 2>&1
    if ($LASTEXITCODE -ne 0) { throw "Control center startup failed (exit $LASTEXITCODE).`n$($output -join [Environment]::NewLine)" }
} catch {
    Add-Type -AssemblyName PresentationFramework
    [System.Windows.MessageBox]::Show($_.Exception.Message, 'Moorkyz startup failed', 'OK', 'Error') | Out-Null
    exit 1
}
