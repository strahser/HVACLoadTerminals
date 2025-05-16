using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI;
using HVACLoadTerminals.ModelsStatic;
using HVACLoadTerminals.Utils;

namespace HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls.RayCasting.commands
{
    [Transaction(TransactionMode.Manual)]
    public class CreateRaycastingExteriorWallsCommand : IExternalCommand
    {
        private Document _doc;
        private readonly BoundaryProcessor _boundaryProcessor = new(null);
        private readonly WallCreator _wallCreator = new(null);
        private readonly SpaceAnalyzer _spaceAnalyzer = new(null);
        private readonly LoggingService _logger = new();

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
                
                // Кэширование всех пространств
                _spaceAnalyzer.CacheSpaces();

                using Transaction tx = new Transaction(_doc, "Создание наружных стен");
                tx.Start();
                // Обработка всех помещений
                var spaces = CollectorQuery.GetAllSpaces(_doc).Cast<Space>();

                foreach (var space in spaces)
                {
                    ProcessSpace(space);
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

        private void ProcessSpace(Space space)
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
                    var wall = _wallCreator.CreateWall(curve, levelId);
                    SetWallParameters(wall,space);
                }
            }
        }

        private static void SetWallParameters(Wall wall, Space space)
        {
            ParametersUtility.SetParameterByValueAndName(
                wall, nameof(ConstructionSurfaceModel.SpaceName), space.Name);
            ParametersUtility.SetParameterByValueAndName(
                wall, nameof(ConstructionSurfaceModel.SpaceId), space.Id.ToString());
            ParametersUtility.SetParameterByValueAndName(
                wall, nameof(ConstructionSurfaceModel.SpaceName), space.Name);
            ParametersUtility.SetParameterByValueAndName(
                wall, nameof(ConstructionSurfaceModel.SpaceNumber), space.Number.ToString());
            ParametersUtility.SetParameterByValueAndName(
                wall, nameof(ConstructionSurfaceModel.EnclosureType), EnclosureTypeOptions.Wall);
        }

    }
}