$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
$output = Join-Path (Get-Location) 'artifacts\portable-win-x64'
if (Test-Path $output) { throw "Output already exists. Preserve or explicitly remove this generated directory before rebuilding: $output" }
dotnet publish .\src\Moorkyz.MediaRecover.App\Moorkyz.MediaRecover.App.csproj -c Release -r win-x64 --self-contained true -p:RuntimeFrameworkVersion=10.0.11 -p:PublishSingleFile=false -p:PublishTrimmed=false -p:DebugType=None -o $output --nologo
if ($LASTEXITCODE -ne 0) { throw "Publish failed: $LASTEXITCODE" }
Copy-Item .\README.md, .\LICENSE, .\THIRD-PARTY-NOTICES.md -Destination $output
New-Item -ItemType Directory -Path (Join-Path $output 'docs') | Out-Null
Copy-Item .\docs\architecture.md, .\docs\verification.md -Destination (Join-Path $output 'docs')
$nuget = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $env:USERPROFILE '.nuget\packages' }
$packNames = @('microsoft.netcore.app.runtime.win-x64', 'microsoft.windowsdesktop.app.runtime.win-x64')
foreach ($pack in $packNames) {
    $base = Join-Path $nuget $pack
    $version = Get-Item (Join-Path $base '10.0.11')
    if (-not $version) { throw "Runtime-pack license source missing: $base" }
    $license = Get-ChildItem $version.FullName -File -Recurse | Where-Object { $_.Name -ieq 'LICENSE.TXT' -or $_.Name -ieq 'LICENSE' } | Select-Object -First 1
    $notices = Get-ChildItem $version.FullName -File -Recurse | Where-Object { $_.Name -ieq 'THIRD-PARTY-NOTICES.TXT' -or $_.Name -ieq 'ThirdPartyNotices.txt' } | Select-Object -First 1
    if (-not $notices -and $pack -eq 'microsoft.windowsdesktop.app.runtime.win-x64') { $notices = Get-Item .\third-party\wpf-10.0.11-NOTICES.txt }
    if (-not $license -or -not $notices) { throw "Missing required upstream license/notices in $($version.FullName)" }
    Copy-Item $license.FullName (Join-Path $output "$pack-LICENSE.txt")
    Copy-Item $notices.FullName (Join-Path $output "$pack-THIRD-PARTY-NOTICES.txt")
}
$zip = Join-Path (Get-Location) 'artifacts\Moorkyz-Media-Recover-0.1.0-win-x64.zip'
if (Test-Path $zip) { throw "Archive already exists: $zip" }
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($output, $zip, [System.IO.Compression.CompressionLevel]::Optimal, $false)
$hash = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content "$zip.sha256" "$hash  $([IO.Path]::GetFileName($zip))" -Encoding ascii
Write-Output "PASS portable EXE: $output\MoorkyzMediaRecover.exe"
Write-Output "PASS ZIP: $zip | SHA256: $hash"
