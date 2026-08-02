using System;
using System.IO;

namespace HVACLoadTerminals.Revit.Logging
{
    /// <summary>
    /// Простой файловый логгер для add-in'а. Пишет в
    /// <c>%LocalAppData%\HVACLoadTerminals\logs\hvac-revit-yyyy-MM-dd.log</c>.
    /// Потокобезопасен, никогда не бросает исключений наружу
    /// (ошибки записи игнорируются — логирование не должно ломать основной код).
    /// </summary>
    public static class HvacLogger
    {
        private const string FolderName = "HVACLoadTerminals";
        private const string SubFolder = "logs";
        private const string Prefix = "hvac-revit-";

        private static readonly object _lock = new object();
        private static string? _cachedPath;

        public static string LogFilePath
        {
            get
            {
                if (_cachedPath != null) return _cachedPath;
                lock (_lock)
                {
                    if (_cachedPath != null) return _cachedPath;
                    var dir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        FolderName, SubFolder);
                    try { Directory.CreateDirectory(dir); }
                    catch { /* ignore — fall back to %TEMP% below */ }

                    _cachedPath = Path.Combine(
                        string.IsNullOrEmpty(dir) ? Path.GetTempPath() : dir,
                        Prefix + DateTime.Now.ToString("yyyy-MM-dd") + ".log");
                    return _cachedPath;
                }
            }
        }

        public static void Info(string message) => Write("INFO", message, null);
        public static void Warn(string message) => Write("WARN", message, null);
        public static void Error(string message, Exception? ex = null) => Write("ERROR", message, ex);

        public static void LogException(string context, Exception ex)
            => Write("ERROR", context + ": " + ex.GetType().Name + ": " + ex.Message, ex);

        private static void Write(string level, string message, Exception? ex)
        {
            try
            {
                var line = string.Format(
                    "[{0:yyyy-MM-dd HH:mm:ss.fff}] [{1}] {2}",
                    DateTime.Now, level, message);
                if (ex != null)
                {
                    line += Environment.NewLine + ex.ToString();
                }
                lock (_lock)
                {
                    File.AppendAllText(LogFilePath, line + Environment.NewLine);
                }
            }
            catch
            {
                // Логгирование не должно бросать исключения.
            }
        }
    }
}
