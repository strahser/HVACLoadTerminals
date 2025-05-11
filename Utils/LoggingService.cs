using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace HVACLoadTerminals.Utils;
public enum LogLevel
{
    Info,
    Warning,
    Error
}

public interface ILogger
{
    void Log(string message, LogLevel level = LogLevel.Info);
}
public class LoggingService(string fileName = "RevitWallLog.txt") : ILogger
{
    private static long _logCounter = 0;
    public void Log(string message, LogLevel level = LogLevel.Info)
    {
        long logNumber = Interlocked.Increment(ref _logCounter);
        string logMessage = $"{logNumber:D5} | {DateTime.Now:HH:mm:ss.fff} | {level.ToString().ToUpper()} | {message}";
        
        Debug.WriteLine(logMessage);
        WriteToFile(logMessage);
    }

    private void WriteToFile(string message)
    {
        try
        {
            string path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),fileName 
                );
            
            File.AppendAllText(path, message + Environment.NewLine);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Ошибка записи в лог: {ex.Message}");
        }
    }
}