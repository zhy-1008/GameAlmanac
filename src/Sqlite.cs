using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace GameAlmanac
{
    internal sealed class Sqlite : IDisposable
    {
        private const string Dll = "winsqlite3.dll";
        private const int Done = 101;
        private const int Row = 100;
        private static readonly IntPtr Transient = new IntPtr(-1);
        private IntPtr database;

        public Sqlite(string path)
        {
            int result = sqlite3_open16(path, out database);
            if (result != 0)
            {
                var error = Error(result);
                Dispose();
                throw error;
            }
            try
            {
                Check(sqlite3_busy_timeout(database, 3000));
                Execute("PRAGMA foreign_keys = ON");
            }
            catch { Dispose(); throw; }
        }

        public void Execute(string sql, params object[] args)
        {
            Read(sql, null, args);
        }

        public void Read(string sql, Action<Statement> row, params object[] args)
        {
            IntPtr pointer;
            Check(sqlite3_prepare16_v2(database, sql, -1, out pointer, IntPtr.Zero));
            try
            {
                for (int index = 0; index < args.Length; index++) Bind(pointer, index + 1, args[index]);
                var statement = new Statement(pointer);
                int result;
                while ((result = sqlite3_step(pointer)) == Row)
                    if (row != null) row(statement);
                if (result != Done) throw Error(result);
            }
            finally { sqlite3_finalize(pointer); }
        }

        public T Transaction<T>(Func<T> body)
        {
            Execute("BEGIN IMMEDIATE");
            try
            {
                var value = body();
                Execute("COMMIT");
                return value;
            }
            catch
            {
                try { Execute("ROLLBACK"); }
                catch (InvalidOperationException) { }
                throw;
            }
        }

        public void BackupTo(Sqlite destination)
        {
            IntPtr backup = sqlite3_backup_init(destination.database, "main", database, "main");
            if (backup == IntPtr.Zero) throw destination.Error(1);
            int stepResult;
            int finishResult;
            try { stepResult = sqlite3_backup_step(backup, -1); }
            finally { finishResult = sqlite3_backup_finish(backup); }
            if (stepResult != Done) throw destination.Error(stepResult);
            destination.Check(finishResult);
        }

        public void CheckIntegrity()
        {
            var results = new List<string>();
            Read("PRAGMA quick_check", row => results.Add(row.Text(0)));
            if (results.Count != 1 || results[0] != "ok")
                throw new InvalidOperationException("新位置的数据库完整性检查未通过。");
        }

        private void Bind(IntPtr stmt, int index, object value)
        {
            if (value == null) Check(sqlite3_bind_null(stmt, index));
            else if (value is string) Check(sqlite3_bind_text16(stmt, index, (string)value, -1, Transient));
            else if (value is int) Check(sqlite3_bind_int(stmt, index, (int)value));
            else if (value is double) Check(sqlite3_bind_double(stmt, index, (double)value));
            else throw new ArgumentException("Unsupported SQLite parameter.", "value");
        }

        private InvalidOperationException Error(int code)
        {
            string message = database == IntPtr.Zero ? "" : Marshal.PtrToStringUni(sqlite3_errmsg16(database));
            return new InvalidOperationException("数据库操作失败（" + code + "）。" + message);
        }

        private void Check(int result) { if (result != 0) throw Error(result); }

        public void Dispose()
        {
            if (database == IntPtr.Zero) return;
            sqlite3_close(database);
            database = IntPtr.Zero;
        }

        internal sealed class Statement
        {
            private readonly IntPtr pointer;
            public Statement(IntPtr pointer) { this.pointer = pointer; }
            public bool IsNull(int column) { return sqlite3_column_type(pointer, column) == 5; }
            public int Int(int column) { return sqlite3_column_int(pointer, column); }
            public double Double(int column) { return sqlite3_column_double(pointer, column); }
            public string Text(int column) { return Marshal.PtrToStringUni(sqlite3_column_text16(pointer, column)); }
        }

        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport(Dll, CharSet = CharSet.Unicode, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_open16(string filename, out IntPtr database);
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport(Dll, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_close(IntPtr database);
        [DllImport(Dll, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_busy_timeout(IntPtr database, int milliseconds);
        [DllImport(Dll, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr sqlite3_errmsg16(IntPtr database);
        [DllImport(Dll, CharSet = CharSet.Unicode, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_prepare16_v2(IntPtr database, string sql, int bytes, out IntPtr stmt, IntPtr tail);
        [DllImport(Dll, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_step(IntPtr stmt);
        [DllImport(Dll, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_finalize(IntPtr stmt);
        [DllImport(Dll, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_bind_null(IntPtr stmt, int index);
        [DllImport(Dll, CharSet = CharSet.Unicode, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_bind_text16(IntPtr stmt, int index, string value, int bytes, IntPtr dispose);
        [DllImport(Dll, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_bind_int(IntPtr stmt, int index, int value);
        [DllImport(Dll, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_bind_double(IntPtr stmt, int index, double value);
        [DllImport(Dll, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_column_type(IntPtr stmt, int column);
        [DllImport(Dll, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_column_int(IntPtr stmt, int column);
        [DllImport(Dll, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
        private static extern double sqlite3_column_double(IntPtr stmt, int column);
        [DllImport(Dll, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr sqlite3_column_text16(IntPtr stmt, int column);
        [DllImport(Dll, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr sqlite3_backup_init(IntPtr destination, string destinationName, IntPtr source, string sourceName);
        [DllImport(Dll, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_backup_step(IntPtr backup, int pages);
        [DllImport(Dll, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_backup_finish(IntPtr backup);
    }
}
