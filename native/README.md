# Vocabulary Database 1.2

Windows vocabulary cards with examples, dialogues, images, flashcard practice and Excel import/export. The application works offline and stores the collection locally.

## Installation

Requires Windows 10/11 x64 and .NET Framework 4.8. Close the application, run `Vocabulary-Database-Setup-1.2.exe`, then open Vocabulary Database. The installer needs no additional configuration.

Updating retains cards, attachments, categories and learning progress. The installer reuses an existing program directory and updates the standard shortcuts. New installations use `%LOCALAPPDATA%\Programs\Vocabulary Database`.

## Data

Cards are stored in `%LOCALAPPDATA%\English Vocab DB\NativeData\vocabulary.json`, with attachments in the adjacent `images` directory. The original directory name is retained for compatibility. Successful saves keep the previous JSON revision. Uninstalling removes program files while retaining the collection.

The interface is written in C# and WPF. Microsoft Edge is needed only for a one-time transfer from an older browser-based installation. That transfer retains the original profile.

## Build

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File build.ps1
```

The build uses the installed .NET Framework compiler and included assets. It downloads no packages and treats warnings as errors. Outputs are `build\VocabularyDatabase.exe` and `..\outputs\Vocabulary-Database-Setup-1.2.exe`.

`VocabularyDatabase.csproj` targets .NET Framework 4.8 and can be opened in Visual Studio. Product name, version and compatibility identifiers are in `AppInfo.cs`.

## Verification

Run against a built installer with a new output directory:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tests\run-release-tests.ps1 -Installer ..\outputs\Vocabulary-Database-Setup-1.2.exe -OutputDirectory C:\Temp\VocabularyDatabase-1.2-tests
```

The suite checks card editing and storage, Unicode text, categories, dialogues, attachments, import/export, PNG export, study progress, window behavior, upgrades, versions and shortcuts. It uses isolated folders and synthetic data.

The app supports `--self-test <folder>`, `--ui-test <folder>` and `--screenshots <folder>`. Screenshot mode uses demonstration cards. The installer supports `--extract-test=<folder>`, `--upgrade-test=<folder>` and `--render-test=<folder>`.

Contact: systemfolder.dev@gmail.com
