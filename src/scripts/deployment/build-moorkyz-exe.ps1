param(
    [string]$OutputPath = (Join-Path $PSScriptRoot '..\..\.moorkyz\app\dist\Moorkyz.exe'),
    [string]$IconPath = (Join-Path $PSScriptRoot '..\..\.moorkyz\public\favicon.ico')
)

$ErrorActionPreference = 'Stop'
$source = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\.moorkyz\app\MoorkyzLauncher.cs'))
$output = [IO.Path]::GetFullPath($OutputPath)
$icon = [IO.Path]::GetFullPath($IconPath)
if ([IO.Path]::GetExtension($output) -ine '.exe') {
    throw 'OutputPath must name an .exe file.'
}
foreach ($required in @($source, $icon)) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) {
        throw "Required file not found: $required"
    }
}
if (Test-Path -LiteralPath $output) {
    throw "Refusing to overwrite existing output: $output"
}
$compiler = @(
    (Join-Path $env:SystemRoot 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'),
    (Join-Path $env:SystemRoot 'Microsoft.NET\Framework\v4.0.30319\csc.exe')
) | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
if (-not $compiler) {
    throw 'The Windows .NET Framework C# compiler is required. Nothing was installed.'
}

$temporary = Join-Path ([IO.Path]::GetTempPath()) ('moorkyz-exe-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temporary | Out-Null
try {
    $compiled = Join-Path $temporary 'Moorkyz.exe'
    & $compiler /nologo /target:winexe /reference:System.Windows.Forms.dll /platform:anycpu /optimize+ /warnaserror+ "/win32icon:$icon" "/out:$compiled" $source
    if ($LASTEXITCODE -ne 0) {
        throw "EXE compilation failed with exit code $LASTEXITCODE."
    }
    $sha256 = [Security.Cryptography.SHA256]::Create()
    $stream = [IO.File]::OpenRead($compiled)
    try {
        $hash = [BitConverter]::ToString($sha256.ComputeHash($stream)).Replace('-', '')
    } finally {
        $stream.Dispose()
        $sha256.Dispose()
    }
    $directory = [IO.Path]::GetDirectoryName($output)
    [IO.Directory]::CreateDirectory($directory) | Out-Null
    # File.Move refuses an existing destination, including one created during compilation.
    [IO.File]::Move($compiled, $output)
    Write-Output "PASS: Built unsigned launcher: $output"
    Write-Output "SHA256: $hash"
    Write-Output 'Requires the checkout and Node.js 22.18.0+. The GUI action also requires Windows PowerShell/WPF. No installer or publication was run.'
} finally {
    Remove-Item -LiteralPath $temporary -Recurse -Force
}
