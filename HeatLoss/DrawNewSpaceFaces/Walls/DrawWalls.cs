using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Mechanical;
using HVACLoadTerminals.ClimateData;
using HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Utils;
using HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls.Calculators;
using HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls.ParametersHandlersStrategy;
using HVACLoadTerminals.Utils;

namespace HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls;

public class DrawWalls
    {
        private readonly Document _hvacDocument;
        private readonly Document _roomDocument;
        private readonly ILogger _logger;
        private readonly Dictionary<string, Room> _roomCache = new();
        private readonly Dictionary<ElementId, XYZ> _spaceLocationCache = new();
        public bool IsReady => _hvacDocument != null && _roomDocument != null && _roomDocument.IsValidObject;

        public List<Wall> WallList { get; } = [];
        private readonly IEnumerable<Room> _roomList;
        private readonly IEnumerable<Space> _spaceList;

        public DrawWalls(Document hvacDocument, Document roomDocument)
        {
            _hvacDocument = hvacDocument ?? throw new ArgumentNullException(nameof(hvacDocument));
            _roomDocument = roomDocument ?? throw new ArgumentNullException(nameof(roomDocument));
            _logger = new LoggingService();
            _roomList = CollectorQuery.GetAllRooms(roomDocument).Cast<Room>();
            _spaceList = CollectorQuery.GetAllSpaces(hvacDocument).Cast<Space>();
            InitializeCaches();
        }

        private void InitializeCaches()
        {
            // Кэширование пространств
            foreach (var space in _spaceList)
            {
                if (space.Location is LocationPoint lp)
                {
                    _spaceLocationCache[space.Id] = lp.Point;
                }
            }

            // Кэширование комнат
            foreach (var room in _roomList)
            {
                _roomCache[GetRoomKey(room)] = room;
            }
        }

        public void DrawWallsForSelectedSpaces(
            string northDirection,
            Level groundLevel,
            HashSet<ElementId> selectedTypes = null)
        {
            ValidateInputParameters(northDirection, groundLevel);

            using var transactionGroup = new TransactionGroup(_hvacDocument, "Create Walls");
            transactionGroup.Start();

            try
            {
                foreach (var space in _spaceList)
                {
                    ProcessSpace(space, northDirection, groundLevel, selectedTypes);
                }

                transactionGroup.Assimilate();
                _logger.Log($"Успешно создано стен: {WallList.Count}");
            }
            catch (Exception ex)
            {
                transactionGroup.RollBack();
                _logger.Log($"Ошибка: {ex.Message}", LogLevel.Error);
            }
        }

        private void ProcessSpace(Space space, string northDirection, Level groundLevel, HashSet<ElementId> selectedTypes)
        {
            var room = FindAssociatedRoom(space);
            if (room == null) return;

            var faces = VerticalWallFacesCalculator.GetRoomExternalVerticalFaces(
                _roomDocument,
                room,
                selectedTypes);

            foreach (var faceData in faces)
            {
                using var transaction = new Transaction(_hvacDocument, "Create Wall");
                try
                {
                    transaction.Start();
                    var wall = CreateWall(space, faceData, northDirection, groundLevel);
                    if (wall != null)
                    {
                        SetSpaceAssociation(wall, space.Id);
                        WallList.Add(wall);
                        transaction.Commit();
                    }
                }
                catch
                {
                    transaction.RollBack();
                }
            }
        }

        private Room FindAssociatedRoom(Space space)
        {
            // Поиск по геометрии
            if (_spaceLocationCache.TryGetValue(space.Id, out var point))
            {
                foreach (var room in _roomCache.Values)
                {
                    if (room.IsPointInRoom(point)) return room;
                }
            }

            // Резервный поиск по номеру
            return _roomCache.Values.FirstOrDefault(r => r.Number == space.Number);
        }

        private Wall CreateWall(
            Space space,
            ConstructionSurfaceModel faceModel,
            string northDirection,
            Level groundLevel)
        {
            var curve = GetValidCurve(faceModel._Face);
            if (curve == null) return null;

            var wall = Wall.Create(_hvacDocument, curve, space.Level.Id, false);
            SetWallParameters(space, faceModel, northDirection, curve, wall, groundLevel);
            return wall;
        }

        private Curve GetValidCurve(Face face)
        {
            return face.GetEdgesAsCurveLoops()?
                .FirstOrDefault()?
                .OfType<Line>()
                .OrderByDescending(l => l.Length)
                .FirstOrDefault();
        }

        private void SetWallParameters(
            Space space,
            ConstructionSurfaceModel faceModel,
            string northDirection,
            Curve wallCurve,
            Wall wall,
            Level groundLevel)
        {
            try
            {
                var strategy = new WallParametersStrategyFactory(_hvacDocument, northDirection)
                    .CreateStrategy(space, groundLevel);
                
                strategy.ApplyParameters(wall, space, faceModel, wallCurve, groundLevel);

                ParametersUtility.SetParameterByValueAndName(
                    wall,
                    "TemperatureInSpace",
                    ParametersHandler.GetSpaceSetHeatPoint(_hvacDocument, space));

                ParametersUtility.SetParameterByValueAndName(
                    wall,
                    "TemperatureOut",
                    ParametersHandler.GetProjectInformation(
                        _hvacDocument,
                        nameof(ClimateDataModel.TWinterOut092)));
            }
            catch (Exception ex)
            {
                _logger.Log($"Ошибка параметров: {ex.Message}");
            }
        }

        private void SetSpaceAssociation(Wall wall, ElementId spaceId)
        {
            wall.LookupParameter("AssociatedSpace")?.Set(spaceId);
        }

        private static string GetRoomKey(Room room)
        {
            return $"{room.LevelId}-{room.Number}";
        }

        private static void ValidateInputParameters(string northDirection, Level groundLevel)
        {
            if (string.IsNullOrEmpty(northDirection))
                throw new ArgumentException("Не задано направление на север");

            if (groundLevel == null)
                throw new ArgumentNullException(nameof(groundLevel));
        }
    }


public class SilentFailureProcessor : IFailuresPreprocessor
{
    public FailureProcessingResult PreprocessFailures(FailuresAccessor failuresAccessor)
    {
        foreach (var failure in failuresAccessor.GetFailureMessages())
        {
            if (failure.GetSeverity() == FailureSeverity.Warning)
            {
                failuresAccessor.DeleteWarning(failure);
            }
        }
        return FailureProcessingResult.Continue;
    }
}