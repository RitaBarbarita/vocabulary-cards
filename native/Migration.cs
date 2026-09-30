using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
namespace Vocab
{
 public static class Migration
 {
  // A one-time exporter for an existing browser profile. The native UI never hosts HTML.
  // All storage reads occur on the original file URL; no cards or images are changed.
  public static async Task<int> Import(string browserProfile,string originalHtml,Store store,CancellationToken cancel,bool isolatedTest=false)
  {
   if(File.Exists(store.FileName))return store.Data.cards.Count;
   Action<string> log=step=>File.AppendAllText(Path.Combine(store.Root,"migration.log"),DateTime.UtcNow.ToString("s")+" "+step+"\n");log("Starting transfer");
   string[] paths={Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),"Microsoft","Edge","Application","msedge.exe"),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Microsoft","Edge","Application","msedge.exe")};string edge=paths.FirstOrDefault(File.Exists);
   if(edge==null)throw new InvalidOperationException("Microsoft Edge is needed once to transfer the previous database.");if(!File.Exists(originalHtml))throw new FileNotFoundException("The previous application file was not found.");
   var probe=new TcpListener(IPAddress.Loopback,0);probe.Start();int port=((IPEndPoint)probe.LocalEndpoint).Port;probe.Stop();
   log("Starting exporter on loopback port "+port);
   var process=Process.Start(new ProcessStartInfo(edge,"--headless=new --remote-debugging-port="+port+" --remote-debugging-address=127.0.0.1 --user-data-dir=\""+browserProfile+"\" --no-first-run --disable-background-networking --disable-background-mode --disable-extensions --allow-file-access-from-files "+(isolatedTest?"--no-sandbox ":"")+"about:blank") {UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,RedirectStandardError=true});
   var stderr=process.StandardError.ReadToEndAsync();
   try{
    using(var http=new HttpClient(new HttpClientHandler{UseProxy=false}){Timeout=TimeSpan.FromSeconds(2)}){
     string json=null;for(int i=0;i<50;i++){cancel.ThrowIfCancellationRequested();try{json=await http.GetStringAsync("http://127.0.0.1:"+port+"/json/version");break;}catch{}if(process.HasExited)break;await Task.Delay(200,cancel);}if(json==null){if(process.HasExited)log("Exporter exit "+process.ExitCode+": "+await stderr);throw new IOException("Close the previous app, then retry the data transfer.");}
     log("Connecting to previous storage");var version=Json.Read<Dictionary<string,object>>(json);using(var cdp=new Cdp()){
      await cdp.Connect(Convert.ToString(version["webSocketDebuggerUrl"]),cancel);
      var target=await cdp.Call("Target.createTarget",new{url="about:blank"},null,cancel);string targetId=Convert.ToString(target["targetId"]);
      var attached=await cdp.Call("Target.attachToTarget",new{targetId=targetId,flatten=true},null,cancel);string session=Convert.ToString(attached["sessionId"]);
      // Loading is stopped before the old app can make an AI request; only our export expression runs.
      await cdp.Call("Page.enable",new{},session,cancel);await cdp.Call("Network.enable",new{},session,cancel);await cdp.Call("Network.setBlockedURLs",new{urls=new[]{"http://*","https://*"}},session,cancel);await cdp.Call("Emulation.setScriptExecutionDisabled",new{value=true},session,cancel);
      log("Opening original storage origin");await cdp.Call("Page.navigate",new{url=new Uri(originalHtml).AbsoluteUri},session,cancel);
      bool loaded=false;for(int i=0;i<100;i++){var ready=await cdp.Evaluate("document.readyState==='complete' && location.href==="+Json.Write(new Uri(originalHtml).AbsoluteUri),session,cancel);if(ready is bool && (bool)ready){loaded=true;break;}await Task.Delay(100,cancel);}if(!loaded)throw new IOException("The previous storage could not be opened.");
      await cdp.Call("Emulation.setScriptExecutionDisabled",new{value=false},session,cancel);
      log("Reading cards");var raw=(Dictionary<string,object>)await cdp.Evaluate("({cards:JSON.parse(localStorage.getItem('englishPhraseDatabase')||'[]'),settings:JSON.parse(localStorage.getItem('englishPhraseDatabaseSettings')||'{}')})",session,cancel);
      File.WriteAllText(Path.Combine(store.Root,"legacy-export.json"),Json.Write(raw));var cards=Json.Read<List<Card>>(Json.Write(raw["cards"]));var settings=Json.Read<UserSettings>(Json.Write(raw["settings"]));if(cards==null)throw new InvalidDataException("The previous database has an invalid format.");
      log("Reading image storage");await cdp.Evaluate("window.__vocabImageDb=await new Promise((resolve,reject)=>{const r=indexedDB.open('englishPhraseDatabaseAttachments');r.onsuccess=()=>resolve(r.result);r.onerror=()=>reject(r.error);});true",session,cancel,true);
      foreach(var card in cards){card.Normalize();foreach(var a in card.attachments){cancel.ThrowIfCancellationRequested();string key=Json.Write(a.id);var image=await cdp.Evaluate("await new Promise((resolve,reject)=>{if(!window.__vocabImageDb.objectStoreNames.contains('images')){resolve(null);return;}const r=window.__vocabImageDb.transaction('images').objectStore('images').get("+key+");r.onerror=()=>reject(r.error);r.onsuccess=()=>{if(!r.result||!r.result.blob){resolve(null);return;}const f=new FileReader();f.onload=()=>resolve(f.result.split(',')[1]);f.onerror=()=>reject(f.error);f.readAsDataURL(r.result.blob);};})",session,cancel,true);if(image==null)throw new InvalidDataException("Image \""+a.name+"\" could not be transferred. The previous database was kept.");var bytes=Convert.FromBase64String(Convert.ToString(image));File.WriteAllBytes(store.ImagePath(a.id),bytes);}}
      store.Data=new Database{cards=cards,settings=settings??new UserSettings()};store.Data.settings.customSections=store.Data.settings.customSections??new List<string>();store.Data.settings.customTopics=store.Data.settings.customTopics??new List<string>();store.Save();
      try{await cdp.Call("Browser.close",new{},null,cancel);}catch{}return cards.Count;
     }
    }
   }finally{try{if(!process.HasExited){process.Kill();process.WaitForExit(5000);}}catch{}process.Dispose();}
  }
  sealed class Cdp:IDisposable
  {
   readonly ClientWebSocket socket=new ClientWebSocket();int id;
   public async Task Connect(string url,CancellationToken cancel){await socket.ConnectAsync(new Uri(url),cancel);}
   public async Task<Dictionary<string,object>> Call(string method,object parameters,string session,CancellationToken cancel)
   {
    int own=++id;var request=new Dictionary<string,object>{{"id",own},{"method",method},{"params",parameters}};if(session!=null)request["sessionId"]=session;var bytes=Encoding.UTF8.GetBytes(Json.Write(request));await socket.SendAsync(new ArraySegment<byte>(bytes),WebSocketMessageType.Text,true,cancel);
    while(true){using(var output=new MemoryStream()){var buffer=new byte[65536];WebSocketReceiveResult r;do{r=await socket.ReceiveAsync(new ArraySegment<byte>(buffer),cancel);if(r.MessageType==WebSocketMessageType.Close)throw new IOException("Data transfer connection closed.");output.Write(buffer,0,r.Count);if(output.Length>256*1024*1024)throw new InvalidDataException("Data transfer response too large.");}while(!r.EndOfMessage);var message=Json.Read<Dictionary<string,object>>(Encoding.UTF8.GetString(output.ToArray()));if(message.ContainsKey("id") && Convert.ToInt32(message["id"])==own){if(message.ContainsKey("error"))throw new IOException(Json.Write(message["error"]));return (Dictionary<string,object>)message["result"];}}}
   }
   public async Task<object> Evaluate(string expression,string session,CancellationToken cancel,bool asynchronous=false){if(asynchronous)expression="(async()=>{"+(expression.StartsWith("window.")?expression.Replace(";true",";return true;"):"return "+expression)+"})()";var result=await Call("Runtime.evaluate",new{expression=expression,returnByValue=true,awaitPromise=true},session,cancel);if(result.ContainsKey("exceptionDetails"))throw new IOException(Json.Write(result["exceptionDetails"]));var remote=(Dictionary<string,object>)result["result"];return remote.ContainsKey("value")?remote["value"]:null;}
   public void Dispose(){socket.Dispose();}
  }
 }
}

