using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

[assembly: AssemblyTitle(Vocab.AppInfo.Name)]
[assembly: AssemblyProduct(Vocab.AppInfo.Name)]
[assembly: AssemblyDescription("Native Windows vocabulary application")]
[assembly: AssemblyVersion(Vocab.AppInfo.AssemblyVersion)]
[assembly: AssemblyFileVersion(Vocab.AppInfo.AssemblyVersion)]
namespace Vocab
{
    public static class Entry
    {
        public static string DataRoot;
        [STAThread]
        public static int Main(string[] args)
        {
            DataRoot = Path.Combine(AppInfo.DataDirectory, "NativeData");
            foreach (var a in args)
                if (a.StartsWith("--data-root="))
                    DataRoot = Path.GetFullPath(a.Substring(12));
            var app = new Application
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown
            };
            Retro.Init();
            if (args.Length > 0 && args[0].StartsWith("--migration-test="))
                return NativeTests.MigrationTest(args[0].Substring(17));
            if (args.Length > 0 && args[0] == "--self-test")
                return NativeTests.Run(args.Length > 1 ? args[1] : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test-output"));
            if (args.Length > 1 && args[0] == "--ui-test")
                return UIRegressionTests.Run(args[1]);
            if (args.Length > 1 && args[0] == "--fixes-test")
                return BugFixTests.Run(args[1]);
            if (args.Length > 1 && args[0] == "--screenshots")
                return Showcase.Run(args[1]);
            bool owner;
            using (var mutex = new Mutex(true, "Local\\EnglishVocabDB.Native." + BitConverter.ToString(System.Security.Cryptography.SHA256.Create().ComputeHash(System.Text.Encoding.UTF8.GetBytes(DataRoot))).Replace("-", "").Substring(0, 16), out owner))
            {
                if (!owner)
                {
                    Retro.Message(AppInfo.Name + " is already open.");
                    return 0;
                }

                try
                {
                    var store = new Store(DataRoot);
                    string profile = Path.Combine(AppInfo.DataDirectory, "BrowserData");
                    string html = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "english-vocab-db.html");
                    if (!File.Exists(store.FileName) && Directory.Exists(profile))
                    {
                        if (!File.Exists(html))
                            File.WriteAllText(html, "<!doctype html><meta charset=\"utf-8\"><title>Vocabulary data transfer</title>");
                        var transfer = new TransferWindow(profile, html, store);
                        transfer.ShowDialog();
                        if (!transfer.Success)
                        {
                            app.Shutdown();
                            return 1;
                        }
                    }

                    var main = new MainWindow(store);
                    app.MainWindow = main;
                    app.ShutdownMode = ShutdownMode.OnMainWindowClose;
                    app.Run(main);
                    return 0;
                }
                catch (Exception error)
                {
                    Retro.Message(error.Message);
                    return 1;
                }
                finally
                {
                    mutex.ReleaseMutex();
                }
            }
        }
    }

    public sealed class TransferWindow : RetroWindow
    {
        public bool Success;
        bool running;
        readonly string profile, html;
        readonly Store store;
        public TransferWindow(string profileFolder, string originalHtml, Store data) : base(AppInfo.Name + " Setup", 460, 170)
        {
            profile = profileFolder;
            html = originalHtml;
            store = data;
            var p = new StackPanel
            {
                Margin = new Thickness(18)
            };
            p.Children.Add(Retro.Text("Transferring cards…"));
            var progress = new SquareMeter
            {
                Margin = new Thickness(0, 15, 0, 0)
            };
            progress.Set(0, true);
            p.Children.Add(progress);
            Body = p;
            Loaded += (s, e) => Run();
            Closing += (s, e) =>
            {
                if (running)
                    e.Cancel = true;
            };
        }

        async void Run()
        {
            running = true;
            try
            {
                using (var stop = new CancellationTokenSource(TimeSpan.FromMinutes(10)))
                    await Migration.Import(profile, html, store, stop.Token);
                Success = true;
            }
            catch (Exception error)
            {
                Retro.Message("Data transfer could not finish.\n\n" + error.Message + "\n\nYour previous data was kept.");
            }
            finally
            {
                running = false;
                Close();
            }
        }
    }
}
