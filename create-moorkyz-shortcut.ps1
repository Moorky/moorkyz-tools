$ErrorActionPreference = 'Stop'

$root = [IO.Path]::GetFullPath($PSScriptRoot)
$launcher = Join-Path $root 'moorkyz.cmd'
$icon = Join-Path $root 'src\.moorkyz\public\favicon.ico'
$shortcutPath = Join-Path $root 'moorkyz.lnk'
$commandPrompt = Join-Path $env:SystemRoot 'System32\cmd.exe'

foreach ($requiredPath in @($launcher, $icon, $commandPrompt)) {
    if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
        throw "Required file not found: $requiredPath"
    }
}
if (Test-Path -LiteralPath $shortcutPath) {
    throw "Refusing to overwrite the existing shortcut: $shortcutPath"
}

$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $commandPrompt
$shortcut.Arguments = '/d /k ""' + $launcher + '""'
$shortcut.WorkingDirectory = $root
$shortcut.IconLocation = "$icon,0"
$shortcut.Description = 'Moorkyz project CLI'
$shortcut.Save()

Write-Host "Created: $shortcutPath"
