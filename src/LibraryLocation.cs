using System;
using System.IO;
using System.Text;

namespace GameAlmanac
{
    internal sealed class LibraryLocation
    {
        private readonly string settingsFile;

        public string CurrentDirectory { get; private set; }

        public LibraryLocation(string defaultDirectory, string settingsFile)
        {
            this.settingsFile = settingsFile;
            CurrentDirectory = Normalize(defaultDirectory);
            string saved;
            try { saved = File.ReadAllText(settingsFile, Encoding.UTF8); }
            catch (FileNotFoundException) { return; }
            catch (DirectoryNotFoundException) { return; }

            CurrentDirectory = Normalize(saved);
            if (!File.Exists(Path.Combine(CurrentDirectory, LibraryStore.DatabaseName)))
                throw new IOException("已保存的数据位置暂不可用。请确认驱动器已连接，且数据库仍在该文件夹中。\n" + CurrentDirectory);
        }

        public static string Normalize(string value)
        {
            string path = Environment.ExpandEnvironmentVariables((value ?? "").Trim().Trim('"'));
            if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path))
                throw new ArgumentException("请选择文件夹，或输入完整的文件夹路径。");
            path = Path.GetFullPath(path);
            string root = Path.GetPathRoot(path);
            return path.Length == root.Length ? root : path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        public void SaveDirectory(string directory)
        {
            directory = Normalize(directory);
            Directory.CreateDirectory(Path.GetDirectoryName(settingsFile));
            string pending = settingsFile + "." + Guid.NewGuid().ToString("N") + ".tmp";
            bool published = false;
            try
            {
                byte[] contents = new UTF8Encoding(false).GetBytes(directory);
                using (var stream = new FileStream(pending, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    stream.Write(contents, 0, contents.Length);
                    stream.Flush(true);
                }
                if (File.Exists(settingsFile)) File.Replace(pending, settingsFile, null);
                else File.Move(pending, settingsFile);
                published = true;
            }
            finally
            {
                if (!published)
                {
                    try { File.Delete(pending); }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
            }
            CurrentDirectory = directory;
        }
    }
}
