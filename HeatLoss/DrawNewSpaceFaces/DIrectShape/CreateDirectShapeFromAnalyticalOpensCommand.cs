using System;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using HVACLoadTerminals.Utils;

namespace HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.DirectShape;

[Transaction(TransactionMode.Manual)]
public class CreateDirectShapeFromAnalyticalOpensCommand : IExternalCommand
{
    private const string NorthDirection = "up";
    private readonly LoggingService _logger = new();


    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        var doc = commandData.Application.ActiveUIDocument.Document;
        var groundLevel = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_Levels)
            .WhereElementIsNotElementType()
            .Cast<Level>()
            .OrderBy(l => l.Elevation)
            .ToList()[3];
        _logger.Log($"Ground Level: {groundLevel.Name}");

        // 1. Собираем аналитические пространства
        try
        {
            using var transaction = new Transaction(doc, "Link Spaces to DirectShapes");
            transaction.Start();
            var analyticSpaces = CollectorQuery.GetAllaAnalysisSpaces(doc);
            foreach (var analyticSpace in analyticSpaces)
            {
                // 2. Находим связанное механическое пространство
                var mechSpace = AnalyticalModelProcessor.FindMechanicalSpaceForAnalyticSpace(analyticSpace, doc);
                if (mechSpace == null) continue;

                // 3. Получаем поверхности аналитического пространства
                var surfaces = analyticSpace.GetAnalyticalSurfaces().ToList();
                foreach (var surface in surfaces.Where(AnalyticalModelProcessor.IsExteriorWall))
                {
                    // 4. Создаем DirectShape с параметром Space
                    var dsShapeCreator = new DirectShapeCreator(doc, surface, mechSpace, NorthDirection, groundLevel);
                    dsShapeCreator.CreateDirectShapeForSurface();
                    dsShapeCreator.CreateDirectShapeForOpenings();
                }
            }

            transaction.Commit();
            return Result.Succeeded;
        }

        catch (Exception ex)
        {
            _logger.Log($"Ошибка: {ex.Message}");
            return Result.Failed;
        }
    }
}