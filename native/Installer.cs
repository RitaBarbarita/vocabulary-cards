using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

[assembly: AssemblyTitle(Vocab.AppInfo.Name + " Setup")]
[assembly: AssemblyProduct(Vocab.AppInfo.Name)]
[assembly: AssemblyVersion(Vocab.AppInfo.AssemblyVersion)]
[assembly: AssemblyFileVersion(Vocab.AppInfo.AssemblyVersion)]
namespace Vocab
{
    public static class Install
    {
        const string Name = AppInfo.Name, IconName = "app-icon.ico";
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        static extern void SHChangeNotify(uint eventId, uint flags, string item, IntPtr unused);
        [STAThread]
        public static int Main(string[] args)
        {
            try
            {
                if (args.Length > 0 && args[0].StartsWith("--extract-test="))
                {
                    Extract(Path.GetFullPath(args[0].Substring(15)));
                    return 0;
                }

                if (args.Length > 0 && args[0].StartsWith("--upgrade-test="))
                    return UpgradeTest(Path.GetFullPath(args[0].Substring(15)));
                var app = new Application();
                Retro.Init();
                if (args.Length > 0 && args[0].StartsWith("--render-test="))
                {
                    string folder = Path.GetFullPath(args[0].Substring(14));
                    Directory.CreateDirectory(folder);
                    var w = new InstallWindow(folder)
                    {
                        ShowInTaskbar = false,
                        WindowStartupLocation = WindowStartupLocation.Manual,
                        Left = -32000,
                        Top = -32000
                    };
                    w.meter.Set(46, true);
                    w.Show();
                    w.UpdateLayout();
                    var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)w.ActualWidth, (int)w.ActualHeight, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                    bitmap.Render((System.Windows.Media.Visual)w.Content);
                    var png = new System.Windows.Media.Imaging.PngBitmapEncoder();
                    png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
                    using (var output = File.Create(Path.Combine(folder, "Native-installer.png")))
                        png.Save(output);
                    w.Close();
                    return 0;
                }

                app.Run(new InstallWindow());
                return 0;
            }
            catch (Exception error)
            {
                System.Windows.MessageBox.Show(error.Message, Name + " Setup");
                return 1;
            }
        }

        public static void Extract(string folder)
        {
            Directory.CreateDirectory(folder);
            foreach (var pair in new[]
            {
                new[]
                {
                    "App",
                    AppInfo.ExecutableName
                },
                new[]
                {
                    "Config",
                    AppInfo.ExecutableName + ".config"
                },
                new[]
                {
                    "Icon",
                    "app-icon.png"
                },
                new[]
                {
                    "IconIco",
                    IconName
                },
                new[]
                {
                    "Uninstall",
                    "uninstall.cmd"
                }
            })
            {
                using (var input = Assembly.GetExecutingAssembly().GetManifestResourceStream("Package." + pair[0]))
                {
                    if (input == null)
                        throw new InvalidOperationException("Missing installer resource.");
                    using (var output = File.Create(Path.Combine(folder, pair[1])))
                        input.CopyTo(output);
                }
            }
        }

        static bool PackageMatches(string file)
        {
            using (var input = Assembly.GetExecutingAssembly().GetManifestResourceStream("Package.App"))
            using (var bytes = new MemoryStream())
            {
                input.CopyTo(bytes);
                return File.ReadAllBytes(file).SequenceEqual(bytes.ToArray());
            }
        }

        static void Backup(string folder)
        {
            if (!Directory.Exists(folder))
                return;
            string backup = Path.Combine(folder, "Backups", DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 6));
            foreach (string name in new[]
            {
                AppInfo.ExecutableName,
                AppInfo.ExecutableName + ".config",
                AppInfo.LegacyExecutableName,
                AppInfo.LegacyExecutableName + ".config",
                "english-vocab-db.html",
                "app-icon.png",
                IconName,
                "app-icon-native-1.0.ico",
                "uninstall.cmd"
            })
            {
                string file = Path.Combine(folder, name);
                if (File.Exists(file))
                {
                    Directory.CreateDirectory(backup);
                    File.Copy(file, Path.Combine(backup, name));
                }
            }
        }

        static int UpgradeTest(string root)
        {
            Directory.CreateDirectory(root);
            string folder = Path.Combine(root, "upgrade-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            var checks = new System.Collections.Generic.List<string>();
            Action<bool, string> check = (ok, name) =>
            {
                if (!ok)
                    throw new IOException(name);
                checks.Add(name);
            };
            try
            {
                File.WriteAllText(Path.Combine(folder, AppInfo.ExecutableName), "version 1.2 app");
                File.WriteAllText(Path.Combine(folder, AppInfo.ExecutableName + ".config"), "version 1.2 config");
                File.WriteAllText(Path.Combine(folder, AppInfo.LegacyExecutableName), "previous app");
                File.WriteAllText(Path.Combine(folder, AppInfo.LegacyExecutableName + ".config"), "previous config");
                File.WriteAllText(Path.Combine(folder, "english-vocab-db.html"), "previous HTML");
                string data = Path.Combine(folder, "NativeData");
                Directory.CreateDirectory(data);
                File.WriteAllText(Path.Combine(data, "vocabulary.json"), "existing vocabulary");
                Directory.CreateDirectory(Path.Combine(data, "images"));
                File.WriteAllBytes(Path.Combine(data, "images", "existing-image"), new byte[] { 1, 2, 3, 4 });
                StopPrevious(folder);
                Backup(folder);
                Extract(folder);
                RemoveLegacyExecutable(folder);
                string backup = Directory.GetDirectories(Path.Combine(folder, "Backups")).Single();
                check(File.ReadAllText(Path.Combine(backup, AppInfo.ExecutableName)) == "version 1.2 app", "Version 1.2 application is backed up before replacement");
                check(File.ReadAllText(Path.Combine(backup, AppInfo.ExecutableName + ".config")) == "version 1.2 config", "Version 1.2 configuration is backed up before replacement");
                check(File.ReadAllBytes(Path.Combine(data, "images", "existing-image")).SequenceEqual(new byte[] { 1, 2, 3, 4 }), "Existing attachment bytes are retained on upgrade");
                check(File.ReadAllText(Path.Combine(backup, AppInfo.LegacyExecutableName)) == "previous app", "Previous app is backed up");
                check(File.ReadAllText(Path.Combine(backup, AppInfo.LegacyExecutableName + ".config")) == "previous config", "Previous configuration is backed up");
                check(File.ReadAllText(Path.Combine(backup, "english-vocab-db.html")) == "previous HTML", "Previous HTML is backed up");
                check(File.ReadAllText(Path.Combine(data, "vocabulary.json")) == "existing vocabulary", "Existing vocabulary is retained");
                check(File.ReadAllText(Path.Combine(folder, "english-vocab-db.html")) == "previous HTML", "Browser storage origin is retained");
                check(PackageMatches(Path.Combine(folder, AppInfo.ExecutableName)), "Renamed application matches the embedded package");
                check(File.Exists(Path.Combine(folder, AppInfo.ExecutableName + ".config")), "Renamed application configuration is installed");
                check(!File.Exists(Path.Combine(folder, AppInfo.LegacyExecutableName)), "Superseded executable is removed after backup");
                string programs = Path.Combine(root, "Programs");
                check(AppInfo.InstallDirectory(programs) == Path.Combine(programs, AppInfo.Name), "New installations use the new product folder");
                Directory.CreateDirectory(Path.Combine(programs, AppInfo.LegacyName));
                check(AppInfo.InstallDirectory(programs) == Path.Combine(programs, AppInfo.LegacyName), "Upgrades reuse the existing installation folder");
                check(AssemblyName.GetAssemblyName(Path.Combine(folder, AppInfo.ExecutableName)).Version.ToString() == AppInfo.AssemblyVersion, "Packaged application version is 1.3.0.0");
                File.WriteAllText(Path.Combine(root, "native-upgrade-results.json"), new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(new { passed = checks.Count, checks = checks }));
                return 0;
            }
            catch (Exception error)
            {
                File.WriteAllText(Path.Combine(root, "native-upgrade-error.txt"), error.ToString());
                return 1;
            }
        }

        static void RemoveLegacyExecutable(string folder)
        {
            foreach (string name in new[]
            {
                AppInfo.LegacyExecutableName,
                AppInfo.LegacyExecutableName + ".config"
            })
            {
                string file = Path.Combine(folder, name);
                if (File.Exists(file))
                    File.Delete(file);
            }
        }

        static void StopPrevious(string folder)
        {
            foreach (string name in new[]
            {
                "VocabularyDatabase",
                "EnglishVocabDB"
            })
                foreach (var p in Process.GetProcessesByName(name))
                    using (p)
                    {
                        try
                        {
                            if (String.Equals(Path.GetDirectoryName(p.MainModule.FileName), folder, StringComparison.OrdinalIgnoreCase) && !p.WaitForExit(5000))
                                throw new IOException("Close " + Name + " and try again.");
                        }
                        catch (System.ComponentModel.Win32Exception)
                        {
                        }
                        catch (InvalidOperationException)
                        {
                        }
                    }
        }

        static void WriteShortcuts(string folder, string[] directories)
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null)
                throw new InvalidOperationException("Windows shortcuts are unavailable.");
            dynamic shell = Activator.CreateInstance(shellType);
            try
            {
                foreach (string directory in directories)
                {
                    string path = Path.Combine(directory, Name + ".lnk");
                    dynamic shortcut = shell.CreateShortcut(path);
                    try
                    {
                        shortcut.TargetPath = Path.Combine(folder, AppInfo.ExecutableName);
                        shortcut.WorkingDirectory = folder;
                        shortcut.IconLocation = Path.Combine(folder, IconName) + ",0";
                        shortcut.Description = Name;
                        shortcut.Save();
                    }
                    finally
                    {
                        Marshal.FinalReleaseComObject(shortcut);
                    }

                    SHChangeNotify(0x2000, 0x1005, path, IntPtr.Zero);
                    string oldPath = Path.Combine(directory, AppInfo.LegacyName + ".lnk");
                    if (!File.Exists(oldPath))
                        continue;
                    dynamic oldShortcut = shell.CreateShortcut(oldPath);
                    string target;
                    try
                    {
                        target = oldShortcut.TargetPath;
                    }
                    finally
                    {
                        Marshal.FinalReleaseComObject(oldShortcut);
                    }

                    if (String.Equals(target, Path.Combine(folder, AppInfo.LegacyExecutableName), StringComparison.OrdinalIgnoreCase) || String.Equals(target, Path.Combine(folder, AppInfo.ExecutableName), StringComparison.OrdinalIgnoreCase))
                    {
                        File.Delete(oldPath);
                        SHChangeNotify(0x2000, 0x1005, oldPath, IntPtr.Zero);
                    }
                }
            }
            finally
            {
                Marshal.FinalReleaseComObject(shell);
            }
        }

        static void Shortcuts(string folder)
        {
            WriteShortcuts(folder, new[] { Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), Environment.GetFolderPath(Environment.SpecialFolder.Programs) });
            using (var key = Registry.CurrentUser.CreateSubKey(AppInfo.RegistryPath))
            {
                key.SetValue("DisplayName", Name);
                key.SetValue("DisplayVersion", AppInfo.Version);
                key.SetValue("Publisher", "Local application");
                key.SetValue("InstallLocation", folder);
                key.SetValue("DisplayIcon", Path.Combine(folder, IconName));
                key.SetValue("UninstallString", "\"" + Path.Combine(folder, "uninstall.cmd") + "\"");
                key.SetValue("NoModify", 1);
                key.SetValue("NoRepair", 1);
            }
        }

        sealed class InstallWindow : RetroWindow
        {
            readonly TextBlock status = Retro.Text("Close the app before installing.");
            internal readonly SquareMeter meter = new SquareMeter
            {
                Margin = new Thickness(0, 18, 0, 20)
            };
            readonly Button install, cancel;
            readonly string folder;
            bool running, finished;
            public InstallWindow() : this(AppInfo.InstallDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs")))
            {
            }

            public InstallWindow(string directory) : base(AppInfo.Name + " Setup", 520, 260)
            {
                folder = directory;
                var p = new StackPanel
                {
                    Margin = new Thickness(18)
                };
                p.Children.Add(Retro.Row(Retro.Icon("book", 32), Retro.Text(AppInfo.Name, true, 16)));
                p.Children.Add(Retro.Text("Version " + AppInfo.Version, false, 11));
                status.Margin = new Thickness(0, 14, 0, 0);
                p.Children.Add(status);
                p.Children.Add(meter);
                install = Retro.Button("Install", null, () => Run());
                cancel = Retro.Button("Cancel", null, () => Close());
                var row = Retro.Row(cancel, install);
                row.HorizontalAlignment = HorizontalAlignment.Right;
                p.Children.Add(row);
                Body = p;
                Closing += (s, e) =>
                {
                    if (running)
                        e.Cancel = true;
                };
            }

            async void Run()
            {
                if (finished)
                {
                    Process.Start(new ProcessStartInfo(Path.Combine(folder, AppInfo.ExecutableName)) { UseShellExecute = false });
                    Close();
                    return;
                }

                if (running)
                    return;
                running = true;
                install.IsEnabled = cancel.IsEnabled = false;
                meter.Set(0, true);
                var report = new Progress<Tuple<string, int>>(v =>
                {
                    status.Text = v.Item1;
                    meter.Set(v.Item2, true);
                });
                Action<string, int> tell = (s, i) => ((IProgress<Tuple<string, int>>)report).Report(Tuple.Create(s, i));
                try
                {
                    await Task.Run(() =>
                    {
                        tell("Preparing…", 0);
                        StopPrevious(folder);
                        Backup(folder);
                        tell("Installing…", 30);
                        Extract(folder);
                        RemoveLegacyExecutable(folder);
                        tell("Finishing…", 80);
                    });
                    Shortcuts(folder);
                    finished = true;
                    status.Text = "Ready.";
                    meter.Set(100, false);
                    SetText(install, "Open app");
                    SetText(cancel, "Close");
                }
                catch (Exception error)
                {
                    status.Text = "Installation failed.";
                    Retro.Message(error.Message);
                    meter.Set(0, false);
                }
                finally
                {
                    running = false;
                    install.IsEnabled = cancel.IsEnabled = true;
                }
            }

            static void SetText(Button button, string text)
            {
                var p = (StackPanel)button.Content;
                p.Children.Clear();
                p.Children.Add(Retro.Text(text));
            }
        }
    }
}
