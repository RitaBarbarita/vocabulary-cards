using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Web.Script.Serialization;

internal static class BrandingTests
{
    const string ProductName = "Vocabulary Database";
    const BindingFlags Flags = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static readonly List<string> checks = new List<string>();
    static void Check(bool condition, string description)
    {
        if (!condition)
            throw new Exception(description);
        checks.Add(description);
        Console.WriteLine("PASS: " + description);
    }

    static IEnumerable<T> Find<T>(DependencyObject node)
        where T : DependencyObject
    {
        if (node is T)
            yield return (T)node;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
            foreach (T child in Find<T>(VisualTreeHelper.GetChild(node, i)))
                yield return child;
    }

    static void Show(Window window)
    {
        window.ShowInTaskbar = false;
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = window.Top = -32000;
        window.Show();
        window.UpdateLayout();
    }

    [STAThread]
    static int Main(string[] args)
    {
        string output = Path.GetFullPath(args[2]);
        Directory.CreateDirectory(output);
        try
        {
            var app = Assembly.LoadFrom(Path.GetFullPath(args[0]));
            app.GetType("LocalApp").GetField("Root", Flags).SetValue(null, Path.Combine(output, "AI"));
            var setup = Assembly.LoadFrom(Path.GetFullPath(args[1]));
            foreach (var assembly in new[]
            {
                app,
                setup
            })
            {
                var info = FileVersionInfo.GetVersionInfo(assembly.Location);
                Check(info.FileVersion == "1.1.0.0" && info.ProductVersion == "1.1.0.0", Path.GetFileName(assembly.Location) + " reports version 1.1.0.0");
                Check(info.ProductName == ProductName && info.FileDescription.StartsWith(ProductName), Path.GetFileName(assembly.Location) + " uses the new product name");
            }

            Check(Path.GetFileName(app.Location) == "VocabularyDatabase.exe", "The installed executable has the new product name");
            var application = new Application
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown
            };
            app.GetType("Vocab.Retro").GetMethod("Init").Invoke(null, null);
            var store = Activator.CreateInstance(app.GetType("Vocab.Store"), Path.Combine(output, "data"));
            var preferences = app.GetType("AIPreferences");
            var language = preferences.GetMethod("Read", Flags).Invoke(null, new object[] { output });
            var main = (Window)Activator.CreateInstance(app.GetType("Vocab.MainWindow"), store, language);
            Show(main);
            Check(main.Title == ProductName, "The main window displays Vocabulary Database");
            Check(Find<TextBlock>(main).Single(t => t.Name == "AppCredit").Text.StartsWith("Version 1.1  "), "The application footer displays version 1.1");
            main.Close();
            var prepare = (Window)Activator.CreateInstance(app.GetType("Vocab.PrepareWindow"));
            Show(prepare);
            Check(prepare.Title == ProductName + " Setup", "AI setup uses the new product name");
            prepare.Close();
            var installer = (Window)Activator.CreateInstance(setup.GetType("Vocab.Install+InstallWindow"), Flags, null, new object[] { Path.Combine(output, "install") }, null);
            Show(installer);
            Check(installer.Title == ProductName + " Setup" && Find<TextBlock>(installer).Any(t => t.Text == ProductName), "Installer title and heading use the new product name");
            installer.Close();
            var appInfo = app.GetType("Vocab.AppInfo");
            string dataDirectory = (string)appInfo.GetProperty("DataDirectory", Flags).GetValue(null, null);
            Check(dataDirectory == Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "English Vocab DB"), "Existing vocabulary and model paths remain compatible");
            string shortcuts = Path.Combine(output, "shortcuts");
            Directory.CreateDirectory(shortcuts);
            string program = Path.Combine(output, "install");
            string oldLink = Path.Combine(shortcuts, "English Vocab DB.lnk");
            dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
            try
            {
                dynamic previous = shell.CreateShortcut(oldLink);
                try
                {
                    previous.TargetPath = Path.Combine(program, "EnglishVocabDB.exe");
                    previous.Save();
                }
                finally
                {
                    Marshal.FinalReleaseComObject(previous);
                }

                var write = setup.GetType("Vocab.Install").GetMethod("WriteShortcuts", Flags);
                write.Invoke(null, new object[] { program, new[] { shortcuts } });
                string newLink = Path.Combine(shortcuts, ProductName + ".lnk");
                Check(File.Exists(newLink) && !File.Exists(oldLink), "Updating shortcuts replaces the previous product shortcut");
                dynamic current = shell.CreateShortcut(newLink);
                try
                {
                    Check((string)current.TargetPath == Path.Combine(program, "VocabularyDatabase.exe") && (string)current.Description == ProductName, "The new shortcut launches the renamed executable");
                }
                finally
                {
                    Marshal.FinalReleaseComObject(current);
                }

                dynamic unrelated = shell.CreateShortcut(oldLink);
                try
                {
                    unrelated.TargetPath = Path.Combine(output, "other-program", "EnglishVocabDB.exe");
                    unrelated.Save();
                }
                finally
                {
                    Marshal.FinalReleaseComObject(unrelated);
                }

                write.Invoke(null, new object[] { program, new[] { shortcuts } });
                Check(File.Exists(oldLink), "An unrelated shortcut is retained");
            }
            finally
            {
                Marshal.FinalReleaseComObject(shell);
            }

            File.WriteAllText(Path.Combine(output, "branding-results.json"), new JavaScriptSerializer().Serialize(new { passed = checks.Count, checks = checks }));
            application.Shutdown();
            return 0;
        }
        catch (Exception error)
        {
            File.WriteAllText(Path.Combine(output, "branding-error.txt"), error.ToString());
            Console.Error.WriteLine(error);
            return 1;
        }
    }
}
