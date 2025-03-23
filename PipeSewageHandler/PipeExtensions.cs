using System;

namespace HVACLoadTerminals.PipeSewageHandler;

using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;

public static class PipeExtensions
{
    /// <summary>
    /// Возвращает нормализованное направление вертикальной трубы
    /// </summary>
    public static XYZ GetVerticalDirection(this Pipe pipe)
    {
        var locationCurve = pipe?.Location as LocationCurve;
        var curve = locationCurve?.Curve as Line;
        return curve?.Direction.Normalize() ?? XYZ.BasisZ;
    }

    /// <summary>
    /// Проверяет, является ли труба вертикальной (допуск ±10%)
    /// </summary>
    public static bool IsVertical(this Pipe pipe)
    {
        var locationCurve = pipe?.Location as LocationCurve;
        var curve = locationCurve?.Curve as Line;
        return curve != null && Math.Abs(curve.Direction.Z) > 0.9;
    }
}