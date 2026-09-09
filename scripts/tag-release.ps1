<#
.SYNOPSIS
    Tags the current commit as a release, using the version the code already declares.

.DESCRIPTION
    v0.4.0 was tagged by hand, and the message recorded the SHA-256 of the packaged zip. That was
    wrong in a way worth writing down: csc stamps a fresh module GUID into every compile, so no
    rebuild reproduces those bytes, and the claim was invalidated within the hour by an unrelated
    script that happened to repackage. A tag should record what is reproducible -- the commit, the
    version, and what changed -- and nothing that decays.

    So this script deliberately does not hash anything. The commit is the identity; the published
    artifact is whatever was built from it.

    Checks before tagging, each of which has already gone wrong at least once in this project:
      - the version in Plugin.cs and manifest.json agree
      - the working tree is clean, so the tag names something reproducible
      - HEAD is on origin, so the tag does not point at a commit nobody else has
      - the tag does not already exist
      - the changelog has a section for this version

.EXAMPLE
    .\scripts\tag-release.ps1
.EXAMPLE
    .\scripts\tag-release.ps1 -Push
.EXAMPLE
    .\scripts\tag-release.ps1 -Version 0.2.0 -Commit a9fb74c -Inferred -Push
#>
[CmdletBinding()]
param(
    # Defaults to the version declared in src/Plugin.cs.
    [string]$Version,

    # Defaults to HEAD. Used for backfilling tags on releases that predate this script.
    [string]$Commit = 'HEAD',

    # Records in the message that the commit was inferred from the upload time rather than known.
    [switch]$Inferred,

    # Push the tag to origin. Without it the tag is local and the push command is printed.
    [switch]$Push
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot

# --- the version comes from the code, not from an argument, unless backfilling -----------------

if (-not $Version) {
    $pluginSource = Join-Path $repoRoot 'src\Plugin.cs'
    $pluginText = [System.IO.File]::ReadAllText($pluginSource)
    if ($pluginText -notmatch 'PluginVersion\s*=\s*"([^"]+)"') {
        throw "Could not read PluginVersion from '$pluginSource'."
    }
    $Version = $Matches[1]

    $manifest = Get-Content (Join-Path $repoRoot 'packaging\manifest.json') -Raw | ConvertFrom-Json
    if ($manifest.version_number -ne $Version) {
        throw "Plugin.cs says $Version but manifest.json says $($manifest.version_number). These must agree before tagging."
    }
}

$tag = "v$Version"

# --- refuse to tag something that is not reproducible ------------------------------------------

if (git tag -l $tag) {
    throw "$tag already exists. Tags are not moved once pushed; bump the version instead."
}

if ($Commit -eq 'HEAD') {
    $dirty = git status --porcelain
    if ($dirty) {
        throw "The working tree has uncommitted changes, so $tag would name something you cannot rebuild. Commit or stash first."
    }
}

$sha = (git rev-parse $Commit).Trim()

# A tag pointing at a commit only this machine has is a tag that means nothing to anyone else.
$onOrigin = git branch -r --contains $sha 2>$null
if (-not $onOrigin) {
    throw "$sha is not on any remote branch. Push the commit before tagging it."
}

# --- the message is the changelog, which is already written for players ------------------------

$changelog = [System.IO.File]::ReadAllText((Join-Path $repoRoot 'packaging\CHANGELOG.md'), [System.Text.Encoding]::UTF8)
$pattern = "(?ms)^##\s+" + [regex]::Escape($Version) + '\s*$(.*?)(?=^##\s|\z)'
$match = [regex]::Match($changelog, $pattern)
if (-not $match.Success) {
    throw "packaging/CHANGELOG.md has no '## $Version' section. Write the changelog before tagging."
}
$notes = $match.Groups[1].Value.Trim()

$provenance = if ($Inferred) {
    "`n`nCommit inferred from the Thunderstore upload time: it was the head of main when this`nversion was published. Not recorded at the time, because tagging began at 0.4.0."
} else {
    ""
}

$message = "Planetary Anomalies $Version`n`n$notes$provenance"

$messageFile = Join-Path ([System.IO.Path]::GetTempPath()) ("pa-tag-" + [Guid]::NewGuid().ToString('N') + ".txt")
[System.IO.File]::WriteAllText($messageFile, $message, (New-Object System.Text.UTF8Encoding($false)))

try {
    git tag -a $tag $sha -F $messageFile
    if ($LASTEXITCODE -ne 0) { throw "git tag failed." }
}
finally {
    Remove-Item $messageFile -Force -ErrorAction SilentlyContinue
}

Write-Host "Tagged $tag at $($sha.Substring(0,7))" -ForegroundColor Green

if ($Push) {
    git push origin $tag
    if ($LASTEXITCODE -ne 0) { throw "git push failed." }
    Write-Host "Pushed $tag to origin." -ForegroundColor Green
}
else {
    Write-Host ""
    Write-Host "Not pushed. To publish the tag:"
    Write-Host "  git push origin $tag"
}
