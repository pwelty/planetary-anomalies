<#
.SYNOPSIS
    Checks the project's markdown for encoding damage.

.DESCRIPTION
    ROADMAP.md was once silently double-encoded end to end: a Get-Content -Raw round trip read it
    as the system codepage and wrote it back as UTF-8, turning every multiplication sign into two
    characters and every em dash into three. The result was still valid UTF-8, so nothing caught
    it, and the file is the project's design record.

    This lives in its own script for a reason worth stating. It was originally only part of
    package.ps1, which builds fresh and repackages every time it runs -- so checking the docs meant
    rebuilding the release artifact, which changed its bytes, which invalidated the hash recorded
    against a tag. A read-only check should not have a side effect that large. package.ps1 calls
    this; so can anyone, at no cost.

.EXAMPLE
    .\scripts\check-docs.ps1
#>
[CmdletBinding()]
param(
    # Collect problems into this list instead of throwing, for callers doing their own reporting.
    [System.Collections.Generic.List[string]]$Problems
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$found = New-Object System.Collections.Generic.List[string]

$strictUtf8 = New-Object System.Text.UTF8Encoding($false, $true)

$docs = @(Get-ChildItem -Path $repoRoot -Filter *.md -Recurse |
          Where-Object { $_.FullName -notmatch '\\(build|dist|\.git)\\' })

foreach ($doc in $docs) {
    $body = $null
    try {
        $body = $strictUtf8.GetString([System.IO.File]::ReadAllBytes($doc.FullName))
    }
    catch {
        $found.Add("$($doc.Name) is not valid UTF-8. $($_.Exception.Message)")
        continue
    }

    # A non-ASCII run beginning U+00C3 or U+00E2 is what a re-encoded UTF-8 lead byte looks like.
    # Neither occurs at the start of a run in real English prose.
    $mojibake = [regex]::Matches($body, "[\u00C3\u00E2][^\x00-\x7F]")
    if ($mojibake.Count -gt 0) {
        $found.Add("$($doc.Name) looks double-encoded: $($mojibake.Count) run(s) such as '$($mojibake[0].Value)'. Something read it as cp1252 and wrote it back as UTF-8.")
    }

    if ($body -match "\uFFFD") {
        $found.Add("$($doc.Name) contains a replacement character (U+FFFD); something was mangled before it reached here.")
    }
}

if ($PSBoundParameters.ContainsKey('Problems')) {
    foreach ($f in $found) { $Problems.Add($f) }
    return
}

if ($found.Count -gt 0) {
    Write-Host "Documentation problems:" -ForegroundColor Red
    foreach ($f in $found) { Write-Host "  - $f" -ForegroundColor Red }
    throw "$($found.Count) problem(s) found."
}

Write-Host "OK  $($docs.Count) markdown file(s) are clean UTF-8 with no double-encoding."
