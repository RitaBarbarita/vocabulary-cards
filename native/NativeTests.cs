using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Vocab
{
    public static class NativeTests
    {
        static readonly List<string> checks = new List<string>();
        static void Check(bool ok, string name)
        {
            if (!ok)
                throw new Exception(name);
            checks.Add(name);
        }

        public static int Run(string root)
        {
            root = Path.GetFullPath(root);
            Directory.CreateDirectory(root);
            string run = Path.Combine(root, "run-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 6));
            Directory.CreateDirectory(run);
            try
            {
                var store = new Store(Path.Combine(run, "data"));
                var c = new Card
                {
                    english = "get along with",
                    russian = "ладить с кем-либо",
                    explanation = "Поддерживать хорошие отношения с человеком. Выражение описывает то, насколько легко люди общаются и понимают друг друга.",
                    example = "1. I get along with my neighbours.\n2. Do you get along with your new classmates?\n3. We have always got along well.",
                    synonyms = "be on good terms with; have a good relationship with",
                    antonyms = "clash with; be at odds with",
                    grammar = "get along with + person\nI get along well with my colleagues.",
                    level = "B1",
                    status = "learning",
                    favorite = true,
                    sections = new List<string>
                    {
                        "Phrasal Verbs"
                    },
                    topics = new List<string>
                    {
                        "Relationships"
                    },
                    dialogs = new List<Dialogue>
                    {
                        new Dialogue
                        {
                            lines = new List<Line>
                            {
                                new Line
                                {
                                    speaker = "A",
                                    text = "Do you get along with your new colleagues?"
                                },
                                new Line
                                {
                                    speaker = "B",
                                    text = "Yes, we get along really well. They are very friendly."
                                }
                            }
                        }
                    }
                };
                store.Data.cards.Add(c);
                store.Data.settings.customTopics.Add("My Topic");
                store.Save();
                var reload = new Store(store.Root);
                Check(reload.Data.cards[0].antonyms == c.antonyms && reload.Data.cards[0].favorite && reload.Data.cards[0].createdAt == c.createdAt, "Cards, antonyms, metadata and dialogues survive disk reload");
                Check(reload.Topics().Contains("My Topic"), "Custom categories survive disk reload");
                store.Save();
                Check(File.Exists(store.FileName + ".previous"), "Atomic saves keep a previous database revision");
                Check(store.Filter("odds", "B1", "learning", true, "Phrasal Verbs", "Relationships").Count == 1, "Combined search/category/status/favorite filters include antonyms");
                Check(store.Filter("", "A1", "", false, "", "").Count == 0, "Filters exclude nonmatching levels");
                var rows = Tables.Parse("English\tTranslation\tAntonyms / Opposite\n\"calm\"\t\"спокойный\"\t\"agitated; restless\"\ncalm\tduplicate\t");
                int skipped;
                Tables.Import(rows, Tables.Map(rows[0]), true, store, out skipped);
                Check(skipped == 1 && store.Data.cards.Count == 2, "Table import maps legacy headers and skips duplicates within the batch");
                Check(store.Data.cards[0].antonyms == "agitated; restless", "Import saves antonyms");
                var csv = Tables.Parse("Word or Phrase,Explanation\n\"hello, friend\",\"line one\nline two\"");
                Check(csv[1][0] == "hello, friend" && csv[1][1].Contains('\n'), "CSV parsing preserves quoted delimiters and multiline cells");
                string workbook = Path.Combine(run, "Vocabulary.xlsx");
                Tables.WriteXlsx(workbook, Tables.Export(store.Data.cards));
                var exported = Tables.ReadXlsx(workbook);
                Check(exported.Count == 3 && exported[0][3] == "Antonyms / Opposite" && exported[1][0] == "calm", "Excel export and import round-trip with adjacent antonyms");
                Check(exported[2][4] == c.example, "Excel round-trip preserves multiline examples");
                var editor = new EditorWindow(store, c);
                ShowOffscreen(editor);
                editor.TextFields["antonyms"].Text = "manual opposite";
                editor.TextFields["grammar"].Text = "Manual grammar";
                editor.Capture();
                Check(editor.Draft.antonyms == "manual opposite" && editor.Draft.id == c.id, "Native editor captures fields while preserving card identity");
                Check(editor.Draft.topics.SequenceEqual(c.topics) && editor.Draft.sections.SequenceEqual(c.sections) && editor.Draft.dialogs.Single().lines.Count == 2, "Editing retains categories and dialogues");
                editor.Save();
                Check(new Store(store.Root).Data.cards.First(x => x.id == c.id).antonyms == "manual opposite", "Native editor Save persists edits");
                var fresh = new EditorWindow(store, null);
                ShowOffscreen(fresh);
                Check(fresh.TextFields["antonyms"].Text == "" && fresh.Draft.dialogs.Count == 0, "New native cards do not inherit earlier antonyms or dialogues");
                Screenshot(fresh, Path.Combine(root, "Native-editor.png"));
                fresh.Close();
                var unicodeEditor = new EditorWindow(store, null);
                ShowOffscreen(unicodeEditor);
                unicodeEditor.TextFields["english"].Text = "你好";
                unicodeEditor.TextFields["russian"].Text = "привет";
                unicodeEditor.Save();
                Check(new Store(store.Root).Data.cards.Any(card => card.english == "你好" && card.russian == "привет"), "Manual card entry preserves Unicode study words and translations");
                int savedCount = store.Data.cards.Count;
                var imageFile = Path.Combine(run, "image.png");
                using (var input = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("Icons.book.png"))
                using (var output = File.Create(imageFile))
                    input.CopyTo(output);
                var imageEditor = new EditorWindow(store, null);
                ShowOffscreen(imageEditor);
                imageEditor.TextFields["english"].Text = "Image card";
                imageEditor.QueueImages(new[] { imageFile });
                string imageId = imageEditor.Draft.attachments.Single().id;
                Check(!File.Exists(store.ImagePath(imageId)), "New attachment files are deferred until Save");
                imageEditor.Save();
                Check(File.ReadAllBytes(store.ImagePath(imageId)).SequenceEqual(File.ReadAllBytes(imageFile)) && new Store(store.Root).Data.cards[0].attachments.Count == 1, "Attachment Save preserves image bytes and metadata");
                var imageCancelled = new EditorWindow(store, null);
                ShowOffscreen(imageCancelled);
                imageCancelled.QueueImages(new[] { imageFile });
                string cancelImage = imageCancelled.Draft.attachments.Single().id;
                imageCancelled.Close();
                Check(!File.Exists(store.ImagePath(cancelImage)) && store.Data.cards.Count == savedCount + 1, "Cancel discards a new card and its pending attachments");
                var rollback = new EditorWindow(store, store.Data.cards[0]);
                ShowOffscreen(rollback);
                rollback.TextFields["english"].Text = "Should not save";
                string savedWord = store.Data.cards[0].english;
                bool failed = false;
                using (var locked = File.Open(store.FileName, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    try
                    {
                        rollback.Save();
                    }
                    catch (IOException)
                    {
                        failed = true;
                    }
                }

                Check(failed && !rollback.Saved && store.Data.cards[0].english == savedWord && new Store(store.Root).Data.cards[0].english == savedWord, "A failed save preserves the previous database in memory and on disk");
                rollback.Close();
                var main = new MainWindow(store);
                ShowOffscreen(main);
                Screenshot(main, Path.Combine(root, "Native-interface.png"));
                main.Switch("table");
                Screenshot(main, Path.Combine(root, "Native-table.png"));
                main.Switch("flash");
                Screenshot(main, Path.Combine(root, "Native-flashcards.png"));
                main.Width = 680;
                main.Height = 500;
                Screenshot(main, Path.Combine(root, "Native-small-window.png"));
                main.Close();
                Check(true, "Native cards, table and flashcard screens render without a browser, including a small window");
                var study = new FlashPanel(store);
                study.Start(false);
                study.Flip();
                study.Answer("known");
                Check(new Store(store.Root).Data.cards[0].status == "known", "Flashcard answers persist learning status");
                Retro.SavePng(CardView.Content(store, c, true), Path.Combine(root, "Native-card.png"), 600, 2);
                var longCard = c.Copy();
                longCard.explanation = String.Join("\n", Enumerable.Repeat("Long text remains visible.", 350));
                string longPath = Path.Combine(run, "Long-card.png");
                Retro.SavePng(CardView.Content(store, longCard, true), longPath, 600, 2);
                var bitmap = new BitmapImage(new Uri(longPath));
                Check(bitmap.PixelHeight > 8000 && bitmap.PixelWidth == 1200, "Native PNG export preserves very long cards at the original 1200-pixel width");
                string invalid = Path.Combine(run, "invalid");
                Directory.CreateDirectory(invalid);
                string badFile = Path.Combine(invalid, "vocabulary.json");
                File.WriteAllText(badFile, "{broken}");
                bool kept = false;
                try
                {
                    new Store(invalid);
                }
                catch
                {
                    kept = true;
                }

                Check(kept && File.ReadAllText(badFile) == "{broken}", "An invalid database is reported without replacing its contents");
                string xls = Path.Combine(run, "excel-biff8.xls");
                using (var input = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("Fixtures.Excel"))
                using (var output = File.Create(xls))
                    input.CopyTo(output);
                var biff = Tables.ReadFile(xls);
                Check(biff.Count == 2 && biff[1][0] == "你好" && biff[1][1] == "привет" && biff[1][3] == "再见", "Legacy Excel .xls imports Unicode cells");
                Check(biff[1][2] == String.Concat(Enumerable.Repeat("長い説明 ", 2000)) && biff[1][4] == "12.5", "Legacy Excel handles SST continuations and numeric cells");
                File.WriteAllText(Path.Combine(root, "native-test-results.json"), Json.Write(new { passed = checks.Count, checks = checks }));
                File.WriteAllText(Path.Combine(root, "last-run.txt"), run);
                return 0;
            }
            catch (Exception error)
            {
                File.WriteAllText(Path.Combine(root, "native-test-error.txt"), error.ToString());
                return 1;
            }
        }

        public static void ShowOffscreen(Window w)
        {
            w.ShowInTaskbar = false;
            w.WindowStartupLocation = WindowStartupLocation.Manual;
            w.Left = w.Top = -32000;
            w.Show();
            w.UpdateLayout();
        }

        public static int MigrationTest(string configFile)
        {
            var config = Json.Read<Dictionary<string, string>>(File.ReadAllText(configFile));
            try
            {
                var store = new Store(config["data"]);
                System.Threading.Tasks.Task.Run(async () => await Migration.Import(config["profile"], config["html"], store, CancellationToken.None, true)).GetAwaiter().GetResult();
                var reload = new Store(store.Root);
                var card = reload.Data.cards.Single();
                Check(card.id == "migration-one" && card.english == "你好" && card.favorite && card.status == "known" && card.createdAt == 123, "Migration preserves Unicode cards and metadata");
                Check(card.antonyms == "再见" && card.dialogs.Single().lines.Count == 2, "Migration preserves antonyms and normalises legacy dialogues");
                Check(reload.Topics().Contains("My Topic") && reload.Sections().Contains("My Section"), "Migration preserves custom categories");
                Check(File.ReadAllBytes(store.ImagePath("test-image")).SequenceEqual(File.ReadAllBytes(config["expectedImage"])), "Migration preserves exact image bytes");
                File.WriteAllText(Path.Combine(store.Root, "migration-test-results.json"), Json.Write(new { passed = checks.Count, checks = checks }));
                return 0;
            }
            catch (Exception error)
            {
                File.WriteAllText(Path.Combine(config["data"], "migration-error.txt"), error.ToString());
                return 1;
            }
        }

        public static void Screenshot(Window w, string file)
        {
            w.UpdateLayout();
            var element = (FrameworkElement)w.Content;
            var bitmap = new RenderTargetBitmap((int)Math.Ceiling(w.ActualWidth), (int)Math.Ceiling(w.ActualHeight), 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(element);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var output = File.Create(file))
                encoder.Save(output);
        }
    }
}
