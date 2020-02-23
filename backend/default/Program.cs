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
        readonly NotifyIcon notifyIcon;
        readonly SynchronizationContext synchronizationContext;
        readonly Server server = new Server();

        public MyApplicationContext()
        {
            Application.ApplicationExit += OnApplicationExit;
            synchronizationContext = new WindowsFormsSynchronizationContext();
            server.Start();
            notifyIcon = CreateNotifyicon();
#if !DEBUG
            OpenBrowser();
#endif
        }

        void OnApplicationExit(object sender, EventArgs e) => notifyIcon.Visible = false;

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
            server.Stop();
            Application.Exit();
        }

        static Icon GetIcon()
        {
            using (var iconStream = new MemoryStream())
            {
                using (Stream compressed = Server.GetResource("favicon.ico"))
                using (Stream stream = new GZipStream(compressed, CompressionMode.Decompress))
                    stream.CopyTo(iconStream);
                iconStream.Seek(0, SeekOrigin.Begin);
                return new Icon(iconStream);
            }
        }

        void NotifyIconClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
                OpenBrowser();
        }

        void OpenBrowser()
        {
            switch (Environment.OSVersion.Platform)
            {
                case PlatformID.Win32NT:
                    Process.Start(server.Url);
                    break;
                case PlatformID.MacOSX:
                    Process.Start("open", server.Url);
                    break;
                case PlatformID.Unix:
                    Process.Start("xdg-open", server.Url);
                    break;
                default:
                    Process.Start(server.Url);
                    break;
            }
        }
    }
}
