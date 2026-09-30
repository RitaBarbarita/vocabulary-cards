$ErrorActionPreference='Stop'
Set-Location -LiteralPath $PSScriptRoot
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$wpf=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\WPF'
$outputFolder=Join-Path (Split-Path $PSScriptRoot -Parent) 'outputs'
New-Item -ItemType Directory -Path build -Force | Out-Null
New-Item -ItemType Directory -Path $outputFolder -Force | Out-Null
$references=@('/reference:System.Core.dll','/reference:System.Web.Extensions.dll','/reference:System.Net.Http.dll','/reference:System.IO.Compression.dll','/reference:System.IO.Compression.FileSystem.dll','/reference:System.Xml.Linq.dll','/reference:System.Xaml.dll','/reference:Microsoft.CSharp.dll',('/reference:'+$wpf+'\PresentationCore.dll'),('/reference:'+$wpf+'\PresentationFramework.dll'),('/reference:'+$wpf+'\WindowsBase.dll'))
$resources=@('/resource:Theme.xaml,Theme.xaml')
foreach($icon in Get-ChildItem -LiteralPath Icons -Filter '*.png'){$resources+=('/resource:'+ $icon.FullName +',Icons.'+$icon.Name)}
$appFiles=@('Startup.cs','Data.cs','Retro.cs','Languages.cs','Engine.cs','ProcessHelper.cs','AI.cs','Tables.cs','Biff.cs','Migration.cs','MainWindow.cs','CardView.cs','EditorWindow.cs','StudyImport.cs','NativeTests.cs','CenteredCardsPanel.cs','UIRegressionTests.cs')
& $compiler /nologo /target:winexe /optimize+ /out:build\EnglishVocabDB.exe /win32icon:Assets\app-icon.ico /resource:fixtures\excel-biff8.xls,Fixtures.Excel @references @resources @appFiles
if($LASTEXITCODE -ne 0){throw 'Native application build failed'}
Copy-Item -LiteralPath App.config -Destination build\EnglishVocabDB.exe.config -Force
$package=@('/resource:build\EnglishVocabDB.exe,Package.App','/resource:App.config,Package.Config','/resource:Assets\app-icon.png,Package.Icon','/resource:Assets\app-icon.ico,Package.IconIco','/resource:uninstall.cmd,Package.Uninstall')
& $compiler /nologo /target:winexe /optimize+ ('/out:'+(Join-Path $outputFolder 'English-Vocab-DB-Native-Setup-2.0.1.exe')) /win32icon:Assets\app-icon.ico @references @resources @package Installer.cs Retro.cs Languages.cs
if($LASTEXITCODE -ne 0){throw 'Native installer build failed'}
Get-Item -LiteralPath (Join-Path $outputFolder 'English-Vocab-DB-Native-Setup-2.0.1.exe') | Select-Object FullName,Length
