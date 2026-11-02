#requires -Version 5.1
[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
param(
    [Parameter(Mandatory = $true)][string]$Target,
    [string]$HandlePath,
    [ValidateRange(1, 2147483647)][int]$CloseProcessId,
    [switch]$Json,
    [switch]$NonInteractive,
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'helpers\FolderUnlock.ps1')

try {
    $resolved = Resolve-UnlockTarget $Target
    if ($DryRun -or $WhatIfPreference) {
        Write-Output "Preview only: inspect '$resolved'. No scanner, process, download or file change."
        return
    }
    if ($CloseProcessId -and $NonInteractive) {
        throw 'Closing an application requires an interactive confirmation. Inspection is non-interactive.'
    }
    $scanner = Resolve-UnlockScanner $HandlePath
    $report = Get-UnlockReport $resolved $scanner
    if ($CloseProcessId) {
        $candidate = @($report.Processes | Where-Object { $_.Id -eq $CloseProcessId })
        if ($candidate.Count -ne 1) { throw 'The selected PID does not hold a matching file handle.' }
        if (-not $candidate[0].CanRequestClose) { throw $candidate[0].Reason }
        if ($PSCmdlet.ShouldProcess("PID $CloseProcessId ($($candidate[0].Name))", 'Request normal application closure')) {
            $answer = Read-Host "Save your work first. Type CLOSE $CloseProcessId to request normal closure"
            if ($answer -cne "CLOSE $CloseProcessId") { throw 'Closure was not confirmed; no action taken.' }
            $result = Request-UnlockClose $candidate[0] $resolved $scanner
            $report = Get-UnlockReport $resolved $scanner
            $report | Add-Member -NotePropertyName CloseOutcome -NotePropertyValue $result
            if (-not $Json) { Write-Output $result }
        }
    }
    if ($Json) {
        $report | ConvertTo-Json -Depth 6
    }
    else {
        Write-Output "Moorkyz Folder Unlock: $resolved"
        if ($report.Handles.Count -eq 0) {
            Write-Output 'No matching file handles were reported. This does not prove that permissions or other locks allow an operation.'
        }
        else {
            $report.Processes | Format-Table Id, Name, CanRequestClose, Reason -AutoSize
            $report.Handles | Format-Table ProcessId, Path -AutoSize
            Write-Output 'Close applications normally, then scan again. Use -CloseProcessId <PID> only after saving your work.'
        }
    }
}
catch {
    Write-Error -Message $_.Exception.Message -ErrorAction Continue
    exit 1
}
