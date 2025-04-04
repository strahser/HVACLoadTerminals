using System;
using System.Diagnostics;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;

namespace HVACLoadTerminals.PipeSewageHandler;

public static class Logger
{
    public static bool IsDebugEnabled { get; set; } = true;

    [Conditional("DEBUG")] // Этот метод будет скомпилирован только в DEBUG-сборке
    public static void Log(string message)
    {
        Debug.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + ": " + message);
    }

    [Conditional("DEBUG")]
    public static void Log(string message, params object[] args)
    {
        Log(string.Format(message, args));
    }

    [Conditional("DEBUG")]
    public static void LogException(Exception ex, string context = "")
    {
        Log("Exception: " + ex.Message + (string.IsNullOrEmpty(context) ? "" : " Context: " + context));
        Log("Stack Trace: " + ex.StackTrace);
    }

    public static void Error(string message, Exception ex = null)
    {
        Debug.WriteLine($"ERROR: {message} {ex?.Message}");
    }
    
    public static void LogConnectorInfo(FamilyInstance tee, string stage)
    {
        var connectors = tee.MEPModel?.ConnectorManager?.Connectors?
            .Cast<Connector>()
            .ToList();

        Logger.Log($"\n{stage}:");

        if (connectors == null || !connectors.Any())
        {
            Logger.Log("Коннекторы не найдены");
            return;
        }

        foreach (var conn in connectors)
        {
            
            // Исправленная строка с правильным форматированием
            Logger.Log(
                $"Коннектор {conn.Id}: " +
                $"Подключен к {conn.AllRefs} элементам | " +
                $"Направление: {conn.CoordinateSystem?.BasisZ.ToString() ?? "N/A"}"
            );
        }
    }

    public static void LogPipeGeometry(Pipe pipe)
    {
        var locCurve = pipe.Location as LocationCurve;
        if (locCurve?.Curve is Line line)
        {
            Logger.Log($"Направление трубы: {(line.GetEndPoint(1) - line.GetEndPoint(0)).Normalize()}");
            Logger.Log($"Высота трубы (Z): {line.GetEndPoint(0).Z} -> {line.GetEndPoint(1).Z}");
        }
    }

    public static void LogPipeConnectors(Pipe pipe, XYZ point)
    {
        var connectors = pipe.ConnectorManager.Connectors
            .Cast<Connector>()
            .OrderBy(c => c.Origin.DistanceTo(point))
            .ToList();

        Logger.Log($"Коннекторы трубы ({connectors.Count}):");
        foreach (Connector conn in connectors)
        {
            Logger.Log($"ID: {conn.Id} | Тип: {conn.ConnectorType} | " +
                       $"Позиция: {conn.Origin} | Направление: {conn.CoordinateSystem.BasisZ}");
        }
    }
}
