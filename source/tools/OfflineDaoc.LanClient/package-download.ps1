param(
    [Parameter(Mandatory=$true)][string]$DownloadDirectory,
    [Parameter(Mandatory=$true)][string]$Output
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$download = Get-Content -LiteralPath (Join-Path $DownloadDirectory 'download-manifest.json') -Raw | ConvertFrom-Json
if ($download.RootFolder -notmatch '^OfflineDAoC-v[a-zA-Z0-9._-]+$' -or $download.Version -notmatch '^[a-zA-Z0-9._-]+$' -or $download.ArchiveSHA256 -notmatch '^[a-fA-F0-9]{64}$') { throw 'Invalid download manifest.' }
$Output = [IO.Path]::GetFullPath($Output)
if (Test-Path -LiteralPath $Output) { throw 'Output must be a new directory.' }
$archivePath = Join-Path $DownloadDirectory ($download.RootFolder + '.zip')
if ((Get-Item -LiteralPath $archivePath).Length -ne $download.ArchiveBytes) { throw 'Archive size mismatch.' }
Write-Output 'Verifying original download SHA-256...'
if ((Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash -ne $download.ArchiveSHA256) { throw 'Archive checksum mismatch.' }
$stage = Join-Path $PSScriptRoot ('artifacts/download-stage-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $stage -Force | Out-Null
$archive = [IO.Compression.ZipFile]::OpenRead($archivePath)
try {
    $prefix = $download.RootFolder + '/'
    $seen = @{}
    foreach ($entry in $archive.Entries) {
        if (!$entry.FullName.StartsWith($prefix, [StringComparison]::Ordinal)) { throw 'Unexpected archive root.' }
        $relative = $entry.FullName.Substring($prefix.Length)
        if ($relative.EndsWith('/') -or !$relative) { continue }
        if (!($relative.StartsWith('runtime/client-opendaoc/app/', [StringComparison]::Ordinal) -or
            $relative -cin @('PACKAGE MANIFEST.sha256','LICENSE','THIRD_PARTY.md'))) { continue }
        if ($relative.Contains('\') -or $relative.Contains(':') -or $relative.Split('/') -contains '..' -or $relative.Split('/') -contains '.' -or $relative.Split('/') -contains '') { throw 'Unsafe archive path.' }
        if ($seen.ContainsKey($relative)) { throw 'Duplicate archive path.' }
        $seen[$relative] = $true
        $destination = [IO.Path]::GetFullPath((Join-Path $stage $relative))
        if (!$destination.StartsWith([IO.Path]::GetFullPath($stage).TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Extraction escaped staging.' }
        New-Item -ItemType Directory -Force -Path (Split-Path $destination) | Out-Null
        [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $destination, $false)
    }
    Write-Output "Extracted $($seen.Count) client/release files. Verifying and assembling LAN package..."
    & (Join-Path $PSScriptRoot 'package.ps1') -CleanRelease $stage -UsePackageManifest -ReleaseLabel $download.Version -Output $Output
    if (!(Test-Path -LiteralPath (Join-Path $Output 'payload/manifest.sha256'))) { throw 'Packaging did not complete.' }
    Set-Content -LiteralPath (Join-Path $Output 'SOURCE.txt') -Encoding ASCII -Value "Source: $($download.RootFolder).zip`r`nSHA256: $($download.ArchiveSHA256)`r`nClient-only derivative; original download and played installation unchanged."
} finally {
    $archive.Dispose()
    $safeRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'artifacts')).TrimEnd('\') + '\'
    $resolvedStage = [IO.Path]::GetFullPath($stage)
    if (!$resolvedStage.StartsWith($safeRoot, [StringComparison]::OrdinalIgnoreCase) -or (Split-Path $resolvedStage -Leaf) -notmatch '^download-stage-[0-9a-f]{32}$') { throw 'Unsafe staging cleanup target.' }
    Remove-Item -LiteralPath $resolvedStage -Recurse -Force
}
