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
[assembly:AssemblyTitle("English Vocab DB Setup")]
[assembly:AssemblyVersion("2.0.1.0")]
[assembly:AssemblyFileVersion("2.0.1.0")]
namespace Vocab
{
 public static class Install
 {
  const string Name="English Vocab DB",IconName="app-icon-native-2.0.1.ico";
  [DllImport("shell32.dll",CharSet=CharSet.Unicode)]static extern void SHChangeNotify(uint eventId,uint flags,string item,IntPtr unused);
  [STAThread]public static int Main(string[] args)
  {
   try{if(args.Length>0 && args[0].StartsWith("--extract-test=")){Extract(Path.GetFullPath(args[0].Substring(15)));return 0;}if(args.Length>0 && args[0].StartsWith("--upgrade-test="))return UpgradeTest(Path.GetFullPath(args[0].Substring(15)));var app=new Application();Retro.Init();if(args.Length>0 && args[0].StartsWith("--render-test=")){string folder=Path.GetFullPath(args[0].Substring(14));Directory.CreateDirectory(folder);var w=new InstallWindow(folder){ShowInTaskbar=false,WindowStartupLocation=WindowStartupLocation.Manual,Left=-32000,Top=-32000};w.meter.Set(46,true);w.Show();w.UpdateLayout();var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap((int)w.ActualWidth,(int)w.ActualHeight,96,96,System.Windows.Media.PixelFormats.Pbgra32);bitmap.Render((System.Windows.Media.Visual)w.Content);var png=new System.Windows.Media.Imaging.PngBitmapEncoder();png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));using(var output=File.Create(Path.Combine(folder,"Native-installer.png")))png.Save(output);w.Close();return 0;}app.Run(new InstallWindow());return 0;}catch(Exception error){System.Windows.MessageBox.Show(error.Message,Name+" Setup");return 1;}
  }
  public static void Extract(string folder){Directory.CreateDirectory(folder);foreach(var pair in new[]{new[]{"App","EnglishVocabDB.exe"},new[]{"Config","EnglishVocabDB.exe.config"},new[]{"Icon","app-icon.png"},new[]{"IconIco",IconName},new[]{"Uninstall","uninstall.cmd"}}){using(var input=Assembly.GetExecutingAssembly().GetManifestResourceStream("Package."+pair[0]))using(var output=File.Create(Path.Combine(folder,pair[1]))){if(input==null)throw new InvalidOperationException("Missing installer resource.");input.CopyTo(output);}}}
  static bool PackageMatches(string file){using(var input=Assembly.GetExecutingAssembly().GetManifestResourceStream("Package.App"))using(var bytes=new MemoryStream()){input.CopyTo(bytes);return File.ReadAllBytes(file).SequenceEqual(bytes.ToArray());}}
  static void Backup(string folder){if(!Directory.Exists(folder))return;string backup=Path.Combine(folder,"Backups",DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-native-"+Guid.NewGuid().ToString("N").Substring(0,6));foreach(string name in new[]{"EnglishVocabDB.exe","EnglishVocabDB.exe.config","english-vocab-db.html","app-icon.png","app-icon.ico",IconName,"ai-preferences.json","local-ai-seed.txt","uninstall.cmd"}){string file=Path.Combine(folder,name);if(File.Exists(file)){Directory.CreateDirectory(backup);File.Copy(file,Path.Combine(backup,name));}}}
  static int UpgradeTest(string root){Directory.CreateDirectory(root);string folder=Path.Combine(root,"upgrade-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);try{File.WriteAllText(Path.Combine(folder,"EnglishVocabDB.exe"),"previous app");File.WriteAllText(Path.Combine(folder,"english-vocab-db.html"),"previous HTML");File.WriteAllText(Path.Combine(folder,"local-ai-seed.txt"),"previous AI seed");string data=Path.Combine(folder,"NativeData");Directory.CreateDirectory(data);File.WriteAllText(Path.Combine(data,"vocabulary.json"),"existing vocabulary");var german=AIPreferences.Languages().First(l=>l.Code=="de");AIPreferences.Write(folder,german);StopPrevious(folder);Backup(folder);Extract(folder);AIPreferences.Write(folder,german);string backup=Directory.GetDirectories(Path.Combine(folder,"Backups")).Single();if(File.ReadAllText(Path.Combine(backup,"EnglishVocabDB.exe"))!="previous app" || File.ReadAllText(Path.Combine(backup,"english-vocab-db.html"))!="previous HTML" || File.ReadAllText(Path.Combine(data,"vocabulary.json"))!="existing vocabulary" || File.ReadAllText(Path.Combine(folder,"english-vocab-db.html"))!="previous HTML" || File.ReadAllText(Path.Combine(folder,"local-ai-seed.txt"))!="previous AI seed" || AIPreferences.Read(folder).Code!="de" || !PackageMatches(Path.Combine(folder,"EnglishVocabDB.exe")))throw new IOException("Upgrade test failed.");File.WriteAllText(Path.Combine(root,"native-upgrade-results.json"),"{\"passed\":7,\"checks\":[\"Old app backup\",\"Old HTML backup\",\"Existing native database preserved\",\"Original storage origin retained\",\"AI seed preserved\",\"Language preserved\",\"Native app extracted\"]}");return 0;}catch(Exception error){File.WriteAllText(Path.Combine(root,"native-upgrade-error.txt"),error.ToString());return 1;}}
  static void StopPrevious(string folder){try{using(var signal=System.Threading.EventWaitHandle.OpenExisting("Local\\EnglishVocabDB.LocalAI.Stop"))signal.Set();}catch(System.Threading.WaitHandleCannotBeOpenedException){}foreach(var p in Process.GetProcessesByName("EnglishVocabDB")){try{if(String.Equals(p.MainModule.FileName,Path.Combine(folder,"EnglishVocabDB.exe"),StringComparison.OrdinalIgnoreCase) && !p.WaitForExit(5000))throw new IOException("Close English Vocab DB and try again.");}catch(System.ComponentModel.Win32Exception){}}}
  static void Shortcuts(string folder)
  {
   var shellType=Type.GetTypeFromProgID("WScript.Shell");if(shellType==null)throw new InvalidOperationException("Windows shortcuts are unavailable.");dynamic shell=Activator.CreateInstance(shellType);foreach(var dest in new[]{Environment.SpecialFolder.DesktopDirectory,Environment.SpecialFolder.Programs}){string path=Path.Combine(Environment.GetFolderPath(dest),Name+".lnk");dynamic shortcut=shell.CreateShortcut(path);shortcut.TargetPath=Path.Combine(folder,"EnglishVocabDB.exe");shortcut.WorkingDirectory=folder;shortcut.IconLocation=Path.Combine(folder,IconName)+",0";shortcut.Description=Name;shortcut.Save();SHChangeNotify(0x2000,0x1005,path,IntPtr.Zero);}
   using(var key=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\EnglishVocabDB")){key.SetValue("DisplayName",Name);key.SetValue("DisplayVersion","2.0.1");key.SetValue("Publisher","Local application");key.SetValue("InstallLocation",folder);key.SetValue("DisplayIcon",Path.Combine(folder,IconName));key.SetValue("UninstallString","\""+Path.Combine(folder,"uninstall.cmd")+"\"");key.SetValue("NoModify",1);key.SetValue("NoRepair",1);}
  }
  sealed class InstallWindow:RetroWindow
  {
   readonly ComboBox languages=new ComboBox();readonly TextBlock status=Retro.Text("Close the app before installing.");internal readonly SquareMeter meter=new SquareMeter{Margin=new Thickness(0,18,0,20)};readonly Button install,cancel;readonly string folder;bool running,finished;
   public InstallWindow():this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs",Install.Name)){}
   public InstallWindow(string directory):base("English Vocab DB Setup",520,310)
   {
    folder=directory;var p=new StackPanel{Margin=new Thickness(18)};p.Children.Add(Retro.Row(Retro.Icon("book",32),Retro.Text("English Vocab DB",true,16)));p.Children.Add(new TextBlock{Text="Translation & explanation language",Margin=new Thickness(0,15,0,6)});var old=AIPreferences.Read(folder);foreach(var choice in AIPreferences.Languages()){languages.Items.Add(choice);if(choice.Code==old.Code)languages.SelectedItem=choice;}p.Children.Add(languages);status.Margin=new Thickness(0,14,0,0);p.Children.Add(status);p.Children.Add(meter);install=Retro.Button("Install",null,()=>Run());cancel=Retro.Button("Cancel",null,()=>Close());var row=Retro.Row(cancel,install);row.HorizontalAlignment=HorizontalAlignment.Right;p.Children.Add(row);Body=p;Closing+=(s,e)=>{if(running)e.Cancel=true;};
   }
   async void Run(){if(finished){Process.Start(new ProcessStartInfo(Path.Combine(folder,"EnglishVocabDB.exe")){UseShellExecute=false});Close();return;}if(running || languages.SelectedItem==null)return;running=true;install.IsEnabled=cancel.IsEnabled=languages.IsEnabled=false;var selected=(LanguageChoice)languages.SelectedItem;meter.Set(0,true);var report=new Progress<Tuple<string,int>>(v=>{status.Text=v.Item1;meter.Set(v.Item2,true);});Action<string,int> tell=(s,i)=>((IProgress<Tuple<string,int>>)report).Report(Tuple.Create(s,i));try{await Task.Run(()=>{tell("Preparing…",0);StopPrevious(folder);Backup(folder);tell("Installing…",30);Extract(folder);AIPreferences.Write(folder,selected);string seed=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"LocalAI");if(Directory.Exists(Path.Combine(seed,"runtime")) && Directory.Exists(Path.Combine(seed,"models")))File.WriteAllText(Path.Combine(folder,"local-ai-seed.txt"),seed);tell("Finishing…",80);});Shortcuts(folder);finished=true;status.Text="Ready.";meter.Set(100,false);SetText(install,"Open app");SetText(cancel,"Close");}catch(Exception error){status.Text="Installation failed.";Retro.Message(error.Message);languages.IsEnabled=true;meter.Set(0,false);}finally{running=false;install.IsEnabled=cancel.IsEnabled=true;}}
   static void SetText(Button button,string text){var p=(StackPanel)button.Content;p.Children.Clear();p.Children.Add(Retro.Text(text));}
  }
 }
}



