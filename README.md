# Vocabulary Database

A Windows vocabulary manager with a classic interface, detailed study cards and flashcard practice. Write your own entries, organise the collection and study offline. Current version: **1.2**.

![Vocabulary Database 1.2 — cards and filters](./screenshots/01-cards.png)

## Download and install

1. Download [Vocabulary Database 1.2 for Windows](https://github.com/RitaBarbarita/vocabulary-cards/raw/refs/heads/main/Vocabulary-Database-Setup-1.2.exe).
2. Close the application if it is running, then run the installer and click **Install**.
3. Open **Vocabulary Database** and add your first card.

Requires **Windows 10/11 x64** and **.NET Framework 4.8**. The interface is in English; words, translations and notes can be in any language. The installer is unsigned, so Windows SmartScreen may display a warning.

To update, install over the existing version. Cards, images, categories and learning progress are retained; uninstalling first is unnecessary.

## Features

- Cards with translations, explanations, examples, grammar notes, synonyms, antonyms and dialogues.
- Image attachments and individual card export as PNG, including long cards and A/B dialogues.
- Sections, topics, language levels, registers, learning status and favourites.
- Search, filters, a sortable table and flashcard practice.
- Excel `.xlsx` and `.xls`, CSV and pasted-table import with column matching and duplicate detection; Excel export.
- Centred card rows, incremental loading that keeps the scroll position and a **Back to top** button.
- Local storage and offline use, with no account or subscription required.

## Screenshots

<table>
<tr>
<td width="50%"><strong>Card details</strong><br><img src="screenshots/02-details.png" alt="Vocabulary Database 1.2 card details with examples, grammar and dialogues"></td>
<td width="50%"><strong>Card editor</strong><br><img src="screenshots/03-editor.png" alt="Vocabulary Database 1.2 editor for words, translations, examples and dialogues"></td>
</tr>
<tr>
<td width="50%"><strong>Vocabulary table</strong><br><img src="screenshots/04-table.png" alt="Vocabulary Database 1.2 sortable table"></td>
<td width="50%"><strong>Flashcard practice</strong><br><img src="screenshots/05-flashcards.png" alt="Vocabulary Database 1.2 flashcard practice"></td>
</tr>
</table>

The screenshots show the actual version 1.2 interface with demonstration cards and Spanish translations. These cards are not installed with the application.

## Creating cards

Click **Add Card**, enter a word or phrase and its translation, then add the explanations and examples you want to practise. You can include grammar notes, related words, A/B dialogues and images. Choose categories and a learning level, then click **Save**. Existing cards can be edited at any time.

Use **Import Cards** to bring in an Excel file, CSV file or pasted table. Match the columns to card fields and review the import before adding it to the collection. **Export to Excel** creates a spreadsheet of your cards.

## Data and updates

Cards and images are stored locally in `%LOCALAPPDATA%\English Vocab DB\NativeData`. The original directory name is retained for compatibility with existing installations. Successful saves keep the previous JSON revision. Uninstalling retains the collection.

The interface uses C# and WPF. Microsoft Edge is used only for a one-time transfer from an older browser-based installation; the original browser profile is retained. The installer and source archive contain no personal vocabulary or attachments.

## Version 1.2

Version 1.2 streamlines card entry and installation while retaining the collection, import/export tools and study screens. See the [changelog](./native/CHANGELOG.md).

## Source and build

Source code, icons and build instructions are in [`native`](./native). A [source archive for version 1.2](https://github.com/RitaBarbarita/vocabulary-cards/raw/refs/heads/main/Vocabulary-Database-Source-1.2.zip) is also available.

On Windows, run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File native\build.ps1
```

The build downloads no packages. It writes `native\build\VocabularyDatabase.exe` and `outputs\Vocabulary-Database-Setup-1.2.exe`. Compiler warnings are treated as build errors.

The packaged application passed **98 checks**: 25 application checks, 48 UI checks, 11 upgrade checks and 14 product/version/editor/shortcut checks. See the [verification report](./verification/1.2.json) and [test instructions](./native/README.md#verification). Checks use isolated folders and synthetic data.

## Contact

Questions, feedback or bug reports: [systemfolder.dev@gmail.com](mailto:systemfolder.dev@gmail.com).
