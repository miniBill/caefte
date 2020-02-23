using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Caefte
{
    public class Server
    {
        readonly int port;

        public string Root { get; }

        public string Key { get; }

        public string Url => $"{Root}?key={Key}";

        bool running = true;
        readonly object runningLock = new object();

        public Server()
        {
            port = GetAvailablePort(1666);
            Root = $"http://localhost:{port}/";
            Key = GenerateRandomString();
        }

        private static string GenerateRandomString()
        {
            var tokenData = new byte[32];
            using (RandomNumberGenerator rng = new RNGCryptoServiceProvider())
                rng.GetBytes(tokenData);
            string result = Convert.ToBase64String(tokenData);
            return result;
        }

        public void Start()
        {
            new Thread(Loop) { IsBackground = true }.Start();

            async void Loop()
            {
                using (var listener = new HttpListener())
                {
                    listener.Prefixes.Add(Root);
                    listener.Start();
                    Console.WriteLine($"Listening on port {port}");

                    while (true)
                    {
                        lock (this)
                            if (!running)
                                return;

                        HttpListenerContext context = await listener.GetContextAsync();
#pragma warning disable CS4014
                        Task.Run(() => HandleRequest(context));
#pragma warning restore CS4014
                    }
                }
            }
        }

        public void Stop()
        {
            lock (runningLock)
                running = false;
        }

        public static Stream GetResource(string resource)
        {
            Assembly executingAssembly = Assembly.GetExecutingAssembly();
            return executingAssembly.GetManifestResourceStream($"{resource}.gz");
        }

        static int GetAvailablePort(int startingPort)
        {
            int port = startingPort;

#if !DEBUG
            foreach (int usedPort in GetUsedPorts())
            {
                if (usedPort < port)
                    continue;

                if (usedPort != port)
                    break;

                port++;
            }
#endif

            return port;
        }

        static List<int> GetUsedPorts()
        {
            IPGlobalProperties properties = IPGlobalProperties.GetIPGlobalProperties();
            var used = new List<int>();

            used.AddRange(properties.GetActiveTcpConnections().Select(n => n.LocalEndPoint.Port));
            used.AddRange(properties.GetActiveTcpListeners().Select(n => n.Port));
            used.Sort();
            return used;
        }

        async Task HandleRequest(HttpListenerContext context)
        {
            HttpListenerRequest request = context.Request;
            HttpListenerResponse response = context.Response;
            string absolutePath = request.Url.AbsolutePath;
            Console.WriteLine($"Asked {absolutePath}");
            try
            {
                if (!CheckKey(request))
                {
                    Console.WriteLine("Denied, missing key");
                    response.StatusCode = 403;
                    using (Stream output = response.OutputStream)
                    using (var writer = new StreamWriter(output, Encoding.UTF8))
                        writer.Write("Key missing");
                    return;
                }

                string extension = Path.GetExtension(absolutePath);
                if (extension == ".js")
                    response.AddHeader("Content-Type", "application/javascript");

                if (absolutePath == "/")
                {
                    absolutePath = "/index.html";
                    response.Cookies.Add(new Cookie("Key", Key));
                }

                response.AddHeader("Content-Encoding", "gzip");

                if (await new Controller().Handle(request, response))
                    return;

#if DEBUG
                await DEBUGServeFromFilesystem(response, absolutePath.Substring(1));
#else
                await EmbeddedResourceOr404(response, absolutePath);
#endif
            }
            catch (Exception e)
            {
                Console.WriteLine("Error: {0} ", e.Message);
                Console.WriteLine("StackTrace: {0}", e.StackTrace);
                while (e.InnerException != null)
                {
                    e = e.InnerException;
                    Console.WriteLine("Caused by: {0} ", e.Message);
                    Console.WriteLine("StackTrace: {0}", e.StackTrace);
                }

                response.StatusCode = 500;
                using (Stream output = response.OutputStream)
                using (Stream gzip = new GZipStream(output, CompressionMode.Compress))
                using (var writer = new StreamWriter(gzip, Encoding.UTF8))
                    writer.Write($"Error: {e.Message}");
            }
        }

        bool CheckKey(HttpListenerRequest request)
        {
#if DEBUG
            return true;
#else
            return request.Url.Query.Contains(Key) || request.Cookies["Key"].Value == Key;
#endif
        }

#if DEBUG
        static async Task DEBUGServeFromFilesystem(HttpListenerResponse response, string absolutePath)
        {
            string relativePath = Path.Combine(Assembly.GetExecutingAssembly().Location, "..", "..", "..", "dist");
            string basepath = Path.GetFullPath(relativePath);
            string path = Path.GetFullPath(Path.Combine(basepath, absolutePath));
            if (!path.StartsWith(basepath))
            {
                Console.WriteLine("Loackering attempt: trying to access {0}", absolutePath);
                response.StatusCode = 403;
                using (Stream output = new GZipStream(response.OutputStream, CompressionMode.Compress))
                using (var writer = new StreamWriter(output, Encoding.UTF8))
                    writer.Write("Nope");
                return;
            }

            using (Stream input = File.OpenRead(path))
            using (Stream output = new GZipStream(response.OutputStream, CompressionMode.Compress))
                await input.CopyToAsync(output);
        }
#else
        public static async Task EmbeddedResourceOr404(HttpListenerResponse response, string absolutePath)
        {
            using (Stream compressed = GetResource(absolutePath.Substring(1)))
                if (compressed != null)
                    using (Stream output = response.OutputStream)
                        await compressed.CopyToAsync(output);
                else
                {
                    response.StatusCode = 404;
                    using (Stream output = new GZipStream(response.OutputStream, CompressionMode.Compress))
                    using (var writer = new StreamWriter(output, Encoding.UTF8))
                        writer.Write($"{absolutePath} not found!");
                }
        }
#endif
    }
}