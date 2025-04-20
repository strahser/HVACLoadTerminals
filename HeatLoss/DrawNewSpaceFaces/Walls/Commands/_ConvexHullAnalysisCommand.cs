using System.Collections.Generic;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace HVACLoadTerminals.HeatLoss.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class _ConvexHullAnalysisCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;

            // Вычисляем выпуклую оболочку
            List<XYZ> convexHullPoints = ComputeConvexHull(doc);

            // Находим наружные стены
            List<ElementId> externalWallIds = GetExternalWallsUsingConvexHull(doc, convexHullPoints);

            TaskDialog.Show("Результат", $"Найдено наружных стен: {externalWallIds.Count}");

            return Result.Succeeded;
        }

        private List<XYZ> ComputeConvexHull(Document doc)
        {
            // Пример: вычисление выпуклой оболочки
            return new List<XYZ>(); // Заглушка
        }

        private List<ElementId> GetExternalWallsUsingConvexHull(Document doc, List<XYZ> convexHullPoints)
        {
            List<ElementId> externalWallIds = new List<ElementId>();

            FilteredElementCollector wallsCollector = new FilteredElementCollector(doc)
                .OfClass(typeof(Wall));

            foreach (Wall wall in wallsCollector)
            {
                LocationCurve locationCurve = wall.Location as LocationCurve;
                if (locationCurve == null) continue;

                Curve wallCurve = locationCurve.Curve;

                if (IsWallIntersectingConvexHull(wallCurve, convexHullPoints))
                {
                    externalWallIds.Add(wall.Id);
                }
            }

            return externalWallIds;
        }

        private bool IsWallIntersectingConvexHull(Curve wallCurve, List<XYZ> convexHullPoints)
        {
            for (int i = 0; i < convexHullPoints.Count; i++)
            {
                XYZ start = convexHullPoints[i];
                XYZ end = convexHullPoints[(i + 1) % convexHullPoints.Count];

                Line hullLine = Line.CreateBound(start, end);
                if (wallCurve.Intersect(hullLine) != SetComparisonResult.Disjoint)
                {
                    return true;
                }
            }
            return false;
        }
    }
}