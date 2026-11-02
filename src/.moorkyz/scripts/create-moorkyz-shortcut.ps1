$ErrorActionPreference = 'Stop'

$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..'))
$launcher = Join-Path $root 'src\.moorkyz\app\launch.ps1'
$icon = Join-Path $root 'src\.moorkyz\public\favicon.ico'
$shortcutPath = Join-Path $PSScriptRoot 'moorkyz.lnk'
$hostPath = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'

foreach ($requiredPath in @($launcher, $icon, $hostPath)) {
    if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
        throw "Required file not found: $requiredPath"
    }
}
if (Test-Path -LiteralPath $shortcutPath) {
    throw "Refusing to overwrite the existing shortcut: $shortcutPath"
}

$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $hostPath
$shortcut.Arguments = '-NoLogo -NoProfile -WindowStyle Hidden -File "' + $launcher + '" -Root "' + $root + '"'
$shortcut.WorkingDirectory = $root
$shortcut.IconLocation = "$icon,0"
$shortcut.Description = 'Moorkyz local control center'
$shortcut.Save()

Write-Host "Created: $shortcutPath"
