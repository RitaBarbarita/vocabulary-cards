using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using Microsoft.Win32;

internal static class LocalApp
{
    internal const string Model = "qwen3.5:9b";
    internal const string Engine = "http://127.0.0.1:11437";
    internal const string Bridge = "http://127.0.0.1:11438";
    internal const string RuntimeVersion = "v0.34.4";
    internal const string RuntimeHash = "535193f38f3344e5b08f5d1c171c31ce11aa17f0124ff69ae26d8ec7fe06fa62";
    internal static string Root;
    internal static readonly string AppFolder = AppDomain.CurrentDomain.BaseDirectory;
    internal static JavaScriptSerializer Json() { return new JavaScriptSerializer { MaxJsonLength = 1024 * 1024 }; }
    internal static HttpClient LocalClient(int seconds)
    {
        return new HttpClient(new HttpClientHandler { UseProxy = false }) { Timeout = TimeSpan.FromSeconds(seconds) };
    }
    internal static bool RuntimeReady { get { return File.Exists(Path.Combine(Root, "runtime", "ollama.exe")) && File.Exists(Path.Combine(Root, "runtime-version.txt")); } }
    internal static bool ModelOnDisk { get { return File.Exists(Path.Combine(Root,"models","manifests","registry.ollama.ai","library",Model.Split(':')[0],Model.Split(':')[1])); } }

    internal static async Task EnsureEngine()
    {
        using (var client = LocalClient(2))
        {
            try { var response = await client.GetAsync(Engine + "/api/version"); if (response.IsSuccessStatusCode) return; } catch { }
        }
        if (!RuntimeReady) throw new InvalidOperationException("Complete AI setup when starting the app.");
        string profile = Path.Combine(Root,"profile");
        Directory.CreateDirectory(profile);
        Directory.CreateDirectory(Path.Combine(Root,"models"));
        var info = new ProcessStartInfo(Path.Combine(Root,"runtime","ollama.exe"),"serve") {
            UseShellExecute=false, CreateNoWindow=true, WindowStyle=ProcessWindowStyle.Hidden, WorkingDirectory=Root
        };
        var engineEnvironment=ProcessHelper.CleanEnvironment();
        engineEnvironment["OLLAMA_HOST"] = "127.0.0.1:11437";
        engineEnvironment["OLLAMA_ORIGINS"] = Bridge;
        engineEnvironment["OLLAMA_MODELS"] = Path.Combine(Root,"models");
        engineEnvironment["OLLAMA_NO_CLOUD"] = "1";
        engineEnvironment["OLLAMA_NOPRUNE"] = "1";
        engineEnvironment["OLLAMA_CONTEXT_LENGTH"] = "8192";
        engineEnvironment["OLLAMA_NUM_PARALLEL"] = "1";
        engineEnvironment["USERPROFILE"] = profile;
        ProcessHelper.Start(info,engineEnvironment);
        using (var client = LocalClient(2))
        {
            for (int i=0;i<40;i++)
            {
                await Task.Delay(500);
                try { var response = await client.GetAsync(Engine + "/api/version"); if (response.IsSuccessStatusCode) return; } catch { }
            }
        }
        throw new InvalidOperationException("Could not start the AI engine. Complete setup and try again.");
    }

    internal static async Task Prepare(Action<string,int> report, CancellationToken cancel)
    {
        string seedFile = Path.Combine(AppFolder,"local-ai-seed.txt");
        string seed = File.Exists(seedFile) ? File.ReadAllText(seedFile).Trim() : "";
        if (!String.IsNullOrEmpty(seed) && Directory.Exists(seed) && !String.Equals(Path.GetFullPath(seed),Path.GetFullPath(Root),StringComparison.OrdinalIgnoreCase))
        {
            if (!RuntimeReady && Directory.Exists(Path.Combine(seed,"runtime")))
            {
                await Task.Run(() => CopyTree(Path.Combine(seed,"runtime"),Path.Combine(Root,"runtime"),report,cancel),cancel);
                if (File.Exists(Path.Combine(seed,"runtime-version.txt"))) File.Copy(Path.Combine(seed,"runtime-version.txt"),Path.Combine(Root,"runtime-version.txt"),true);
            }
            if (!ModelOnDisk && File.Exists(Path.Combine(seed,"models","manifests","registry.ollama.ai","library",Model.Split(':')[0],Model.Split(':')[1])))
                await Task.Run(() => CopyTree(Path.Combine(seed,"models"),Path.Combine(Root,"models"),report,cancel,true),cancel);
        }
        if (!RuntimeReady)
        {
            string archive = Path.Combine(Root,"runtime-download.zip");
            if (!File.Exists(archive) || !HashMatches(archive))
            {
                report("Downloading AI engine…",0);
                using (var client = new HttpClient {Timeout=TimeSpan.FromHours(2)})
                using (var response = await client.GetAsync("https://github.com/ollama/ollama/releases/download/" + RuntimeVersion + "/ollama-windows-amd64.zip",HttpCompletionOption.ResponseHeadersRead,cancel))
                {
                    response.EnsureSuccessStatusCode();
                    long total = response.Content.Headers.ContentLength ?? 0;
                    using (var input = await response.Content.ReadAsStreamAsync())
                    using (var output = new FileStream(archive,FileMode.Create,FileAccess.Write))
                    {
                        byte[] buffer = new byte[1024*1024]; long done=0; int count;
                        while ((count=await input.ReadAsync(buffer,0,buffer.Length,cancel))>0)
                        {
                            await output.WriteAsync(buffer,0,count,cancel); done+=count;
                            report("Downloading AI engine… " + (done/1024/1024) + " MB",total>0 ? (int)(done*100/total) : 0);
                        }
                    }
                }
            }
            report("Verifying AI engine…",0);
            await Task.Run(() => {
                if (!HashMatches(archive)) throw new InvalidDataException("Download verification failed. Please try setup again.");
                string runtime = Path.Combine(Root,"runtime"); Directory.CreateDirectory(runtime);
                using (var zip = ZipFile.OpenRead(archive))
                {
                    int done=0;
                    foreach (var item in zip.Entries)
                    {
                        cancel.ThrowIfCancellationRequested();
                        string destination = Path.GetFullPath(Path.Combine(runtime,item.FullName));
                        if (!destination.StartsWith(Path.GetFullPath(runtime)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Invalid archive path.");
                        if (String.IsNullOrEmpty(item.Name)) Directory.CreateDirectory(destination);
                        else { Directory.CreateDirectory(Path.GetDirectoryName(destination)); item.ExtractToFile(destination,true); }
                        report("Extracting AI engine…",++done*100/zip.Entries.Count);
                    }
                }
                File.WriteAllText(Path.Combine(Root,"runtime-version.txt"),RuntimeVersion);
            },cancel);
        }
        cancel.ThrowIfCancellationRequested();
        report("Starting AI…",0);
        await EnsureEngine();
        using (var client = LocalClient(7200))
        {
            bool installed=false;
            try {
                var tags=Json().Deserialize<Dictionary<string,object>>(await client.GetStringAsync(Engine+"/api/tags"));
                var models=tags["models"] as System.Collections.IEnumerable;
                if (models!=null) foreach(Dictionary<string,object> model in models) if ((string)model["name"]==Model) installed=true;
            } catch { }
            if (!installed)
            {
                report("Downloading language model…",0);
                var request = new HttpRequestMessage(HttpMethod.Post,Engine+"/api/pull") {Content=new StringContent("{\"model\":\""+Model+"\",\"stream\":true}",Encoding.UTF8,"application/json")};
                using (var response = await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,cancel))
                {
                    response.EnsureSuccessStatusCode();
                    using (var stream=await response.Content.ReadAsStreamAsync())
                    using (cancel.Register(() => stream.Close()))
                    using (var reader=new StreamReader(stream))
                    {
                        string line; bool success=false;
                        while ((line=await reader.ReadLineAsync())!=null)
                        {
                            cancel.ThrowIfCancellationRequested();
                            var progress=Json().Deserialize<Dictionary<string,object>>(line);
                            if (progress.ContainsKey("error")) throw new InvalidOperationException(Convert.ToString(progress["error"]));
                            if (progress.ContainsKey("status") && Convert.ToString(progress["status"])=="success") success=true;
                            long total=progress.ContainsKey("total") ? Convert.ToInt64(progress["total"]) : 0;
                            long done=progress.ContainsKey("completed") ? Convert.ToInt64(progress["completed"]) : 0;
                            report(total>0 ? "Downloading model… " + (done/1024/1024)+" / "+(total/1024/1024)+" MB" : "Verifying model…",total>0 ? (int)(done*100/total) : 0);
                        }
                        if (!success) throw new IOException("Model download interrupted. Click Set up AI to resume.");
                    }
                }
            }
        }
        cancel.ThrowIfCancellationRequested();
        report("Starting model…",0);
        using (var warmClient=LocalClient(180))
        {
            var warm=new {model=Model,stream=false,think=false,keep_alive="10m",messages=new[]{new {role="user",content="Reply only OK."}},options=new {num_ctx=8192,num_predict=4}};
            using(var response=await warmClient.PostAsync(Engine+"/api/chat",new StringContent(Json().Serialize(warm),Encoding.UTF8,"application/json"),cancel)) response.EnsureSuccessStatusCode();
        }
        report("Ready.",100);
    }
    private static bool HashMatches(string file)
    {
        using (var input=File.OpenRead(file)) using(var sha=SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(input)).Replace("-","").ToLowerInvariant()==RuntimeHash;
    }
    private static void CopyTree(string source,string destination,Action<string,int> report,CancellationToken cancel,bool modelOnly=false)
    {
        string[] files;
        if (modelOnly)
        {
            string manifestFile=Path.Combine(source,"manifests","registry.ollama.ai","library",Model.Split(':')[0],Model.Split(':')[1]);
            var manifest=Json().Deserialize<Dictionary<string,object>>(File.ReadAllText(manifestFile));
            var entries=new List<Dictionary<string,object>> { (Dictionary<string,object>)manifest["config"] };
            foreach(Dictionary<string,object> entry in (System.Collections.IEnumerable)manifest["layers"]) entries.Add(entry);
            var selected=new List<string>();
            foreach(var entry in entries)
            {
                string digest=Convert.ToString(entry["digest"]);
                if(!System.Text.RegularExpressions.Regex.IsMatch(digest,@"\Asha256:[0-9a-f]{64}\z"))throw new InvalidDataException("Invalid model file.");
                selected.Add(Path.Combine(source,"blobs",digest.Replace(':','-')));
            }
            selected.Add(manifestFile); // Publish the manifest only after all blobs have been copied.
            files=selected.Distinct().ToArray();
        }
        else files=Directory.GetFiles(source,"*",SearchOption.AllDirectories);
        long total=files.Sum(p=>new FileInfo(p).Length), done=0;
        foreach(string file in files)
        {
            cancel.ThrowIfCancellationRequested();
            string relative=file.Substring(source.TrimEnd(Path.DirectorySeparatorChar).Length+1);
            string target=Path.Combine(destination,relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            using(var input=File.OpenRead(file)) using(var output=new FileStream(target,FileMode.Create,FileAccess.Write))
            {
                var buffer=new byte[4*1024*1024]; int count;
                while((count=input.Read(buffer,0,buffer.Length))>0) { cancel.ThrowIfCancellationRequested(); output.Write(buffer,0,count); done+=count; report("Preparing AI files…",total>0 ? (int)(done*100/total) : 0); }
            }
        }
    }
}


