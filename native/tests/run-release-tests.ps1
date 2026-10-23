param(
    [Parameter(Mandatory=$true)][string]$Installer,
    [Parameter(Mandatory=$true)][string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
$testRoot = [IO.Path]::GetFullPath($OutputDirectory)
$setup = [IO.Path]::GetFullPath($Installer)
New-Item -ItemType Directory -Path $testRoot -Force | Out-Null

function Invoke-CheckProcess {
    param([string]$File, [string]$Arguments)
    $process = Start-Process -FilePath $File -ArgumentList $Arguments -WindowStyle Hidden -Wait -PassThru
    if ($process.ExitCode -ne 0) {
        throw "Verification failed: $Arguments. See the error report in $testRoot."
    }
}

$extracted = Join-Path $testRoot 'application'
Invoke-CheckProcess $setup ('"--extract-test=' + $extracted + '"')
$application = Join-Path $extracted 'VocabularyDatabase.exe'
Invoke-CheckProcess $application ('--self-test "' + (Join-Path $testRoot 'app') + '"')
Invoke-CheckProcess $application ('--ui-test "' + (Join-Path $testRoot 'ui') + '"')
Invoke-CheckProcess $application ('--fixes-test "' + (Join-Path $testRoot 'fixes') + '"')
Invoke-CheckProcess $setup ('"--upgrade-test=' + (Join-Path $testRoot 'upgrade') + '"')

$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compiler = Join-Path $framework 'csc.exe'
$references = @(
    '/reference:System.Core.dll',
    '/reference:System.Web.Extensions.dll',
    '/reference:System.Xaml.dll',
    '/reference:Microsoft.CSharp.dll',
    ('/reference:' + (Join-Path $framework 'WPF\PresentationCore.dll')),
    ('/reference:' + (Join-Path $framework 'WPF\PresentationFramework.dll')),
    ('/reference:' + (Join-Path $framework 'WPF\WindowsBase.dll'))
)
$brandingRunner = Join-Path $testRoot 'BrandingTests.exe'
& $compiler /nologo /target:exe /optimize+ /warnaserror+ "/out:$brandingRunner" @references (Join-Path $PSScriptRoot 'BrandingTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Branding test build failed' }
& $brandingRunner $application $setup (Join-Path $testRoot 'branding')
if ($LASTEXITCODE -ne 0) { throw 'Branding verification failed' }

$reports = @(
    'app\native-test-results.json',
    'ui\ui-regression-results.json',
    'fixes\bug-fix-results.json',
    'upgrade\native-upgrade-results.json',
    'branding\branding-results.json'
)
$results = @($reports | ForEach-Object { Get-Content -LiteralPath (Join-Path $testRoot $_) -Raw | ConvertFrom-Json })
$summary = [ordered]@{
    version = '1.3'
    passed = ($results | Measure-Object -Property passed -Sum).Sum
    tests = $results
}
$summary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $testRoot 'release-results.json') -Encoding utf8
Write-Output ('Release checks passed: ' + $summary.passed)
