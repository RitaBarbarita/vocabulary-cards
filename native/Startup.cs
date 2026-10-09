using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
[assembly:AssemblyTitle("English Vocab DB")]
[assembly:AssemblyDescription("Native Windows vocabulary application")]
[assembly:AssemblyVersion("1.0.0.0")]
[assembly:AssemblyFileVersion("1.0.0.0")]
namespace Vocab
{
 public static class Entry
 {
  public static string DataRoot;
  [STAThread] public static int Main(string[] args)
  {
   System.Net.ServicePointManager.SecurityProtocol=System.Net.SecurityProtocolType.Tls12;
   LocalApp.Root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"English Vocab DB","AI");
   DataRoot=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"English Vocab DB","NativeData");
   foreach(var a in args)if(a.StartsWith("--data-root="))DataRoot=Path.GetFullPath(a.Substring(12));
   var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};Retro.Init();
   if(args.Length>0 && args[0].StartsWith("--migration-test="))return NativeTests.MigrationTest(args[0].Substring(17));
   if(args.Length>0 && args[0].StartsWith("--ai-test="))return NativeTests.AITest(args[0].Substring(10));
   if(args.Length>0 && args[0].StartsWith("--cancel-test="))return NativeTests.CancelTest(args[0].Substring(14));
   if(args.Length>0 && args[0]=="--self-test")return NativeTests.Run(args.Length>1?args[1]:Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-output"));
   if(args.Length>1 && args[0]=="--ui-test")return UIRegressionTests.Run(args[1]);
   if(args.Length>1 && args[0]=="--screenshots")return Showcase.Run(args[1]);
   bool owner;using(var mutex=new Mutex(true,"Local\\EnglishVocabDB.Native."+BitConverter.ToString(System.Security.Cryptography.SHA256.Create().ComputeHash(System.Text.Encoding.UTF8.GetBytes(DataRoot))).Replace("-","").Substring(0,16),out owner)){
    if(!owner){Retro.Message("English Vocab DB is already open.");return 0;}
    try{var store=new Store(DataRoot);string profile=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"English Vocab DB","BrowserData");string html=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"english-vocab-db.html");
     if(!File.Exists(store.FileName) && Directory.Exists(profile)){
      if(!File.Exists(html))File.WriteAllText(html,"<!doctype html><meta charset=\"utf-8\"><title>Vocabulary data transfer</title>");
      var transfer=new TransferWindow(profile,html,store);transfer.ShowDialog();if(!transfer.Success){app.Shutdown();return 1;}
     }
     var main=new MainWindow(store,AIPreferences.Read(AppDomain.CurrentDomain.BaseDirectory));app.MainWindow=main;app.ShutdownMode=ShutdownMode.OnMainWindowClose;app.Run(main);return 0;
    }catch(Exception error){Retro.Message(error.Message);return 1;}finally{mutex.ReleaseMutex();}
   }
  }
 }
 public sealed class PrepareWindow:RetroWindow
 {
  readonly SquareMeter progress=new SquareMeter{Margin=new Thickness(0,14,0,14)};readonly TextBlock message=Retro.Text("Set up local AI");readonly Button start,later;CancellationTokenSource cancellation;bool running;
  public PrepareWindow():base("English Vocab DB Setup",500,245)
  {
   var p=new StackPanel{Margin=new Thickness(18)};p.Children.Add(Retro.Row(Retro.Icon("ai",32),Retro.Text("Local AI",true,14)));message.Margin=new Thickness(0,12,0,0);p.Children.Add(message);p.Children.Add(Retro.Text(LocalApp.RuntimeReady && LocalApp.ModelOnDisk?"Ready.":"Download: up to 8.1 GB · Free space: 15 GB",false,11));p.Children.Add(progress);start=Retro.Button("Set up AI",null,()=>Run());later=Retro.Button("Later",null,()=>{if(running)cancellation.Cancel();else Close();});var row=Retro.Row(start,later);row.HorizontalAlignment=HorizontalAlignment.Right;p.Children.Add(row);Body=p;Closing+=(s,e)=>{if(running){cancellation.Cancel();e.Cancel=true;}};
  }
  async void Run(){if(running)return;running=true;start.IsEnabled=false;((StackPanel)later.Content).Children.Clear();((StackPanel)later.Content).Children.Add(Retro.Text("Stop"));cancellation=new CancellationTokenSource();progress.Set(0,true);var report=new Progress<Tuple<string,int>>(v=>{message.Text=v.Item1;progress.Set(v.Item2,true);});try{await LocalApp.Prepare((s,i)=>((IProgress<Tuple<string,int>>)report).Report(Tuple.Create(s,i)),cancellation.Token);running=false;Close();}catch(Exception error){message.Text=cancellation.IsCancellationRequested?"Setup paused.":"Setup failed.";if(!cancellation.IsCancellationRequested)Retro.Message(error.Message);}finally{running=false;start.IsEnabled=true;progress.Set(progress.Value,false);((StackPanel)later.Content).Children.Clear();((StackPanel)later.Content).Children.Add(Retro.Text("Later"));}}
 }
 public sealed class TransferWindow:RetroWindow
 {
  public bool Success;bool running;readonly string profile,html;readonly Store store;
  public TransferWindow(string profileFolder,string originalHtml,Store data):base("English Vocab DB Setup",460,170){profile=profileFolder;html=originalHtml;store=data;var p=new StackPanel{Margin=new Thickness(18)};p.Children.Add(Retro.Text("Transferring cards…"));var progress=new SquareMeter{Margin=new Thickness(0,15,0,0)};progress.Set(0,true);p.Children.Add(progress);Body=p;Loaded+=(s,e)=>Run();Closing+=(s,e)=>{if(running)e.Cancel=true;};}
  async void Run(){running=true;try{using(var stop=new CancellationTokenSource(TimeSpan.FromMinutes(10)))await Migration.Import(profile,html,store,stop.Token);Success=true;}catch(Exception error){Retro.Message("Data transfer could not finish.\n\n"+error.Message+"\n\nYour previous data was kept.");}finally{running=false;Close();}}
 }
}
