$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
dotnet run --project .\tests\Moorkyz.MediaRecover.Tests -c Release
if ($LASTEXITCODE -ne 0) { throw "Tests failed: $LASTEXITCODE" }
