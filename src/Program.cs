using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Markup;

[assembly: AssemblyTitle("我的游戏年鉴")]
[assembly: AssemblyProduct("我的游戏年鉴")]
[assembly: AssemblyDescription("在 Windows 上整理个人游戏和历年评分。")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]
[assembly: AssemblyInformationalVersion("1.0")]

namespace GameAlmanac
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            try
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string data = Path.Combine(localAppData, "GameAlmanac");
                string settingsFile = Path.Combine(localAppData, "GameAlmanac.location");
                if (args.Length == 2 && args[0] == "--data-directory")
                {
                    data = LibraryLocation.Normalize(args[1]);
                    settingsFile = string.Equals(data, Path.GetPathRoot(data), StringComparison.OrdinalIgnoreCase)
                        ? Path.Combine(data, "GameAlmanac.location") : data + ".location";
                }
                using (var session = new Mutex(false, "Local\\GameAlmanac-1"))
                {
                    bool entered;
                    try { entered = session.WaitOne(0); }
                    catch (AbandonedMutexException) { entered = true; }
                    if (!entered)
                    {
                        MessageBox.Show("我的游戏年鉴已在运行。", "我的游戏年鉴", MessageBoxButton.OK, MessageBoxImage.Information);
                        return 0;
                    }
                    try
                    {
                        var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
                        LoadTheme(app);
                        var location = new LibraryLocation(data, settingsFile);
                        using (var store = new LibraryStore(location.CurrentDirectory))
                            return app.Run(new MainWindow(store, location));
                    }
                    finally { session.ReleaseMutex(); }
                }
            }
            catch (Exception error)
            {
                MessageBox.Show("无法打开游戏库。\n" + error.Message, "我的游戏年鉴", MessageBoxButton.OK, MessageBoxImage.Error);
                return 1;
            }
        }

        internal static void LoadTheme(Application app)
        {
            using (Stream xaml = typeof(Program).Assembly.GetManifestResourceStream("GameAlmanac.Theme.xaml"))
                app.Resources.MergedDictionaries.Add((ResourceDictionary)XamlReader.Load(xaml));
        }
    }
}
