param([string]$Output = (Join-Path $PSScriptRoot 'artifacts'))
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
if (!(Test-Path -LiteralPath $compiler)) { throw 'Windows .NET Framework compiler is required.' }
New-Item -ItemType Directory -Force -Path $Output | Out-Null
foreach ($program in @(@('Client.cs','OfflineDAoC-LAN.exe'), @('Setup.cs','Setup-LAN.exe'))) {
    & $compiler /nologo /target:winexe /optimize+ /platform:anycpu /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Core.dll /reference:Microsoft.CSharp.dll "/out:$(Join-Path $Output $program[1])" (Join-Path $PSScriptRoot $program[0])
    if ($LASTEXITCODE -ne 0) { throw "Compilation failed: $($program[0])" }
}
Write-Output "Built launcher and installer in $Output"
