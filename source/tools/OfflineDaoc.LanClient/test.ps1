$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'build.ps1')
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$output = Join-Path $PSScriptRoot 'artifacts/Tests.exe'
& $compiler /nologo /target:exe /main:Tests /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Core.dll /reference:Microsoft.CSharp.dll "/out:$output" (Join-Path $PSScriptRoot 'Client.cs') (Join-Path $PSScriptRoot 'Setup.cs') (Join-Path $PSScriptRoot 'Tests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
& $output
if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
