using System;
using System.IO;

namespace OpenSurge
{
    static class Logger
    {
        static readonly string LogFile =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "OpenSurge.log");

        static readonly object _lock = new object();

        public static void Info(string message)  { Write("INFO ", message); }
        public static void Debug(string message) { Write("DEBUG", message); }
        public static void Warn(string message)  { Write("WARN ", message); }
        public static void Error(string message) { Write("ERROR", message); }
        public static void Error(Exception ex)   { Write("ERROR", ex.ToString()); }

        static void Write(string level, string message)
        {
            string line = string.Format("[{0:yyyy-MM-dd HH:mm:ss}] {1} {2}",
                DateTime.Now, level, message);

            System.Diagnostics.Debug.WriteLine(line);

            lock (_lock)
            {
                try { File.AppendAllText(LogFile, line + Environment.NewLine); }
                catch { /* ignore write errors */ }
            }
        }
    }
}
