using System;
using System.IO;
using System.Linq;

namespace HVACLoadTerminals.App
{
    /// <summary>
    /// Simple file logger for the standalone app:
    /// %LocalAppData%\HVACLoadTerminals\logs\app-yyyy-MM-dd.log
    /// Never throws.
    /// </summary>
    public static class AppLogger
    {
        private static readonly object Gate = new object();

        public static string LogDirectory { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HVACLoadTerminals", "logs");

        public static void Info(string message) => Write("INFO ", message);
        public static void Warn(string message) => Write("WARN ", message);
        public static void Error(string message, Exception? ex = null) =>
            Write("ERROR", message + (ex == null ? "" :
                "\n  Exception: " + ex.GetType().Name + ": " + ex.Message +
                "\n  Stack: " + ex.StackTrace));

        private static void Write(string level, string message)
        {
            try
            {
                Directory.CreateDirectory(LogDirectory);
                var path = Path.Combine(LogDirectory,
                    "app-" + DateTime.Now.ToString("yyyy-MM-dd") + ".log");
                lock (Gate)
                {
                    File.AppendAllText(path,
                        DateTime.Now.ToString("HH:mm:ss.fff") + " [" + level + "] " +
                        message + Environment.NewLine);
                }
            }
            catch
            {
                // Logging must never crash the app.
            }
        }
    }
}
