param([string]$OutputRoot = 'src\artifacts\release')
# Reproducible portable release: one self-contained single-file EXE plus notices and documentation.
# Development-time tooling only; the published EXE does not need PowerShell.
$ErrorActionPreference = 'Stop'
Set-Location (Split-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) -Parent)
$runtime = '10.0.11'
$version = ([xml](Get-Content .\src\Directory.Build.props)).Project.PropertyGroup.Version
$output = Join-Path (Get-Location) "$OutputRoot\MoorkszVisoryMediaDownloader-$version-win-x64"
if (Test-Path $output) { throw "Output already exists. Remove this generated folder explicitly before rebuilding: $output" }

dotnet publish .\src\tools\desktop\MoorkszVisoryMediaDownloader.App.csproj -c Release -r win-x64 --self-contained true `
    -p:RuntimeFrameworkVersion=$runtime -p:PublishSingleFile=true -p:ContinuousIntegrationBuild=true -o $output --nologo
if ($LASTEXITCODE -ne 0) { throw "Publish failed: $LASTEXITCODE" }

$unexpected = Get-ChildItem $output -File | Where-Object { $_.Name -ne 'MoorkszVisoryMediaDownloader.exe' }
if ($unexpected) { throw "Unexpected files beside the single-file EXE: $($unexpected.Name -join ', ')" }

$docs = Join-Path $output 'docs'
$notices = Join-Path $output 'licenses'
New-Item -ItemType Directory -Path $docs, $notices | Out-Null
Copy-Item .\README.md, .\README.html, .\src\docs\ARCHITECTURE.md, .\src\docs\VISORY_INTEGRATION.md, .\src\docs\TEST_REPORT.md, .\src\docs\RELEASE_NOTES.md -Destination $docs
Copy-Item .\LICENSE (Join-Path $notices 'MoorkszVisoryMediaDownloader-LICENSE.txt')
Copy-Item .\src\docs\THIRD-PARTY-NOTICES.md $notices
Copy-Item .\src\utils\documents\fonts\DMMono-OFL.txt $notices

$nuget = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $env:USERPROFILE '.nuget\packages' }
$webview = Join-Path $nuget 'microsoft.web.webview2\1.0.4314.35'
$netcore = Join-Path $nuget "microsoft.netcore.app.runtime.win-x64\$runtime"
$desktop = Join-Path $nuget "microsoft.windowsdesktop.app.runtime.win-x64\$runtime"
$required = @(
    @{ Source = "$webview\LICENSE.txt"; Name = 'Microsoft.Web.WebView2-LICENSE.txt' },
    @{ Source = "$webview\NOTICE.txt"; Name = 'Microsoft.Web.WebView2-NOTICE.txt' },
    @{ Source = "$netcore\LICENSE.TXT"; Name = 'dotnet-runtime-LICENSE.txt' },
    @{ Source = "$netcore\THIRD-PARTY-NOTICES.TXT"; Name = 'dotnet-runtime-THIRD-PARTY-NOTICES.txt' },
    @{ Source = "$desktop\LICENSE"; Name = 'dotnet-windowsdesktop-LICENSE.txt' },
    @{ Source = '.\src\third-party\wpf-10.0.11-NOTICES.txt'; Name = 'dotnet-wpf-THIRD-PARTY-NOTICES.txt' }
)
foreach ($item in $required) {
    if (-not (Test-Path $item.Source)) { throw "Required license/notice missing: $($item.Source)" }
    Copy-Item $item.Source (Join-Path $notices $item.Name)
}

$exe = Join-Path $output 'MoorkszVisoryMediaDownloader.exe'
$hash = (Get-FileHash $exe -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content (Join-Path $output 'SHA256SUMS.txt') "$hash  MoorkszVisoryMediaDownloader.exe" -Encoding ascii
Write-Output "PASS portable EXE: $exe ($([Math]::Round((Get-Item $exe).Length / 1MB, 1)) MB)"
Write-Output "PASS SHA256: $hash"
