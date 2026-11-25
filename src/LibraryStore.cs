using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Media.Imaging;

namespace GameAlmanac
{
    public sealed class LibraryStore : IDisposable
    {
        internal const string DatabaseName = "Library.sqlite3";
        private static readonly string[] Sidecars = { "-journal", "-wal", "-shm" };
        private Sqlite db;
        private string directory;
        private string coverDirectory;
        private readonly Dictionary<string, BitmapSource> covers = new Dictionary<string, BitmapSource>();

        public LibraryStore(string directory)
        {
            this.directory = LibraryLocation.Normalize(directory);
            directory = this.directory;
            Directory.CreateDirectory(directory);
            coverDirectory = Path.Combine(directory, "Covers");
            Directory.CreateDirectory(coverDirectory);
            db = new Sqlite(Path.Combine(directory, DatabaseName));
            try { Initialize(); }
            catch { db.Dispose(); throw; }
        }

        public void Dispose() { db.Dispose(); }

        internal bool MoveTo(string destination, Action<string> saveLocation)
        {
            destination = LibraryLocation.Normalize(destination);
            if (string.Equals(directory, destination, StringComparison.OrdinalIgnoreCase)) return true;

            string coverPrefix = coverDirectory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (destination.StartsWith(coverPrefix, StringComparison.OrdinalIgnoreCase)
                || string.Equals(destination, coverDirectory, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("请选择现有封面文件夹以外的位置。");

            string targetDatabase = Path.Combine(destination, DatabaseName);
            string targetCovers = Path.Combine(destination, "Covers");
            Directory.CreateDirectory(destination);
            if (PathExists(targetDatabase) || Sidecars.Any(suffix => PathExists(targetDatabase + suffix))
                || File.Exists(targetCovers) || (Directory.Exists(targetCovers) && Directory.EnumerateFileSystemEntries(targetCovers).Any()))
                throw new InvalidOperationException("目标文件夹已有同名数据库或封面文件，请选择没有这些文件的位置。");

            string stagedDatabase = Path.Combine(destination, ".GameAlmanac-" + Guid.NewGuid().ToString("N") + ".tmp");
            string oldDatabase = Path.Combine(directory, DatabaseName);
            string oldCovers = coverDirectory;
            bool createdCovers = !Directory.Exists(targetCovers);
            var copiedCovers = new List<string>();
            var sourceFiles = new List<string>();
            bool databaseInstalled = false;
            Sqlite candidate = null;
            try
            {
                using (var staged = new Sqlite(stagedDatabase))
                {
                    db.BackupTo(staged);
                    staged.CheckIntegrity();
                }
                Directory.CreateDirectory(targetCovers);
                db.Read("SELECT DISTINCT cover_file FROM games", row =>
                {
                    string name = row.Text(0);
                    if (!string.IsNullOrEmpty(name) && name == Path.GetFileName(name) && name != "." && name != "..")
                        sourceFiles.Add(name);
                });
                foreach (string name in sourceFiles)
                {
                    var source = OpenExistingCover(Path.Combine(oldCovers, name));
                    if (source == null) continue;
                    using (source)
                    using (var target = new FileStream(Path.Combine(targetCovers, name), FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        copiedCovers.Add(name);
                        source.CopyTo(target);
                        target.Flush(true);
                    }
                }
                File.Move(stagedDatabase, targetDatabase);
                databaseInstalled = true;
                candidate = new Sqlite(targetDatabase);
                candidate.CheckIntegrity();
                saveLocation(destination);
            }
            catch
            {
                if (candidate != null) candidate.Dispose();
                if (databaseInstalled) DeleteDatabaseFiles(targetDatabase);
                foreach (string name in copiedCovers) TryDeleteFile(Path.Combine(targetCovers, name));
                if (createdCovers) RemoveEmptyDirectory(targetCovers);
                throw;
            }
            finally { DeleteDatabaseFiles(stagedDatabase); }

            // The settings write above is the point at which the destination becomes authoritative.
            db.Dispose();
            db = candidate;
            directory = destination;
            coverDirectory = targetCovers;
            if (!DeleteDatabaseFiles(oldDatabase)) return false;
            bool removed = true;
            foreach (string name in copiedCovers) removed &= TryDeleteFile(Path.Combine(oldCovers, name));
            RemoveEmptyDirectory(oldCovers);
            return removed;
        }

        private static bool PathExists(string path) { return File.Exists(path) || Directory.Exists(path); }

        private static FileStream OpenExistingCover(string path)
        {
            try { return File.OpenRead(path); }
            catch (FileNotFoundException) { return null; }
            catch (DirectoryNotFoundException) { return null; }
        }

        private static bool DeleteDatabaseFiles(string path)
        {
            if (!TryDeleteFile(path)) return false;
            bool removed = true;
            foreach (string suffix in Sidecars) removed &= TryDeleteFile(path + suffix);
            return removed;
        }

        private static bool TryDeleteFile(string path)
        {
            try { File.Delete(path); return true; }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }

        private static void RemoveEmptyDirectory(string path)
        {
            try { Directory.Delete(path, false); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        private void Initialize()
        {
            db.Transaction(() =>
            {
                int version = 0;
                db.Read("PRAGMA user_version", row => version = row.Int(0));
                if (version == 1) return true;
                if (version != 0) throw new InvalidOperationException("此游戏库由更高版本的应用创建。");
                db.Execute(@"CREATE TABLE IF NOT EXISTS templates (
                    id TEXT PRIMARY KEY, name TEXT NOT NULL UNIQUE COLLATE NOCASE,
                    w0 INTEGER NOT NULL, w1 INTEGER NOT NULL, w2 INTEGER NOT NULL, w3 INTEGER NOT NULL, w4 INTEGER NOT NULL, w5 INTEGER NOT NULL,
                    CHECK(min(w0,w1,w2,w3,w4,w5) >= 0 AND w0+w1+w2+w3+w4+w5=100))");
                db.Execute(@"CREATE TABLE IF NOT EXISTS game_types (
                    id TEXT PRIMARY KEY, name TEXT NOT NULL UNIQUE COLLATE NOCASE,
                    template_id TEXT NOT NULL REFERENCES templates(id))");
                db.Execute(@"CREATE TABLE IF NOT EXISTS games (
                    id TEXT PRIMARY KEY, name TEXT NOT NULL, cover_file TEXT NOT NULL,
                    type_id TEXT NOT NULL REFERENCES game_types(id), note TEXT NOT NULL)");
                db.Execute(@"CREATE TABLE IF NOT EXISTS plays (
                    game_id TEXT NOT NULL REFERENCES games(id), year INTEGER NOT NULL,
                    reuse_year INTEGER, detailed INTEGER NOT NULL CHECK(detailed IN (0,1)),
                    PRIMARY KEY(game_id,year), FOREIGN KEY(game_id,reuse_year) REFERENCES plays(game_id,year),
                    CHECK(reuse_year IS NULL OR reuse_year < year))");
                db.Execute(@"CREATE TABLE IF NOT EXISTS scores (
                    game_id TEXT NOT NULL, year INTEGER NOT NULL, metric INTEGER NOT NULL CHECK(metric BETWEEN 0 AND 5),
                    item INTEGER NOT NULL CHECK(item BETWEEN -1 AND 2), value REAL CHECK(value BETWEEN 0 AND 10),
                    PRIMARY KEY(game_id,year,metric,item), FOREIGN KEY(game_id,year) REFERENCES plays(game_id,year) ON DELETE CASCADE)");
                InsertTemplate("general", "通用均衡", Rating.DefaultWeights());
                InsertTemplate("story", "剧情体验", new[] { 20, 15, 15, 30, 10, 10 });
                InsertTemplate("competitive", "竞技对抗", new[] { 40, 15, 10, 0, 25, 10 });
                InsertTemplate("cozy", "轻松游玩", new[] { 25, 15, 20, 10, 10, 20 });
                InsertType("adventure", "冒险探索", "general");
                InsertType("narrative", "剧情向", "story");
                InsertType("versus", "竞技向", "competitive");
                InsertType("casual", "休闲向", "cozy");
                db.Execute("PRAGMA user_version = 1");
                return true;
            });
        }

        public Library Load()
        {
            var library = new Library();
            db.Read("SELECT id,name,w0,w1,w2,w3,w4,w5 FROM templates ORDER BY rowid", row =>
                library.Templates.Add(new WeightTemplate {
                    Id = row.Text(0), Name = row.Text(1),
                    Weights = Enumerable.Range(2, Rating.Metrics.Length).Select(row.Int).ToArray()
                }));
            db.Read("SELECT id,name,template_id FROM game_types ORDER BY rowid", row =>
                library.Types.Add(new GameType { Id = row.Text(0), Name = row.Text(1), TemplateId = row.Text(2) }));
            var games = new Dictionary<string, Game>();
            db.Read("SELECT id,name,cover_file,type_id,note FROM games ORDER BY rowid DESC", row =>
            {
                var game = new Game { Id = row.Text(0), Name = row.Text(1), Cover = row.Text(2), TypeId = row.Text(3), Note = row.Text(4) };
                games.Add(game.Id, game);
                library.Games.Add(game);
            });
            var plays = new Dictionary<Tuple<string, int>, Play>();
            db.Read("SELECT game_id,year,reuse_year,detailed FROM plays ORDER BY year DESC", row =>
            {
                string id = row.Text(0);
                var play = new Play(row.Int(1)) { ReuseYear = row.IsNull(2) ? (int?)null : row.Int(2), Detailed = row.Int(3) != 0 };
                plays.Add(Tuple.Create(id, play.Year), play);
                games[id].Plays.Add(play);
            });
            db.Read("SELECT game_id,year,metric,item,value FROM scores", row =>
            {
                var play = plays[Tuple.Create(row.Text(0), row.Int(1))];
                int metric = row.Int(2);
                int item = row.Int(3);
                double value = row.IsNull(4) ? double.NaN : row.Double(4);
                if (item == -1) play.Direct[metric] = value;
                else play.Details[metric][item] = value;
            });
            return library;
        }

        public string SaveGame(string id, string name, string type, string note, int? year, string coverSource)
        {
            name = RequiredName(name);
            if (id == null && (!year.HasValue || string.IsNullOrWhiteSpace(coverSource)))
                throw new InvalidOperationException("请选择游戏封面并填写游玩年份。");
            if (year.HasValue) CheckYear(year.Value);
            string newCover = coverSource == null ? null : CopyCover(coverSource);
            string oldCover = null;
            try
            {
                id = db.Transaction(() =>
                {
                    if (id == null)
                    {
                        string created = NewId();
                        db.Execute("INSERT INTO games(id,name,cover_file,type_id,note) VALUES (?,?,?,?,?)", created, name, newCover, type, note);
                        db.Execute("INSERT INTO plays(game_id,year,detailed) VALUES (?,?,0)", created, year.Value);
                        return created;
                    }
                    db.Read("SELECT cover_file FROM games WHERE id=?", row => oldCover = row.Text(0), id);
                    if (oldCover == null) throw new InvalidOperationException("找不到这款游戏。");
                    db.Execute("UPDATE games SET name=?,cover_file=?,type_id=?,note=? WHERE id=?", name, newCover ?? oldCover, type, note, id);
                    return id;
                });
            }
            catch
            {
                DiscardCover(newCover);
                throw;
            }
            if (newCover != null && oldCover != null) DiscardCover(oldCover);
            return id;
        }

        public void AddReusedPlay(string gameId, int year)
        {
            CheckYear(year);
            db.Transaction(() =>
            {
                CheckPlay(gameId, year, true);
                int? previous = null;
                db.Read("SELECT year FROM plays WHERE game_id=? AND year<? ORDER BY year DESC LIMIT 1", row => previous = row.Int(0), gameId, year);
                if (!previous.HasValue) throw new InvalidOperationException("这个年份之前没有可沿用的游玩记录。");
                db.Execute("INSERT INTO plays(game_id,year,reuse_year,detailed) VALUES (?,?,?,0)", gameId, year, previous.Value);
                return true;
            });
        }

        public void SaveScore(string gameId, Play score, bool createYear)
        {
            CheckYear(score.Year);
            db.Transaction(() =>
            {
                CheckPlay(gameId, score.Year, createYear);
                WriteScore(gameId, score, createYear);
                return true;
            });
        }

        private void WriteScore(string gameId, Play score, bool createYear)
        {
            if (createYear)
                db.Execute("INSERT INTO plays(game_id,year,detailed) VALUES (?,?,?)", gameId, score.Year, score.Detailed ? 1 : 0);
            else
                db.Execute("UPDATE plays SET reuse_year=NULL,detailed=? WHERE game_id=? AND year=?", score.Detailed ? 1 : 0, gameId, score.Year);
            db.Execute("DELETE FROM scores WHERE game_id=? AND year=?", gameId, score.Year);
            for (int metric = 0; metric < Rating.Metrics.Length; metric++)
            {
                SaveScoreValue(gameId, score.Year, metric, -1, score.Direct[metric]);
                for (int item = 0; item < score.Details[metric].Length; item++)
                    SaveScoreValue(gameId, score.Year, metric, item, score.Details[metric][item]);
            }
        }

        public void DeleteGame(string id)
        {
            string cover = db.Transaction(() =>
            {
                string filename = null;
                db.Read("SELECT cover_file FROM games WHERE id=?", row => filename = row.Text(0), id);
                if (filename == null) throw new InvalidOperationException("找不到这款游戏。");
                db.Execute("DELETE FROM plays WHERE game_id=?", id);
                db.Execute("DELETE FROM games WHERE id=?", id);
                return filename;
            });
            DiscardCover(cover);
        }

        public void DeletePlay(string gameId, int year)
        {
            db.Transaction(() =>
            {
                var game = Load().Game(gameId);
                var play = game == null ? null : game.FindPlay(year);
                if (play == null) throw new InvalidOperationException("找不到这一年的游玩记录。");
                if (game.Plays.Count == 1) throw new InvalidOperationException("这是唯一的游玩记录，可通过「删除游戏」移除。");
                if (play.ReuseYear.HasValue)
                {
                    db.Execute("UPDATE plays SET reuse_year=? WHERE game_id=? AND reuse_year=?", play.ReuseYear.Value, gameId, year);
                }
                else
                {
                    foreach (var dependent in game.Plays.Where(item => item.ReuseYear == year))
                        WriteScore(gameId, play.IndependentCopy(dependent.Year), false);
                }
                db.Execute("DELETE FROM plays WHERE game_id=? AND year=?", gameId, year);
                return true;
            });
        }

        private void CheckPlay(string gameId, int year, bool creating)
        {
            bool exists = false;
            db.Read("SELECT 1 FROM plays WHERE game_id=? AND year=?", row => exists = true, gameId, year);
            if (exists == creating)
                throw new InvalidOperationException(creating ? "这一年已有游玩记录。" : "找不到这一年的游玩记录。");
        }

        private void SaveScoreValue(string gameId, int year, int metric, int item, double? value)
        {
            if (!value.HasValue) return;
            if (!Rating.IsNotApplicable(value) && (value.Value < 0 || value.Value > 10
                || Math.Abs(value.Value * 10 - Math.Round(value.Value * 10)) > 1e-6))
                throw new InvalidOperationException("分数需为 0–10，最多一位小数。");
            db.Execute("INSERT INTO scores(game_id,year,metric,item,value) VALUES (?,?,?,?,?)",
                gameId, year, metric, item, Rating.IsNotApplicable(value) ? null : (object)value.Value);
        }

        public string AddTemplate(string name, int[] weights)
        {
            string id = NewId();
            InsertTemplate(id, RequiredName(name), weights);
            return id;
        }

        public void UpdateWeights(string id, int[] weights)
        {
            CheckWeights(weights);
            db.Execute("UPDATE templates SET w0=?,w1=?,w2=?,w3=?,w4=?,w5=? WHERE id=?",
                weights[0], weights[1], weights[2], weights[3], weights[4], weights[5], id);
        }

        private void InsertTemplate(string id, string name, int[] weights)
        {
            CheckWeights(weights);
            db.Execute("INSERT INTO templates(id,name,w0,w1,w2,w3,w4,w5) VALUES (?,?,?,?,?,?,?,?)",
                id, name, weights[0], weights[1], weights[2], weights[3], weights[4], weights[5]);
        }

        public void AddType(string name, string templateId) { InsertType(NewId(), RequiredName(name), templateId); }
        public void UpdateType(string id, string templateId) { db.Execute("UPDATE game_types SET template_id=? WHERE id=?", templateId, id); }
        private void InsertType(string id, string name, string templateId) { db.Execute("INSERT INTO game_types(id,name,template_id) VALUES (?,?,?)", id, name, templateId); }

        public void DeleteTemplate(string id, string replacement)
        {
            db.Transaction(() =>
            {
                if (Count("SELECT COUNT(*) FROM templates WHERE id=?", id) == 0) throw new InvalidOperationException("找不到此评分模板。");
                if (Count("SELECT COUNT(*) FROM templates") < 2) throw new InvalidOperationException("至少保留一个评分模板。");
                if (Count("SELECT COUNT(*) FROM game_types WHERE template_id=?", id) > 0)
                {
                    if (string.IsNullOrEmpty(replacement) || replacement == id || Count("SELECT COUNT(*) FROM templates WHERE id=?", replacement) == 0)
                        throw new InvalidOperationException("请为关联的游戏类型选择替代模板。");
                    db.Execute("UPDATE game_types SET template_id=? WHERE template_id=?", replacement, id);
                }
                db.Execute("DELETE FROM templates WHERE id=?", id);
                return true;
            });
        }

        public void DeleteType(string id, string replacement)
        {
            db.Transaction(() =>
            {
                if (Count("SELECT COUNT(*) FROM game_types WHERE id=?", id) == 0) throw new InvalidOperationException("找不到此游戏类型。");
                if (Count("SELECT COUNT(*) FROM game_types") < 2) throw new InvalidOperationException("至少保留一个游戏类型。");
                if (Count("SELECT COUNT(*) FROM games WHERE type_id=?", id) > 0)
                {
                    if (string.IsNullOrEmpty(replacement) || replacement == id || Count("SELECT COUNT(*) FROM game_types WHERE id=?", replacement) == 0)
                        throw new InvalidOperationException("请为关联的游戏选择替代类型。");
                    db.Execute("UPDATE games SET type_id=? WHERE type_id=?", replacement, id);
                }
                db.Execute("DELETE FROM game_types WHERE id=?", id);
                return true;
            });
        }

        private int Count(string query, params object[] arguments)
        {
            int count = 0;
            db.Read(query, row => count = row.Int(0), arguments);
            return count;
        }

        private static void CheckWeights(int[] weights)
        {
            if (weights == null || weights.Length != Rating.Metrics.Length || weights.Any(weight => weight < 0 || weight > 100) || weights.Sum() != 100)
                throw new InvalidOperationException("六项权重应为 0–100 的整数，合计 100%。");
        }

        private static void CheckYear(int year)
        {
            if (year < 1970 || year > DateTime.Today.Year)
                throw new InvalidOperationException("请输入 1970 至 " + DateTime.Today.Year.ToString(CultureInfo.InvariantCulture) + " 之间的年份。");
        }

        private static string RequiredName(string name)
        {
            name = (name ?? "").Trim();
            if (name.Length == 0) throw new InvalidOperationException("请输入名称。");
            return name;
        }

        private static string NewId() { return Guid.NewGuid().ToString("N"); }

        public static BitmapSource ReadImage(string path)
        {
            using (var stream = File.OpenRead(path))
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.DecodePixelWidth = 480;
                image.StreamSource = stream;
                image.EndInit();
                image.Freeze();
                return image;
            }
        }

        private string CopyCover(string source)
        {
            ReadImage(source);
            string extension = Path.GetExtension(source);
            string filename = NewId() + extension;
            try { File.Copy(source, Path.Combine(coverDirectory, filename)); }
            catch
            {
                DiscardCover(filename);
                throw;
            }
            return filename;
        }

        public BitmapSource LoadCover(string filename)
        {
            if (string.IsNullOrEmpty(filename) || filename != Path.GetFileName(filename)) return null;
            BitmapSource image;
            if (covers.TryGetValue(filename, out image)) return image;
            try { image = ReadImage(Path.Combine(coverDirectory, filename)); }
            catch (Exception error)
            {
                if (!(error is IOException || error is UnauthorizedAccessException || error is NotSupportedException || error is FileFormatException)) throw;
                return null;
            }
            covers[filename] = image;
            return image;
        }

        private void DiscardCover(string filename)
        {
            if (string.IsNullOrEmpty(filename) || filename != Path.GetFileName(filename)) return;
            covers.Remove(filename);
            try { File.Delete(Path.Combine(coverDirectory, filename)); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
