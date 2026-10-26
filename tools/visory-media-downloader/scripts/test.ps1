param(
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Debug',
    [string]$Filter = '',
    [switch]$Heavy
)
# Runs the isolated automated suite (loopback HTTP server, mock provider, temporary .fixtures folder).
$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
if ($Heavy) { $env:VBD_HEAVY_TESTS = '1' }
$arguments = @('run', '--project', '.\tests\MoorkszVisoryMediaDownloader.Tests', '-c', $Configuration)
if ($Filter) { $arguments += @('--', $Filter) }
dotnet @arguments
if ($LASTEXITCODE -ne 0) { throw "Tests failed: $LASTEXITCODE" }
