# Vocabulary Database 1.1

Windows vocabulary cards with examples, dialogues, images, flashcards, Excel import/export and optional local AI.

## Installation

Windows 10/11 x64 and .NET Framework 4.8 are required. Close the application and run `Vocabulary-Database-Setup-1.1.exe`. Choose the translation and explanation language, then open the app.

Installing over English Vocab DB retains cards, attachments, language preferences and AI files. The installer reuses an existing installation directory and replaces its standard shortcuts with Vocabulary Database shortcuts. New installations use `%LOCALAPPDATA%\Programs\Vocabulary Database`.

For optional local AI, select **Set up AI**. The initial download can use about 8.1 GB and needs about 15 GB of free space. No API key or separate Ollama installation is required. Setup creates its directories and supports retrying after cancellation or a failed download. Review generated cards before saving.

## Data

The data directory remains `%LOCALAPPDATA%\English Vocab DB` for upgrade compatibility:

- `NativeData\vocabulary.json` and `NativeData\images` contain cards and attachments.
- `AI` contains the local runtime and models.
- `BrowserData` contains the previous application's profile, if present.

The native interface uses WPF. Microsoft Edge is needed only for a one-time transfer from an older browser-based version. The old browser profile is retained. Database saves keep the previous JSON revision; uninstalling retains vocabulary and AI files.

## Build

Run from a Windows PowerShell terminal:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File build.ps1
```

The build uses the installed .NET Framework compiler and included assets. It downloads no packages and treats compiler warnings as errors. Outputs are `build\VocabularyDatabase.exe` and `..\outputs\Vocabulary-Database-Setup-1.1.exe`.

`VocabularyDatabase.csproj` targets .NET Framework 4.8 and can be opened in Visual Studio. Product name, version and compatibility identifiers are in `AppInfo.cs`.

## Verification

Run against a built installer, with a new output directory:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tests\run-release-tests.ps1 -Installer ..\outputs\Vocabulary-Database-Setup-1.1.exe -OutputDirectory C:\Temp\VocabularyDatabase-1.1-tests
```

The suite checks card storage, import/export, attachments, AI draft/undo behavior, the native interface, clean AI setup, legacy upgrades, names, versions and shortcuts. It uses isolated folders and synthetic data; AI download tests use small in-memory HTTP fixtures.

For AI setup checks alone, use `tests\run-ai-setup-tests.ps1 -Application <app-exe> -OutputDirectory <new-folder>`. The app also supports `--self-test <folder>`, `--ui-test <folder>` and `--screenshots <folder>`. Screenshot mode uses demonstration cards. The installer supports `--extract-test=<folder>`, `--upgrade-test=<folder>` and `--render-test=<folder>`.

Contact: systemfolder.dev@gmail.com
