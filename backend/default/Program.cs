using System;
#if !DEBUG
using System.Collections.Generic;
#endif
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
#if !DEBUG
using System.Linq;
#endif
using System.Net;
#if !DEBUG
using System.Net.NetworkInformation;
#endif
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Caefte
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MyApplicationContext());
        }
    }

    sealed class MyApplicationContext : ApplicationContext
    {
        readonly int port = GetAvailablePort(1666);
        readonly string root;
        static readonly string Key;
        bool running = true;
        readonly NotifyIcon notifyIcon;
        readonly SynchronizationContext synchronizationContext;

        static MyApplicationContext()
        {
            var tokenData = new byte[32];
            using (RandomNumberGenerator rng = new RNGCryptoServiceProvider())
                rng.GetBytes(tokenData);
            Key = Convert.ToBase64String(tokenData);
        }

        public MyApplicationContext()
        {
            Application.ApplicationExit += OnApplicationExit;
            root = $"http://localhost:{port}/";
            synchronizationContext = new WindowsFormsSynchronizationContext();
            Listen();
            notifyIcon = CreateNotifyicon();
#if !DEBUG
            OpenBrowser();
#endif
        }

        void OnApplicationExit(object sender, EventArgs e) => notifyIcon.Visible = false;

        static int GetAvailablePort(int startingPort)
        {
#if DEBUG
            return startingPort;
#else
            IPGlobalProperties properties = IPGlobalProperties.GetIPGlobalProperties();
            var used = new List<int>();

            used.AddRange(properties.GetActiveTcpConnections().Select(n => n.LocalEndPoint.Port));
            used.AddRange(properties.GetActiveTcpListeners().Select(n => n.Port));
            used.Sort();

            foreach (int port in used)
            {
                if (port < startingPort)
                    continue;

                if (startingPort != port)
                    break;

                startingPort++;
            }

            return startingPort;
#endif
        }

        void Listen()
        {
            new Thread(Loop) { IsBackground = true }.Start();

            async void Loop()
            {
                using (var listener = new HttpListener())
                {
                    listener.Prefixes.Add(root);
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
#endif

        static async Task HandleRequest(HttpListenerContext context)
        {
            HttpListenerRequest request = context.Request;
            HttpListenerResponse response = context.Response;
            string absolutePath = request.Url.AbsolutePath;
            Console.WriteLine($"Asked {absolutePath}");
            try
            {
#if !DEBUG
                if (!request.Url.Query.Contains(Key) && request.Cookies["Key"].Value != Key)
                {
                    Console.WriteLine("Denied, missing key");
                    response.StatusCode = 403;
                    using (Stream output = response.OutputStream)
                    using (var writer = new StreamWriter(output, Encoding.UTF8))
                        writer.Write("Key missing");
                    return;
                }
#endif
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

#if !DEBUG
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

        public T Invoke<T>(Func<T> callback)
        {
            T result = default(T);
            synchronizationContext.Send(_ => result = callback(), null);
            return result;
        }

        public void Invoke(Action callback) => synchronizationContext.Send(_ => callback(), null);

        NotifyIcon CreateNotifyicon()
        {
            var contextMenu = new ContextMenu();

            var openMenuItem = new MenuItem
            {
                Index = 0,
                Text = "&Open"
            };
            contextMenu.MenuItems.Add(openMenuItem);

            var exitMenuItem = new MenuItem
            {
                Index = 1,
                Text = "E&xit"
            };
            contextMenu.MenuItems.Add(exitMenuItem);

            var icon = new NotifyIcon
            {
                Visible = true,
                Icon = GetIcon(),
                Text = "Caefte",
                ContextMenu = contextMenu
            };

            openMenuItem.Click += (_, __) => OpenBrowser();
            exitMenuItem.Click += (_, __) => Exit();
            icon.MouseClick += NotifyIconClick;

            return icon;
        }

        void Exit()
        {
            lock (this)
                running = false;
            Application.Exit();
        }

        static Icon GetIcon()
        {
            using (var iconStream = new MemoryStream())
            {
                using (Stream compressed = GetResource("favicon.ico"))
                using (Stream stream = new GZipStream(compressed, CompressionMode.Decompress))
                    stream.CopyTo(iconStream);
                iconStream.Seek(0, SeekOrigin.Begin);
                return new Icon(iconStream);
            }
        }

        static Stream GetResource(string resource)
        {
            Assembly executingAssembly = Assembly.GetExecutingAssembly();
            return executingAssembly.GetManifestResourceStream($"{resource}.gz");
        }

        void NotifyIconClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
                OpenBrowser();
        }

        void OpenBrowser()
        {
            string url = $"{root}?key={Key}";
            switch (Environment.OSVersion.Platform)
            {
                case PlatformID.Win32NT:
                    Process.Start(url);
                    break;
                case PlatformID.MacOSX:
                    Process.Start("open", url);
                    break;
                case PlatformID.Unix:
                    Process.Start("xdg-open", url);
                    break;
                default:
                    Process.Start(url);
                    break;
            }
        }
    }
}
