# English Vocab DB — Native Windows 2.0.1

C# / WPF vocabulary application with a classic Windows interface. The main application has no browser, WebView, JavaScript or Node.js runtime dependency.

## Install

Windows 10/11, x64, .NET Framework 4.8. Close the previous app, then run `English-Vocab-DB-Native-Setup-2.0.1.exe`. Choose the language used for translations and explanations. Interface labels stay in English.

The application uses the existing local Qwen3.5 9B model through its private Ollama endpoint on `127.0.0.1:11437`. An API key is not required. If AI files are missing, use **Set up AI**; this can download about 8.1 GB and needs about 15 GB free space. AI is optional; manual cards and study features work independently. Generated text is a draft to review and save explicitly.

## Existing data

On first launch, cards, custom categories and images are transferred from the previous application's browser profile. Microsoft Edge is used in the background for this one-time storage export; it does not display or run the native interface. Previous cards and images remain in their original profile. A failed transfer leaves that profile intact and does not create an empty replacement database.

Native data: `%LOCALAPPDATA%\English Vocab DB\NativeData\vocabulary.json` and `images`. Each successful database update keeps the previous JSON revision. Uninstalling removes program files and shortcuts; it retains the vocabulary and local AI files.

## Features

- Cards, sortable table, text/category/level/status/favorite filters.
- Card details, editing, images, synonyms and adjacent antonyms.
- Flashcards with selectable pools, shuffle and persisted learning status.
- Local AI filling, only-empty mode, Stop and Undo; manual changes made while generating are preserved.
- Translation/explanation language chosen during installation; study words can use any language.
- Excel `.xlsx` / `.xls`, CSV and pasted-table import, column mapping, duplicate skipping, template and Excel export.
- Classic card PNG export, including long text and colored A/B dialogue labels.

## Build

Run `powershell -NoProfile -ExecutionPolicy Bypass -File build.ps1` on Windows with .NET Framework 4.8. No packages are downloaded for the build. Assets and a BIFF8 test fixture are included. The app is written to `build\EnglishVocabDB.exe`; the installer is written to the sibling `outputs` folder.

`EnglishVocabDB.csproj` can also be opened with Visual Studio and the .NET Framework 4.8 development tools. Use `build.ps1` to package the installer.

## Verify

`build\EnglishVocabDB.exe --self-test <absolute-output-folder>` runs isolated data, import/export, AI draft/undo, attachment, save-failure and WPF rendering checks. It does not write to the user's installed database.

`build\EnglishVocabDB.exe --ui-test <absolute-output-folder>` verifies picker interaction, all metadata choices, white category panels, row centering, paging scroll position, Back to top, caption buttons, maximized monitor work-area bounds and PNG separators with synthetic cards.

Optional `--ai-test=<config.json>` and `--cancel-test=<config.json>` use a JSON object with `engine` (an isolated AI runtime/models folder) and `output`. `--migration-test=<config.json>` uses `profile`, `html`, `data` and `expectedImage`; it is for a synthetic test profile only. Its test-only sandbox flag is never used for production migration.

The installer supports `--extract-test=<folder>`, `--upgrade-test=<folder>` and `--render-test=<folder>` for isolated package, upgrade-preservation and installer-render checks.

The model can make linguistic mistakes; review generated cards. Full AI speed depends on available memory and GPU. This release is for Windows; it is not an iOS application.
