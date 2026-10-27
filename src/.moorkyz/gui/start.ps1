param(
    [Parameter(Mandatory = $true)][string]$Root,
    [Parameter(Mandatory = $true)][string]$EntryPoint,
    [Parameter(Mandatory = $true)][string]$NodePath,
    [Parameter(Mandatory = $true)][string]$ProjectData
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase, System.Xaml
$references = @(
    [System.Windows.Window].Assembly.Location,
    [System.Windows.Media.Brush].Assembly.Location,
    [System.Windows.Threading.Dispatcher].Assembly.Location,
    [System.Xaml.XamlReader].Assembly.Location,
    [System.Xml.XmlReader].Assembly.Location
)
$sources = @('Motion.cs', 'MoorkyzGui.cs') | ForEach-Object { Join-Path $PSScriptRoot $_ }
Add-Type -Path $sources -ReferencedAssemblies $references
$projects = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($ProjectData)) | ConvertFrom-Json
$window = [Moorkyz.ControlGui]::new(
    $PSScriptRoot, $Root, $EntryPoint, $NodePath,
    [string[]]$projects.names, [string]$projects.defaultProject
)
$window.Window.ShowDialog() | Out-Null
