using System;
using System.Linq;
using Autodesk.Revit.DB;
using HVACLoadTerminals.ModelsStatic;

namespace HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls.Calculators;

// Калькулятор ориентации
public class OrientationCalculator
{
    public string Calculate(Curve curve, string northDirection)
    {
        //northDirection up,down,left,right)
        var mapping = OrientationMapping.OrientationMappings.FirstOrDefault(m =>
            m.MainDirection.ToLower() == northDirection.ToLower());
        if (curve is Arc)
        {
            // Если кривая - дуга, преобразуем ее в линию
            var startPoint = curve.GetEndPoint(0);
            var endPoint = curve.GetEndPoint(1);
            curve = Line.CreateBound(startPoint, endPoint);
        }

        // Получение вектора направления кривой
        var curveDirection = curve.GetEndPoint(1) - curve.GetEndPoint(0);
        curveDirection.Normalize(); // Нормализация вектора
        return CurveNormalizeMappingOrientation(curveDirection, mapping);
    }

    private static string CurveNormalizeMappingOrientation(XYZ curveDirection, OrientationMapping mapping)
    {
        // Определение ориентации
        if (Math.Abs(curveDirection.Y) > 0.9) // Вертикальное направление (С/Ю)
        {
            return curveDirection.Y > 0 ? mapping.N : mapping.S;
        }
        else if (Math.Abs(curveDirection.X) > 0.9) // Горизонтальное направление (В/З)
        {
            return curveDirection.X > 0 ? mapping.E : mapping.W;
        }
        else
        {
            // Промежуточные направления
            if (curveDirection.X > 0 && curveDirection.Y > 0)
            {
                return mapping.NE; // Северо-восток
            }
            else if (curveDirection.X < 0 && curveDirection.Y > 0)
            {
                return mapping.NW; // Северо-запад
            }
            else if (curveDirection.X > 0 && curveDirection.Y < 0)
            {
                return mapping.SE; // Юго-восток
            }
            else if (curveDirection.X < 0 && curveDirection.Y < 0)
            {
                return mapping.SW; // Юго-запад
            }
            else
            {
                return "Не определено"; // Ориентация не определена
            }
        }
    }
}