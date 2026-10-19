param(
    [Parameter(Mandatory=$true)][string]$Application,
    [Parameter(Mandatory=$true)][string]$OutputDirectory
)
$ErrorActionPreference='Stop'
$testOutput=[IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $testOutput -Force | Out-Null
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$testRunner=Join-Path $testOutput 'AISetupTests.exe'
& $compiler /nologo /target:exe /optimize+ "/out:$testRunner" /reference:System.Net.Http.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll /reference:System.Web.Extensions.dll (Join-Path $PSScriptRoot 'AISetupTests.cs')
if($LASTEXITCODE -ne 0){throw 'AI setup test build failed'}
& $testRunner ([IO.Path]::GetFullPath($Application)) $testOutput
if($LASTEXITCODE -ne 0){throw 'AI setup regression checks failed'}
