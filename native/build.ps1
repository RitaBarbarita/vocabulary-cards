param(
    [string]$OutputDirectory = (Join-Path (Split-Path $PSScriptRoot -Parent) 'outputs'),
    [string]$SetupFileName = 'Vocabulary-Database-Setup-1.3.exe'
)
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$wpf = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\WPF'
$outputFolder = [IO.Path]::GetFullPath($OutputDirectory)

Push-Location -LiteralPath $PSScriptRoot
try {
    New-Item -ItemType Directory -Path build -Force | Out-Null
    New-Item -ItemType Directory -Path $outputFolder -Force | Out-Null
    $references = @(
        '/reference:System.Core.dll',
        '/reference:System.Web.Extensions.dll',
        '/reference:System.Net.Http.dll',
        '/reference:System.IO.Compression.dll',
        '/reference:System.IO.Compression.FileSystem.dll',
        '/reference:System.Xml.Linq.dll',
        '/reference:System.Xaml.dll',
        '/reference:Microsoft.CSharp.dll',
        ('/reference:' + (Join-Path $wpf 'PresentationCore.dll')),
        ('/reference:' + (Join-Path $wpf 'PresentationFramework.dll')),
        ('/reference:' + (Join-Path $wpf 'WindowsBase.dll'))
    )
    $resources = @('/resource:Theme.xaml,Theme.xaml')
    foreach ($icon in Get-ChildItem -LiteralPath Icons -Filter '*.png') {
        $resources += '/resource:' + $icon.FullName + ',Icons.' + $icon.Name
    }
    $appFiles = @(
        'AppInfo.cs', 'Startup.cs', 'Data.cs', 'Retro.cs',
        'Tables.cs', 'Biff.cs',
        'Migration.cs', 'MainWindow.cs', 'CardView.cs', 'EditorWindow.cs',
        'StudyImport.cs', 'NativeTests.cs', 'CenteredCardsPanel.cs',
        'UIRegressionTests.cs', 'BugFixTests.cs', 'Showcase.cs'
    )
    & $compiler /nologo /target:winexe /optimize+ /warn:4 /warnaserror+ /out:build\VocabularyDatabase.exe /win32icon:Assets\app-icon.ico /resource:fixtures\excel-biff8.xls,Fixtures.Excel @references @resources @appFiles
    if ($LASTEXITCODE -ne 0) { throw 'Application build failed' }
    Copy-Item -LiteralPath App.config -Destination build\VocabularyDatabase.exe.config -Force

    $package = @(
        '/resource:build\VocabularyDatabase.exe,Package.App',
        '/resource:App.config,Package.Config',
        '/resource:Assets\app-icon.png,Package.Icon',
        '/resource:Assets\app-icon.ico,Package.IconIco',
        '/resource:uninstall.cmd,Package.Uninstall'
    )
    & $compiler /nologo /target:winexe /optimize+ /warn:4 /warnaserror+ ('/out:' + (Join-Path $outputFolder $SetupFileName)) /win32icon:Assets\app-icon.ico @references @resources @package Installer.cs AppInfo.cs Retro.cs
    if ($LASTEXITCODE -ne 0) { throw 'Installer build failed' }
    Get-Item -LiteralPath (Join-Path $outputFolder $SetupFileName) | Select-Object FullName, Length
}
finally {
    Pop-Location
}
