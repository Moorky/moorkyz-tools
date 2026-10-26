$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
dotnet build .\Moorkyz.MediaRecover.sln -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw "Build failed: $LASTEXITCODE" }
