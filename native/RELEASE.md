# Native Windows release 1.0

The current application is C# / WPF, built by `native/build.ps1`. Do not package the historical Edge launcher or regenerate the UI from the original web sources.

Preserve the accepted Windows 95 graphic icons, pink app icon, gray/navy windows, white information panels, blue A/red B labels with black dialogue text, muted Add Card accent, adjacent synonyms/antonyms and measured PNG export. Labels are English; AI explanations follow the installation language.

Release artifact: `outputs/English-Vocab-DB-Native-Setup-1.0.exe`.

Verification:

- 48 UI regression checks: all metadata choices in creation/editing, white category panels, centered full/incomplete rows, retained paging position, floating Back to top, caption buttons and actual native window bounds above the taskbar, restored dark red New and dashed example separators.
- 38 isolated native app checks, also run against the app extracted from the final installer.
- Three live local-AI requests: French word / German explanation, Chinese word / English explanation, English phrase / simplified Chinese explanation.
- Three native cancellation checks.
- Four migration checks: Unicode/metadata, antonyms/legacy dialogues, custom categories and exact attachment bytes.
- Seven isolated upgrade/package checks: previous binaries and HTML backed up, vocabulary and original storage origin retained, AI seed and language preserved, correct executable extracted.
- WPF render inspection of cards, table, flashcards, editor, a small window, PNG export and matching installer.

Migration uses Edge only once to export an existing browser profile, with original app scripts disabled during navigation. The WPF UI does not reference WebView2, Windows Forms, HTML or JavaScript resources. The old browser profile is retained. Native JSON saves are atomic and keep the previous revision. Actual installation into the user's profile was not performed by the automated tests; the checks used isolated folders.

Model answers remain drafts and may contain language errors. Windows 10/11 x64 and .NET Framework 4.8 are required; GPU speed is not a requirement for manual cards.

1.0 corrects the custom window frame and title-button layout, adds per-monitor maximize bounds, fixes dropdown interaction and readable selected labels, centers rows, appends more cards without rebuilding the scroll view, and restores the accepted white tag panels, New color and example dividers. Existing native data, language preferences and AI files are unchanged.

The public version is 1.0, as requested. Application and installer file versions and Windows uninstall metadata are 1.0. The main window includes a discreet version/contact footer. Five styled repository screenshots show synthetic cards; personal vocabulary, images and AI model files are excluded.
