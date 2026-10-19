using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

internal static class AISetupTests
{
    const BindingFlags Flags = BindingFlags.Static | BindingFlags.NonPublic;
    static Type app;
    static string root;
    static readonly List<string> checks = new List<string>();
    static readonly Action<string, int> Quiet = (s, p) =>
    {
    };
    sealed class Download : HttpMessageHandler
    {
        internal byte[] Bytes;
        internal int Calls;
        internal HttpStatusCode Status = HttpStatusCode.OK;
        internal Download(byte[] bytes)
        {
            Bytes = bytes;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancel)
        {
            cancel.ThrowIfCancellationRequested();
            Calls++;
            return Task.FromResult(new HttpResponseMessage(Status) { Content = new ByteArrayContent(Bytes) });
        }
    }

    static void Check(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
        checks.Add(message);
        Console.WriteLine("PASS: " + message);
    }

    static void Select(string name)
    {
        root = Path.Combine(output, name, "Профиль с пробелами", "AI");
        app.GetField("Root", Flags).SetValue(null, root);
    }

    static bool Ready
    {
        get
        {
            return (bool)app.GetProperty("RuntimeReady", Flags).GetValue(null, null);
        }
    }

    static string Hash(byte[] bytes)
    {
        using (var sha = SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "");
    }

    static byte[] Archive(params string[] names)
    {
        using (var memory = new MemoryStream())
        {
            using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, true))
                foreach (string name in names)
                    using (var writer = new StreamWriter(zip.CreateEntry(name).Open()))
                        writer.Write("fixture " + name);
            return memory.ToArray();
        }
    }

    static Task Prepare(Download handler, string hash, CancellationToken cancel, Action<string, int> report = null)
    {
        return PrepareWithClient(handler, hash, cancel, report ?? Quiet);
    }

    static async Task PrepareWithClient(Download handler, string hash, CancellationToken cancel, Action<string, int> report)
    {
        using (var client = new HttpClient(handler, false))
            await ((Task)app.GetMethod("PrepareRuntime", Flags).Invoke(null, new object[] { report, cancel, client, "https://fixture.invalid/runtime.zip", hash }));
    }

    static async Task Fails<T>(Func<Task> action)
        where T : Exception
    {
        try
        {
            await action();
        }
        catch (T)
        {
            return;
        }

        throw new Exception("Expected " + typeof(T).Name);
    }

    static bool NoTemporaryFiles()
    {
        return !File.Exists(Path.Combine(root, "runtime-download.zip.partial")) && !Directory.GetDirectories(root, "runtime-install-*").Any() && !Directory.GetDirectories(root, "runtime-previous-*").Any();
    }

    static string output;
    static int Main(string[] args)
    {
        output = Path.GetFullPath(args[1]);
        Directory.CreateDirectory(output);
        try
        {
            app = Assembly.LoadFrom(Path.GetFullPath(args[0])).GetType("LocalApp", true);
            Run().GetAwaiter().GetResult();
            File.WriteAllText(Path.Combine(output, "ai-setup-results.json"), new JavaScriptSerializer().Serialize(new { passed = checks.Count, checks = checks }));
            return 0;
        }
        catch (Exception error)
        {
            File.WriteAllText(Path.Combine(output, "ai-setup-error.txt"), error.ToString());
            Console.Error.WriteLine(error);
            return 1;
        }
    }

    static async Task Run()
    {
        byte[] bytes = Archive("ollama.exe", "lib/ollama/fixture.dll");
        string hash = Hash(bytes);
        // Exercise the real entry point without a seed, cancelling before any network request.
        Select("entry");
        using (var stop = new CancellationTokenSource())
        {
            Action<string, int> report = (s, p) =>
            {
                if (s.StartsWith("Downloading AI engine"))
                    stop.Cancel();
            };
            await Fails<OperationCanceledException>(() => (Task)app.GetMethod("Prepare", Flags).Invoke(null, new object[] { report, stop.Token }));
        }

        Check(Directory.Exists(root) && !Ready, "Unseeded Prepare creates the absent AI directory before attempting a download");
        Select("fresh");
        Check(!Directory.Exists(root), "Fresh-install test starts without the profile or AI parent directory");
        var download = new Download(bytes);
        await Prepare(download, hash, CancellationToken.None);
        Check(Ready && download.Calls == 1 && File.ReadAllText(Path.Combine(root, "runtime", "ollama.exe")) == "fixture ollama.exe", "Fresh installation downloads and installs into a Unicode path with spaces");
        Check(File.Exists(Path.Combine(root, "runtime", "lib", "ollama", "fixture.dll")), "Nested engine libraries are extracted");
        Check(NoTemporaryFiles() && !File.Exists(Path.Combine(root, "runtime-download.zip")), "Successful setup removes the archive and temporary files");
        await Prepare(download, hash, CancellationToken.None);
        Check(download.Calls == 1, "An installed runtime is reused without another download");
        Select("cancel-download");
        using (var stop = new CancellationTokenSource())
        {
            download = new Download(bytes);
            await Fails<OperationCanceledException>(() => Prepare(download, hash, stop.Token, (s, p) =>
            {
                if (s.StartsWith("Downloading AI engine… "))
                    stop.Cancel();
            }));
        }

        Check(!Ready && NoTemporaryFiles(), "Cancelling a download removes its partial archive and does not mark AI ready");
        await Prepare(new Download(bytes), hash, CancellationToken.None);
        Check(Ready, "Setup succeeds when retried after cancelling a download");
        Select("cancel-extract");
        using (var stop = new CancellationTokenSource())
            await Fails<OperationCanceledException>(() => Prepare(new Download(bytes), hash, stop.Token, (s, p) =>
            {
                if (s.StartsWith("Extracting"))
                    stop.Cancel();
            }));
        Check(!Ready && NoTemporaryFiles() && !Directory.Exists(Path.Combine(root, "runtime")), "Cancelling extraction leaves no partially installed engine");
        download = new Download(bytes);
        await Prepare(download, hash, CancellationToken.None);
        Check(Ready && download.Calls == 0, "Retry after cancelled extraction reuses the verified archive");
        Select("corrupt");
        await Fails<InvalidDataException>(() => Prepare(new Download(Encoding.UTF8.GetBytes("truncated")), hash, CancellationToken.None));
        Check(!Ready && NoTemporaryFiles() && !File.Exists(Path.Combine(root, "runtime-download.zip")), "A corrupt download fails verification without publishing engine files");
        await Prepare(new Download(bytes), hash, CancellationToken.None);
        Check(Ready, "Setup succeeds after a corrupt download without manual cleanup");
        Select("http-error");
        await Fails<HttpRequestException>(() => Prepare(new Download(bytes) { Status = HttpStatusCode.ServiceUnavailable }, hash, CancellationToken.None));
        Check(!Ready && NoTemporaryFiles(), "A server error leaves setup retryable");
        await Prepare(new Download(bytes), hash, CancellationToken.None);
        Check(Ready, "Setup succeeds after a server error");
        Select("stale-cache");
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "runtime-download.zip"), "old partial download");
        File.WriteAllText(Path.Combine(root, "runtime-download.zip.partial"), "stale temporary download");
        Directory.CreateDirectory(Path.Combine(root, "runtime"));
        File.WriteAllText(Path.Combine(root, "runtime", "old.dll"), "incomplete extraction");
        Directory.CreateDirectory(Path.Combine(root, "models"));
        File.WriteAllText(Path.Combine(root, "models", "keep.txt"), "existing model");
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(root), "vocabulary.json"), "existing cards");
        await Prepare(new Download(bytes), hash, CancellationToken.None);
        Check(Ready && !File.Exists(Path.Combine(root, "runtime", "old.dll")) && NoTemporaryFiles(), "Old partial downloads and incomplete extraction recover automatically");
        Check(File.ReadAllText(Path.Combine(root, "models", "keep.txt")) == "existing model" && File.ReadAllText(Path.Combine(Path.GetDirectoryName(root), "vocabulary.json")) == "existing cards", "Repair retains existing models and vocabulary");
        Select("cached");
        Directory.CreateDirectory(root);
        File.WriteAllBytes(Path.Combine(root, "runtime-download.zip"), bytes);
        download = new Download(new byte[0])
        {
            Status = HttpStatusCode.ServiceUnavailable
        };
        await Prepare(download, hash, CancellationToken.None);
        Check(Ready && download.Calls == 0, "A previously verified archive installs without network access");
        Select("unsafe-archive");
        byte[] unsafeZip = Archive("../outside.txt", "ollama.exe");
        await Fails<InvalidDataException>(() => Prepare(new Download(unsafeZip), Hash(unsafeZip), CancellationToken.None));
        Check(!Ready && !File.Exists(Path.Combine(root, "outside.txt")) && NoTemporaryFiles(), "Archive traversal is rejected before installing the engine");
        Select("missing-executable");
        byte[] missing = Archive("lib/fixture.dll");
        await Fails<InvalidDataException>(() => Prepare(new Download(missing), Hash(missing), CancellationToken.None));
        Check(!Ready && NoTemporaryFiles(), "An archive without the engine executable cannot be marked ready");
        Select("cancelled-before-start");
        await Fails<OperationCanceledException>(() => Prepare(new Download(bytes), hash, new CancellationToken(true)));
        Check(!Directory.Exists(root), "A setup cancelled before starting makes no filesystem changes");
    }
}
