param(
    [Parameter(Mandatory=$true)][string]$CleanRelease,
    [Parameter(Mandatory=$true)][string]$Output,
    [Parameter(Mandatory=$true)][ValidatePattern('^[a-zA-Z0-9._-]+$')][string]$ReleaseLabel,
    [string]$BuildReport,
    [switch]$UsePackageManifest
)
$ErrorActionPreference = 'Stop'
$CleanRelease = (Resolve-Path -LiteralPath $CleanRelease).Path
$Output = [IO.Path]::GetFullPath($Output)
if (Test-Path -LiteralPath $Output) { throw 'Output must be a new directory.' }
if ($Output.StartsWith($CleanRelease.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Output must be outside the source release.' }
if ($UsePackageManifest) {
    $releaseHashes = @{}
    foreach ($line in Get-Content -LiteralPath (Join-Path $CleanRelease 'PACKAGE MANIFEST.sha256')) {
        if ($line -notmatch '^([a-fA-F0-9]{64})  (.+)$') { throw 'Invalid upstream package manifest.' }
        if ($releaseHashes.ContainsKey($Matches[2])) { throw 'Duplicate manifest path.' }
        $releaseHashes[$Matches[2]] = $Matches[1]
    }
} else {
if (!$BuildReport) { $BuildReport = "$CleanRelease-build-report.json" }
# Consume only the clean release produced by the upstream build_release_034.py pipeline.
$report = Get-Content -LiteralPath $BuildReport -Raw | ConvertFrom-Json
if (!$report.copied_hashes -or !$report.world) { throw 'Missing upstream clean-release build report.' }
}
$app = Join-Path $CleanRelease 'runtime/client-opendaoc/app'
foreach ($required in @('game.dll','connect.exe','paths.dat')) {
    if (!(Test-Path -LiteralPath (Join-Path $app $required))) { throw "Missing client file: $required" }
}
if (Test-Path -LiteralPath (Join-Path $CleanRelease 'runtime/account.txt')) { throw 'Source release has been played. Use a fresh clean release.' }
& (Join-Path $PSScriptRoot 'build.ps1')
$payload = Join-Path $Output 'payload'
New-Item -ItemType Directory -Path (Join-Path $payload 'client') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'artifacts/Setup-LAN.exe') -Destination $Output
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'artifacts/OfflineDAoC-LAN.exe') -Destination $payload
foreach ($file in Get-ChildItem -LiteralPath $app -Recurse -Force) {
    if ($file.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Client contains a link: $($file.Name)" }
    if ($file.PSIsContainer) { continue }
    # Explorer thumbnail caches are not game assets; official archives may include them.
    if ($file.Name -ieq 'Thumbs.db') { continue }
    $relative = $file.FullName.Substring($app.Length + 1).Replace('\','/')
    if ($relative -match '(?i)(^|/)(account\.txt|user\.dat|logs?|screenshots?|backups?)(/|$)|\.(log|dmp|bak|db|sqlite3)$') { throw "Unexpected personal file in clean client: $relative" }
    $key = "client-opendaoc/app/$relative"
    if ($UsePackageManifest) {
        $expectedHash = $releaseHashes["runtime/$key"]
        if (!$expectedHash -or (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash -ne $expectedHash) { throw "Client differs from release manifest: $relative" }
    } else {
    $record = $report.copied_hashes.PSObject.Properties[$key]
    if (!$record) { throw "Client file is not recorded in upstream build report: $relative" }
    # Upstream deliberately rewrites paths.dat to a fresh profile after copying.
    if ($relative -ne 'paths.dat' -and (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash -ne $record.Value) { throw "Client changed since clean build: $relative" }
    }
    $destination = Join-Path (Join-Path $payload 'client') $relative
    New-Item -ItemType Directory -Force -Path (Split-Path $destination) | Out-Null
    Copy-Item -LiteralPath $file.FullName -Destination $destination
}
# Keep LAN preferences separate from the host's client preferences.
Set-Content -LiteralPath (Join-Path $payload 'client/paths.dat') -Encoding ASCII -Value "[paths]`r`nsettings=OfflineDAoCLAN"
Set-Content -LiteralPath (Join-Path $payload 'release.txt') -Encoding ASCII -Value "Offline DAoC $ReleaseLabel LAN client. Use the matching server release and edition."
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'README.md') -Destination $payload
foreach ($notice in @('LICENSE','THIRD_PARTY.md')) {
    $path = Join-Path $CleanRelease $notice
    if (!(Test-Path -LiteralPath $path)) { throw "Missing release notice: $notice. Assemble the clean upstream release first." }
    if ($UsePackageManifest -and (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $releaseHashes[$notice]) { throw "Release notice checksum mismatch: $notice" }
    Copy-Item -LiteralPath $path -Destination $payload
}
$manifest = foreach ($file in Get-ChildItem -LiteralPath $payload -Recurse -File | Sort-Object FullName) {
    $relative = $file.FullName.Substring($payload.Length + 1).Replace('\','/')
    "$( (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant() )`t$relative"
}
Set-Content -LiteralPath (Join-Path $payload 'manifest.sha256') -Value $manifest -Encoding UTF8
Write-Output "Client-only installer folder ready: $Output. Distribute Setup-LAN.exe together with payload/."
