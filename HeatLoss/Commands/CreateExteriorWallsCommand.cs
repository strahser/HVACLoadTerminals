using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI;
using HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls.RayCasting;

namespace HVACLoadTerminals.HeatLoss.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class CreateExteriorWallsCommand : IExternalCommand
    {
        private Document _doc;
        private View3D _view3D;
        private readonly BoundaryProcessor _boundaryProcessor;
        private readonly WallCreator _wallCreator;
        private readonly SpaceAnalyzer _spaceAnalyzer;
        private readonly LoggingService _logger;
        private readonly GeometryUtility _geometryUtility;

        public CreateExteriorWallsCommand()
        {
            _boundaryProcessor = new BoundaryProcessor(null);
            _wallCreator = new WallCreator(null);
            _spaceAnalyzer = new SpaceAnalyzer(null);
            _logger = new LoggingService();
            _geometryUtility = new GeometryUtility();
        }

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

                _view3D = Get3DView();
                if (_view3D == null)
                {
                    message = "Требуется 3D вид для анализа";
                    return Result.Failed;
                }

                // Кэширование всех пространств
                _spaceAnalyzer.CacheSpaces();

                using (Transaction tx = new Transaction(_doc, "Создание наружных стен"))
                {
                    tx.Start();

                    // Сбор всех границ
                    var allBoundaries = _boundaryProcessor.GetAllBoundaries();

                    // Обработка всех помещений
                    var spaces = new FilteredElementCollector(_doc)
                        .OfCategory(BuiltInCategory.OST_MEPSpaces)
                        .WhereElementIsNotElementType()
                        .Cast<Space>();

                    foreach (var space in spaces)
                    {
                        ProcessSpace(space, allBoundaries);
                    }

                    tx.Commit();
                }
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

                    if (IsExteriorWall(space, curve, allBoundaries))
                    {
                        _wallCreator.CreateWall(curve, levelId);
                    }
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