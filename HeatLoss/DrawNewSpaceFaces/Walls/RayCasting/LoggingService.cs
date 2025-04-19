using System;
using System.Diagnostics;
using System.IO;

namespace HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls.RayCasting
{
    public class LoggingService
    {
        public void Log(string message)
        {
            string logMessage = $"{DateTime.Now:HH:mm:ss.fff} | {message}";
            Debug.WriteLine(logMessage);
            WriteToFile(logMessage);
        }

        private void WriteToFile(string message)
        {
            string path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "RevitWallLog.txt");
            File.AppendAllText(path, message + Environment.NewLine);
        }
    }
}