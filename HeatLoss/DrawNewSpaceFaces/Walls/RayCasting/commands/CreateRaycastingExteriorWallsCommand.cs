using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI;
using HVACLoadTerminals.Utils;

namespace HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls.RayCasting.commands
{
    [Transaction(TransactionMode.Manual)]
    public class CreateRaycastingExteriorWallsCommand : IExternalCommand
    {
        private Document _doc;
        private View3D _view3D;
        private readonly BoundaryProcessor _boundaryProcessor = new(null);
        private readonly WallCreator _wallCreator = new(null);
        private readonly SpaceAnalyzer _spaceAnalyzer = new(null);
        private readonly LoggingService _logger = new();
        private readonly GeometryUtility _geometryUtility = new();

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            _doc = uidoc.Document;

            try
            {
                _boundaryProcessor._doc = _doc;
                _wallCreator._doc = _doc;
                _spaceAnalyzer._doc = _doc;

                /*_view3D = Get3DView();
                if (_view3D == null)
                {
                    message = "Требуется 3D вид для анализа";
                    return Result.Failed;
                }*/

                // Кэширование всех пространств
                _spaceAnalyzer.CacheSpaces();

                using Transaction tx = new Transaction(_doc, "Создание наружных стен");
                tx.Start();

                // Сбор всех границ
                var allBoundaries = _boundaryProcessor.GetAllBoundaryData();

                // Обработка всех помещений
                var spaces = CollectorQuery.GetAllSpaces(_doc).Cast<Space>();

                foreach (var space in spaces)
                {
                    ProcessSpace(space, allBoundaries.Select(x=>x.CurveData).ToList());
                }
                tx.Commit();
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = $"Ошибка: {ex.Message}";
                _logger.Log($"CRITICAL ERROR: {ex}");
                return Result.Failed;
            }
        }

        private void ProcessSpace(Space space, List<Curve> allBoundaries)
        {
            if (space == null) return;

            var boundaries = space.GetBoundarySegments(new SpatialElementBoundaryOptions());
            var levelId = space.Level.Id;

            foreach (var loop in boundaries)
            {
                foreach (var segment in loop)
                {
                    Curve curve = segment.GetCurve();
                    if (curve == null) continue;
                    _wallCreator.CreateWall(curve, levelId);
                    /*if (IsExteriorWall(space, curve, allBoundaries))
                    {
                        
                    }*/
                }
            }
        }

        private bool IsExteriorWall(Space space, Curve curve, List<Curve> allBoundaries)
        {
            int validPoints = 0;
            var points = _geometryUtility.GetSamplePoints(curve);

            foreach (var point in points)
            {
                XYZ outwardDirection = _geometryUtility.GetOutwardDirection(curve, point, space,_view3D);
                if (outwardDirection == null) continue;

                XYZ endPoint = point + outwardDirection * 1.0;

                // Проверка нахождения в других помещениях
                if (_spaceAnalyzer.IsPointInAnySpace(endPoint, space.Id)) continue;

                // Проверка пересечений
                if (!_geometryUtility.DoesNormalIntersectOtherCurves(point, endPoint, curve, allBoundaries))
                {
                    validPoints++;
                }
            }

            return validPoints >= (points.Count / 2 + 1);
        }

        private View3D Get3DView()
        {
            return new FilteredElementCollector(_doc)
                .OfClass(typeof(View3D))
                .Cast<View3D>()
                .FirstOrDefault(v => 
                    !v.IsTemplate && 
                    v.CanBePrinted && 
                    !v.IsPerspective);
        }
    }
}