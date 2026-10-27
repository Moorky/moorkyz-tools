$ErrorActionPreference = 'Stop'
$project = Split-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) -Parent
$startup = Join-Path $project 'src\scripts\startup'
$fixture = Join-Path $project ('src\.fixtures\startup-' + [Guid]::NewGuid().ToString('N'))
$originalPath = $env:PATH
$originalNoninteractive = $env:VISORY_NONINTERACTIVE
function Assert-Startup([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw "FAIL: $Message" }
}
try {
    $scripts = Join-Path $fixture 'src\scripts\startup'
    [void][IO.Directory]::CreateDirectory($scripts)
    Copy-Item (Join-Path $project 'RUN.cmd') $fixture
    Copy-Item (Join-Path $startup 'run.ps1'), (Join-Path $startup 'diagnostics.ps1') $scripts
    $documents = Join-Path $fixture 'src\utils\documents'
    [void][IO.Directory]::CreateDirectory((Join-Path $documents 'fonts'))
    Copy-Item (Join-Path $project 'src\utils\documents\document.css') $documents
    Copy-Item (Join-Path $project 'src\utils\documents\fonts\DMMono-Regular.ttf.b64') (Join-Path $documents 'fonts')
    $env:VISORY_NONINTERACTIVE = '1'
    $run = Join-Path $fixture 'RUN.cmd'
    Push-Location $env:TEMP
    try { $output = & $run -CheckOnly; $code = $LASTEXITCODE }
    finally { Pop-Location }
    Assert-Startup ($code -eq 0) 'RUN works from a different working directory.'
    $runs = Join-Path $fixture 'src\runtime\runs'
    Assert-Startup (@(Get-ChildItem -LiteralPath $fixture -Directory | Where-Object Name -ne 'src').Count -eq 0) 'startup creates directories only under src.'
    $first = @(Get-ChildItem -LiteralPath $runs -Directory)[0].FullName
    $log = Get-Content -LiteralPath (Join-Path $first 'logs\startup.md')
    Assert-Startup ($log[0] -match 'RUN started') 'logging starts before SDK checks.'
    Assert-Startup (($log -join "`n") -match 'Area: sdk') 'native stdout is logged.'
    Assert-Startup (($output -join "`n") -match 'RUN started') 'same startup event reaches terminal.'
    Assert-Startup (@(Import-Csv -LiteralPath (Join-Path $first 'reports\startup.csv')).Count -eq 2) 'initialization and prerequisite rows are saved.'
    foreach ($extension in @('html', 'md')) {
        $text = [IO.File]::ReadAllText((Join-Path $first "reports\startup.$extension"))
        Assert-Startup ($text -match 'Windows x64 and .NET 10 SDK available.') "$extension includes CSV row contents."
    }
    Assert-Startup ([IO.File]::ReadAllText((Join-Path $first 'reports\startup.html')) -match 'DM Mono' ) 'startup report shares offline monospace typography.'
    & $run -CheckOnly | Out-Null
    Assert-Startup ($LASTEXITCODE -eq 0 -and @(Get-ChildItem -LiteralPath $runs -Directory).Count -eq 2) 'repeat starts do not overwrite earlier runs.'
    $env:PATH = "$env:SystemRoot\System32;$env:SystemRoot"
    $failure = [Diagnostics.ProcessStartInfo]::new()
    $failure.FileName = $env:ComSpec
    $failure.Arguments = '/d /c ""' + $run + '" -CheckOnly"'
    $failure.UseShellExecute = $false
    $failure.RedirectStandardOutput = $true
    $failure.RedirectStandardError = $true
    $child = [Diagnostics.Process]::Start($failure)
    try {
        $stdout = $child.StandardOutput.ReadToEndAsync()
        $stderr = $child.StandardError.ReadToEndAsync()
        Assert-Startup ($child.WaitForExit(30000)) 'failed startup terminates without an interactive pause.'
        Assert-Startup ($child.ExitCode -ne 0) 'missing SDK is a failed launch, not success.'
        [void]$stdout.GetAwaiter().GetResult()
        [void]$stderr.GetAwaiter().GetResult()
    }
    finally { $child.Dispose() }
    $last = Get-ChildItem -LiteralPath $runs -Directory | Sort-Object Name | Select-Object -Last 1
    $errors = [IO.File]::ReadAllText((Join-Path $last.FullName 'failed\startup-errors.md'))
    Assert-Startup ($errors -match 'SDK is missing' -and $errors -match '\[ERROR\]') 'missing SDK saved to timestamped Markdown error log.'
    $rows = @(Import-Csv -LiteralPath (Join-Path $last.FullName 'reports\startup.csv'))
    Assert-Startup ($rows[-1].status -eq 'FAIL' -and $rows[-1].action -eq 'prerequisites') 'failed startup report names the failed stage.'
    $env:PATH = $originalPath
    . (Join-Path $startup 'diagnostics.ps1')
    foreach ($secret in @('password=VALUE-SECRET', 'https://user:pass@example.test/a?token=URL-SECRET', '{"Authorization": "Basic JSON-HEADER-SECRET", "Cookie": "JSON-COOKIE-SECRET"}')) {
        $clean = Protect-VisoryText $secret
        Assert-Startup ($clean -eq (Protect-VisoryText $clean)) 'redaction stays stable across multiple sinks.'
        Assert-Startup ($clean -notmatch 'VALUE-SECRET|URL-SECRET|JSON-HEADER-SECRET|JSON-COOKIE-SECRET|user:pass') 'structured secrets redacted.'
    }
    $script:ProjectRoot = $fixture
    $script:RunId = 'process-test'
    $script:LogPath = Join-Path $fixture 'process.md'
    $script:ErrorPath = Join-Path $fixture 'process-errors.md'
    $emitter = Join-Path $fixture 'emit.ps1'
    [IO.File]::WriteAllText($emitter, '[Console]::WriteLine("password=STARTUP-SECRET"); [Console]::Error.WriteLine("https://example.test/a?token=STDERR-SECRET"); exit 9')
    $failedProcess = $false
    try { Invoke-VisoryProcess (Join-Path $PSHOME 'powershell.exe') @('-NoProfile', '-File', $emitter) 'fixture' }
    catch { $failedProcess = $_.Exception.Message -match 'code 9' }
    Assert-Startup $failedProcess 'nonzero child exit stops startup.'
    $text = [IO.File]::ReadAllText($script:LogPath)
    Assert-Startup (-not $text.Contains('STARTUP-SECRET') -and -not $text.Contains('STDERR-SECRET')) 'both stdout and stderr redacted before persistence.'
    Assert-Startup ([IO.File]::ReadAllText($script:ErrorPath) -match '\[ERROR\]') 'stderr saved to error log.'
    Write-Output 'PASS startup: independent CWD, per-line terminal/file output, repeated runs, missing SDK, CSV/HTML/Markdown, child stdout/stderr, redaction and exit propagation.'
}
finally {
    $env:PATH = $originalPath
    $env:VISORY_NONINTERACTIVE = $originalNoninteractive
    if ([IO.Directory]::Exists($fixture)) { [IO.Directory]::Delete($fixture, $true) }
}
