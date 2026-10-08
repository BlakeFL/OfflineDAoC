$ErrorActionPreference = 'Stop'
$fixture = Join-Path ([IO.Path]::GetTempPath()) ('OfflineDaoc-LanPackageTests-' + [guid]::NewGuid().ToString('N'))
$fixture = [IO.Path]::GetFullPath($fixture)
try {
    $release = Join-Path $fixture 'clean'
    $app = Join-Path $release 'runtime/client-opendaoc/app'
    New-Item -ItemType Directory -Force -Path $app | Out-Null
    $hashes = @{}
    foreach ($name in @('game.dll','connect.exe','paths.dat','Thumbs.db')) {
        $path = Join-Path $app $name
        Set-Content -LiteralPath $path -Value "fake fixture $name" -Encoding ASCII
        $hashes["client-opendaoc/app/$name"] = (Get-FileHash -LiteralPath $path).Hash.ToLowerInvariant()
    }
    foreach ($notice in @('LICENSE','THIRD_PARTY.md')) { Set-Content -LiteralPath (Join-Path $release $notice) -Value 'fixture notice' }
    @{ world = @{ cleaned = $true }; copied_hashes = $hashes } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath "$release-build-report.json"
    $output = Join-Path $fixture 'package'
    & (Join-Path $PSScriptRoot 'package.ps1') -CleanRelease $release -ReleaseLabel fixture -Output $output
    if (!(Test-Path -LiteralPath (Join-Path $output 'Setup-LAN.exe'))) { throw 'Setup missing' }
    [void][Reflection.Assembly]::Load([IO.File]::ReadAllBytes((Join-Path $output 'Setup-LAN.exe')))
    $installed = Join-Path $fixture 'installed'
    [OfflineDaoc.LanClient.Setup.Package]::Install((Join-Path $output 'payload'), $installed, [Action[int]]{})
    if (!(Test-Path -LiteralPath (Join-Path $installed 'client/connect.exe'))) { throw 'Installed client missing' }
    if ((Get-Content -LiteralPath (Join-Path $installed 'client/paths.dat') -Raw) -notmatch 'settings=OfflineDAoCLAN') { throw 'Profile not isolated' }
    if (Test-Path -LiteralPath (Join-Path $installed 'server')) { throw 'Server leaked into package' }
    if (Test-Path -LiteralPath (Join-Path $installed 'client/Thumbs.db')) { throw 'Thumbnail cache leaked into package' }
    Write-Output 'PASS clean package builds and installs with isolated profile and no server'
    $releaseManifest = foreach ($file in Get-ChildItem -LiteralPath $release -File -Recurse) {
        $relative = $file.FullName.Substring($release.Length + 1).Replace('\','/')
        "$( (Get-FileHash -LiteralPath $file.FullName).Hash.ToLowerInvariant() )  $relative"
    }
    Set-Content -LiteralPath (Join-Path $release 'PACKAGE MANIFEST.sha256') -Value $releaseManifest
    & (Join-Path $PSScriptRoot 'package.ps1') -CleanRelease $release -UsePackageManifest -ReleaseLabel fixture -Output (Join-Path $fixture 'sealed-package')
    Write-Output 'PASS sealed release manifest accepted without build report'
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $downloads = Join-Path $fixture 'downloads'
    New-Item -ItemType Directory -Path $downloads | Out-Null
    $zipPath = Join-Path $downloads 'OfflineDAoC-vfixture.zip'
    $zip = [IO.Compression.ZipFile]::Open($zipPath, [IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($file in Get-ChildItem -LiteralPath $release -File -Recurse) {
            $relative = $file.FullName.Substring($release.Length + 1).Replace('\','/')
            [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $file.FullName, "OfflineDAoC-vfixture/$relative")
        }
        # Unselected host data must never be extracted or installed.
        $entry = $zip.CreateEntry('OfflineDAoC-vfixture/runtime/data/opendaoc.sqlite3.db')
        $writer = [IO.StreamWriter]::new($entry.Open())
        try { $writer.Write('fixture server database') } finally { $writer.Dispose() }
    } finally { $zip.Dispose() }
    $downloadManifest = @{ Version='fixture'; RootFolder='OfflineDAoC-vfixture'; ArchiveBytes=(Get-Item -LiteralPath $zipPath).Length; ArchiveSHA256=(Get-FileHash -LiteralPath $zipPath).Hash }
    $downloadManifest | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $downloads 'download-manifest.json')
    & (Join-Path $PSScriptRoot 'package-download.ps1') -DownloadDirectory $downloads -Output (Join-Path $fixture 'download-package')
    if (Test-Path -LiteralPath (Join-Path $fixture 'download-package/payload/runtime/data')) { throw 'Server database copied from ZIP' }
    Write-Output 'PASS original ZIP workflow excludes host database'
    $downloadManifest.ArchiveSHA256 = '0' * 64
    $downloadManifest | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $downloads 'download-manifest.json')
    $rejected = $false
    try { & (Join-Path $PSScriptRoot 'package-download.ps1') -DownloadDirectory $downloads -Output (Join-Path $fixture 'bad-download') }
    catch { if ($_.Exception.Message -notmatch 'Archive checksum mismatch') { throw }; $rejected = $true }
    if (!$rejected) { throw 'Corrupt download accepted' }
    Write-Output 'PASS wrong archive checksum rejected'
    Set-Content -LiteralPath (Join-Path $app 'game.dll') -Value 'tampered'
    $rejected = $false
    try { & (Join-Path $PSScriptRoot 'package.ps1') -CleanRelease $release -UsePackageManifest -ReleaseLabel fixture -Output (Join-Path $fixture 'sealed-tampered') }
    catch { if ($_.Exception.Message -notmatch 'differs from release manifest') { throw }; $rejected = $true }
    if (!$rejected) { throw 'Modified sealed asset accepted' }
    Write-Output 'PASS sealed release asset tampering rejected'
    $rejected = $false
    try { & (Join-Path $PSScriptRoot 'package.ps1') -CleanRelease $release -ReleaseLabel fixture -Output (Join-Path $fixture 'tampered') }
    catch { if ($_.Exception.Message -notmatch 'changed since clean build') { throw }; $rejected = $true }
    if (!$rejected) { throw 'Modified asset accepted' }
    Write-Output 'PASS modified release asset rejected'
    Set-Content -LiteralPath (Join-Path $release 'runtime/account.txt') -Value 'fixture'
    $rejected = $false
    try { & (Join-Path $PSScriptRoot 'package.ps1') -CleanRelease $release -ReleaseLabel fixture -Output (Join-Path $fixture 'played') }
    catch { if ($_.Exception.Message -notmatch 'has been played') { throw }; $rejected = $true }
    if (!$rejected) { throw 'Played release accepted' }
    Write-Output 'PASS played release rejected'
}
finally {
    $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\'
    if (!$fixture.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase) -or
        (Split-Path $fixture -Leaf) -notmatch '^OfflineDaoc-LanPackageTests-[0-9a-f]{32}$') { throw 'Unsafe fixture cleanup target' }
    if (Test-Path -LiteralPath $fixture) { Remove-Item -LiteralPath $fixture -Recurse -Force }
}
