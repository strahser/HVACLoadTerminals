using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls.Zones;

[Transaction(TransactionMode.Manual)]
public class TestZoneWallsCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        var doc = commandData.Application.ActiveUIDocument.Document;
        var uidoc = new UIDocument(doc);

        try
        {
            // 1. Выбор уровня земли
            var groundLevel = doc.GetElement(uidoc.Selection.PickObject(
                    ObjectType.Element, 
                    new WallSelectionFilter(), 
                    "Выберите уровень земли")
            ) as Level;

            if (groundLevel == null) return Result.Cancelled;

            // 2. Создание базовой линии (10 метров вдоль оси X)
            var startPoint = new XYZ(
                UnitUtils.ConvertToInternalUnits(0, UnitTypeId.Meters),
                UnitUtils.ConvertToInternalUnits(0, UnitTypeId.Meters),
                groundLevel.Elevation
            );

            var endPoint = new XYZ(
                UnitUtils.ConvertToInternalUnits(10, UnitTypeId.Meters),
                UnitUtils.ConvertToInternalUnits(0, UnitTypeId.Meters),
                groundLevel.Elevation
            );

            var wallCurve = Line.CreateBound(startPoint, endPoint);

            // 3. Создание стен для 3 зон
            using (var tr = new Transaction(doc, "Создать зонированные стены"))
            {
                tr.Start();

                CreateZoneWall(doc, wallCurve, groundLevel, -2.0, 2.0, "Зона I");
                CreateZoneWall(doc, wallCurve, groundLevel, -4.0, 2.0, "Зона II");
                CreateZoneWall(doc, wallCurve, groundLevel, -6.0, 2.0, "Зона III");
                tr.Commit();
            }

            return Result.Succeeded;
        }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
        {
            return Result.Cancelled;
        }
        catch (Exception ex)
        {
            message = $"Ошибка: {ex.Message}";
            return Result.Failed;
        }
    }

    private static void CreateZoneWall(
        Document doc, 
        Curve curve, 
        Level baseLevel, 
        double baseOffsetMeters, 
        double heightMeters, 
        string zoneName)
    {
        // Создание стены с использованием стандартного типа
        var wall = Wall.Create(
            doc,
            curve,
            baseLevel.Id,
             false
        );

        // Установка параметров высоты
        wall.get_Parameter(BuiltInParameter.WALL_BASE_OFFSET)
            .Set(UnitUtils.ConvertToInternalUnits(baseOffsetMeters, UnitTypeId.Meters));

        wall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM)
            .Set(UnitUtils.ConvertToInternalUnits(heightMeters, UnitTypeId.Meters));

        // Назначение параметра зоны
        wall.LookupParameter("Комментарии").Set(zoneName);
    }
}

// Фильтр для выбора только уровней
public class WallSelectionFilter : ISelectionFilter
{
    public bool AllowElement(Element elem) => elem is Level;
    public bool AllowReference(Reference reference, XYZ position) => false;
}