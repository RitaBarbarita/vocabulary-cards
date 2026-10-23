using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Vocab
{
    public static class BugFixTests
    {
        static readonly List<string> checks = new List<string>();
        static void Check(bool ok, string message)
        {
            if (!ok) throw new Exception(message);
            checks.Add(message);
        }

        static T Find<T>(DependencyObject root, string name) where T : FrameworkElement
        {
            return UIRegressionTests.Find<T>(root).Single(e => e.Name == name);
        }

        static void Dialog<T>(Action open, Action<T> inspect) where T : Window
        {
            Exception failure = null;
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
            {
                T window = Application.Current.Windows.OfType<T>().Single();
                window.ShowInTaskbar = false;
                window.Left = window.Top = -32000;
                try { window.UpdateLayout(); inspect(window); }
                catch (Exception error) { failure = error; }
                finally { window.Close(); }
            }));
            open();
            UIRegressionTests.Pump();
            if (failure != null) throw failure;
        }

        static void MakeImage(string path)
        {
            var visual = new DrawingVisual();
            using (var d = visual.RenderOpen())
            {
                d.DrawRectangle(Brushes.Teal, null, new Rect(0, 0, 1600, 900));
                d.DrawRectangle(Brushes.Gold, null, new Rect(150, 150, 600, 600));
                d.DrawRectangle(Brushes.Crimson, null, new Rect(850, 150, 600, 600));
            }
            var bitmap = new RenderTargetBitmap(1600, 900, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            var png = new PngBitmapEncoder();
            png.Frames.Add(BitmapFrame.Create(bitmap));
            using (var file = File.Create(path)) png.Save(file);
        }

        public static int Run(string output)
        {
            Directory.CreateDirectory(output);
            MainWindow main = null;
            try
            {
                var store = new Store(Path.Combine(output, "data-" + Guid.NewGuid().ToString("N")));
                var card = new Card
                {
                    english = "for / since / ago",
                    russian = "for — в течение; since — с; ago — назад",
                    example = "I've known her since 2020. Я знаю её с 2020 года.\nIt costs 120. It was 3.5 yesterday.\r\n1. First numbered example.\r2. Second numbered example.",
                    exampleTr = "Перевод примера остаётся читаемым.",
                    explanation = "FOR → период: for five years\nSINCE → начальная точка: since 2021\nAGO → назад от настоящего: five years ago",
                    level = "A2", status = "known", register = "Neutral",
                    sections = new List<string> { "Grammar" }, topics = new List<string> { "Time" }
                };
                var examples = CardView.Examples(card);
                var texts = UIRegressionTests.Find<TextBlock>(examples).Select(t => t.Text).ToArray();
                Check(texts.Contains("I've known her since 2020. Я знаю её с 2020 года."), "A sentence ending with 2020 stays intact");
                Check(texts.Contains("It costs 120. It was 3.5 yesterday.") && !texts.Contains("2"), "Integers and decimals do not create spurious example rows");
                Check(examples.Children.OfType<DottedDivider>().Count() == 3 && texts.Contains("2. Second numbered example."), "CRLF, LF and CR separate examples and preserve numbering");

                string imageFile = Path.Combine(output, "attachment.png");
                MakeImage(imageFile);
                var attachment = new Attachment { id = "fixture", name = "Example image.png", type = "image/png", size = new FileInfo(imageFile).Length };
                File.Copy(imageFile, store.ImagePath(attachment.id));
                card.attachments.Add(attachment);
                var content = CardView.Content(store, card, true);
                string pngFile = Path.Combine(output, "Fixed-card.png");
                Retro.SavePng(content, pngFile, 600, 2);
                Check(UIRegressionTests.Find<TextBlock>(content).Where(t => t.Text == card.russian || t.Text == card.exampleTr).All(t => t.Foreground == Brushes.Black), "Card and example translations use black in PNG");
                var tags = Find<WrapPanel>(content, "CardTags");
                var tagPositions = tags.Children.Cast<FrameworkElement>().Select(t => t.TranslatePoint(new Point(), tags)).ToArray();
                Check(tagPositions.Length == 2 && Math.Abs(tagPositions[0].Y - tagPositions[1].Y) < 1 && tagPositions[1].X > tagPositions[0].X, "Section and topic tags share a horizontal row in the actual PNG layout");
                Check(UIRegressionTests.Find<Image>(content).Count(i => i.Source is BitmapSource && ((BitmapSource)i.Source).PixelWidth == 1600) == 1, "PNG layout includes full-resolution attachment pixels");
                var exported = new FormatConvertedBitmap(new BitmapImage(new Uri(Path.GetFullPath(pngFile))), PixelFormats.Bgra32, null, 0);
                byte[] pixels = new byte[exported.PixelWidth * exported.PixelHeight * 4];
                exported.CopyPixels(pixels, exported.PixelWidth * 4, 0);
                int teal = 0, gold = 0, crimson = 0;
                for (int i = 0; i < pixels.Length; i += 4)
                {
                    if (pixels[i] == 128 && pixels[i+1] == 128 && pixels[i+2] == 0) teal++;
                    if (pixels[i] == 0 && pixels[i+1] == 215 && pixels[i+2] == 255) gold++;
                    if (pixels[i] == 60 && pixels[i+1] == 20 && pixels[i+2] == 220) crimson++;
                }
                Check(teal > 10000 && gold > 10000 && crimson > 10000, "Saved PNG contains all three attachment color regions");
                card.attachments.Add(new Attachment { id = "missing", name = "Missing image.png" });
                File.WriteAllText(store.ImagePath("broken"), "not an image");
                card.attachments.Add(new Attachment { id = "broken", name = "Damaged image.png" });
                Retro.SavePng(CardView.Content(store, card, true), Path.Combine(output, "Unavailable-images.png"), 600, 2);
                Check(true, "Missing or damaged attachments do not prevent PNG export");
                card.attachments.RemoveRange(1, 2);

                for (int i = 0; i < 130; i++)
                    store.Data.cards.Add(new Card { english = "Word " + i.ToString("D3"), russian = "Translation", example = "An example sentence.", level = "B1" });
                store.Data.cards.Add(card);
                store.Save();
                main = new MainWindow(store);
                NativeTests.ShowOffscreen(main);
                UIRegressionTests.Pump();
                main.ShowMoreCards();
                var scroll = Find<ScrollViewer>(main, "CardScroll");
                scroll.ScrollToEnd();
                UIRegressionTests.Pump();
                double offset = scroll.VerticalOffset;
                Check(offset > 1000, "Regression starts deep in a list with 96 loaded cards");
                Card edited = store.Data.cards[80];
                Dialog<DetailWindow>(() => main.Detail(edited), w => { });
                Check(ReferenceEquals(scroll, Find<ScrollViewer>(main, "CardScroll")) && Math.Abs(scroll.VerticalOffset - offset) < 1, "Closing card details preserves the list and its position");
                Dialog<EditorWindow>(() => main.Edit(edited), w => { });
                Check(ReferenceEquals(scroll, Find<ScrollViewer>(main, "CardScroll")) && Math.Abs(scroll.VerticalOffset - offset) < 1, "Cancelling editing preserves the list and its position");
                Dialog<EditorWindow>(() => main.Edit(edited), w => { w.TextFields["russian"].Text = "Updated translation"; w.Save(); });
                scroll = Find<ScrollViewer>(main, "CardScroll");
                Check(Math.Abs(scroll.VerticalOffset - offset) < 1 && Find<CenteredCardsPanel>(main, "CardItems").Children.Count == 96, "Saving edits preserves the scroll offset and previously loaded cards");
                Check(new Store(store.Root).Data.cards[80].russian == "Updated translation", "Edits remain saved after reopening the database");
                Dialog<DetailWindow>(() => main.Detail(edited), w =>
                {
                    var editButton = UIRegressionTests.Find<Button>(w).Single(b => UIRegressionTests.Find<TextBlock>(b).Any(t => t.Text == "Edit"));
                    Dialog<EditorWindow>(() => editButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)), e => { e.TextFields["russian"].Text = "Edited from details"; e.Save(); });
                });
                scroll = Find<ScrollViewer>(main, "CardScroll");
                Check(Math.Abs(scroll.VerticalOffset - offset) < 1 && Find<CenteredCardsPanel>(main, "CardItems").Children.Count == 96, "Details to editor to Save returns to the same place");
                Dialog<DetailWindow>(() => main.Detail(card), w =>
                {
                    Check(UIRegressionTests.Find<TextBlock>(w).Single(t => t.Text == card.russian).Foreground == Brushes.Black, "Opened card translation is black");
                    var open = Find<Button>(w, "OpenAttachedImage");
                    Dialog<ImageViewerWindow>(() => open.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)), viewer =>
                    {
                        var image = Find<Image>(viewer, "FullSizeImage");
                        var bitmap = (BitmapSource)image.Source;
                        Check(bitmap.PixelWidth == 1600 && bitmap.PixelHeight == 900 && image.Width == 1600 && image.Height == 900, "One image click opens the original resolution at 100 percent");
                        viewer.SetFit(true); viewer.UpdateLayout();
                        Check(image.Width < 1600 && Math.Abs(image.Width / image.Height - 1600.0 / 900) < .001, "Fit to window retains the image proportions");
                        viewer.SetFit(false); viewer.UpdateLayout();
                        Check(image.Width == 1600 && image.Height == 900, "100 percent restores original dimensions");
                    });
                });
                main.Switch("table"); UIRegressionTests.Pump();
                edited = store.Data.cards.Single(c => c.id == edited.id);
                var table = UIRegressionTests.Find<DataGrid>(main).Single();
                table.Items.SortDescriptions.Add(new System.ComponentModel.SortDescription("english", System.ComponentModel.ListSortDirection.Descending));
                table.SelectedItem = edited;
                table.UpdateLayout();
                var tableScroll = UIRegressionTests.Find<ScrollViewer>(table).First();
                tableScroll.ScrollToVerticalOffset(60); tableScroll.ScrollToHorizontalOffset(200);
                UIRegressionTests.Pump();
                double tableOffset = tableScroll.VerticalOffset, horizontalOffset = tableScroll.HorizontalOffset;
                Dialog<EditorWindow>(() => main.Edit(edited), w => { w.TextFields["russian"].Text = "Table edit"; w.Save(); });
                table = UIRegressionTests.Find<DataGrid>(main).Single();
                tableScroll = UIRegressionTests.Find<ScrollViewer>(table).First();
                Check(Math.Abs(tableScroll.VerticalOffset - tableOffset) < 1 && Math.Abs(tableScroll.HorizontalOffset - horizontalOffset) < 1, "Table editing preserves both scroll directions");
                Check(table.SelectedItem is Card && ((Card)table.SelectedItem).id == edited.id && table.Items.SortDescriptions.Single().Direction == System.ComponentModel.ListSortDirection.Descending, "Table editing preserves selection and sorting");
                main.Close(); main = null;
                Check(AppInfo.Name == "Vocabulary Database" && AppInfo.Version == "1.3" && AppInfo.AssemblyVersion == "1.3.0.0", "Application name and version match the 1.3 release");
                Check(AppInfo.DataDirectory.EndsWith("English Vocab DB"), "Existing cards and images retain their storage location");
                var assembly = typeof(MainWindow).Assembly;
                Check(new[] { "Vocab.AI", "LocalApp", "AIPreferences", "Vocab.PrepareWindow" }.All(name => assembly.GetType(name) == null), "Release contains no AI engine, assistant or setup classes");
                File.WriteAllText(Path.Combine(output, "bug-fix-results.json"), Json.Write(new { passed = checks.Count, checks = checks }));
                return 0;
            }
            catch (Exception error)
            {
                File.WriteAllText(Path.Combine(output, "bug-fix-error.txt"), error.ToString());
                File.WriteAllText(Path.Combine(output, "bug-fix-results.json"), Json.Write(new { passed = checks.Count, checks = checks, error = error.Message }));
                return 1;
            }
            finally { if (main != null) main.Close(); }
        }
    }
}
