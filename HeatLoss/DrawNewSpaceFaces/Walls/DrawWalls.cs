using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Mechanical;
using HVACLoadTerminals.ClimateData;
using HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls.Calculators;
using HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls.ParametersHandlersStrategy;
using HVACLoadTerminals.Utils;

namespace HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls;

public class DrawWalls
{
    private readonly Document _hvacDocument;
    private readonly Document _roomDocument;
    private readonly ILogger _logger;
    private readonly FailedFacesManager _failedFacesManager;
    
    private readonly List<Room> _cachedRooms;
    private readonly List<Space> _cachedSpaces;
    private readonly Dictionary<string, Room> _roomKeyCache = new();
    private readonly Dictionary<ElementId, string> _spaceRoomKeyMap = new();
    public List<Wall> CreatedWalls { get; } = [];
    public bool IsReady => _hvacDocument != null && _roomDocument?.IsValidObject == true;
    public List<string> FailedFaceKeys => _failedFacesManager.FailedFaceKeys;

    public DrawWalls(Document hvacDocument, Document roomDocument)
    {
        _hvacDocument = hvacDocument;
        _roomDocument = roomDocument;
        _logger = new LoggingService("DrawWalls.log");
        _failedFacesManager = new FailedFacesManager(_logger);
        ParametersHandler.GetProjectInformation(hvacDocument, nameof(ClimateDataModel.TWinterOut092));
        
        _cachedRooms = CollectorQuery.GetAllRooms(roomDocument);
        _cachedSpaces = CollectorQuery.GetAllSpaces(hvacDocument).Cast<Space>().ToList();
        
        InitializeCaches();
    }

    private void InitializeCaches()
    {
        foreach (var room in _cachedRooms)
        {
            var key = $"{room.LevelId}_{room.Number}";
            if (!_roomKeyCache.ContainsKey(key)) _roomKeyCache.Add(key, room);
        }

        foreach (var space in _cachedSpaces.Where(s => s.Location is LocationPoint))
        {
            var spacePoint = ((LocationPoint)space.Location).Point;
            var room = _cachedRooms.FirstOrDefault(r => r.IsPointInRoom(spacePoint));
            if (room != null) _spaceRoomKeyMap[space.Id] = GetRoomKey(room);
        }
    }

    public void CreateWallsForSpaces(string northDirection, Level groundLevel, HashSet<ElementId> filter = null)
    {
        ValidateInput(northDirection, groundLevel);
        
        using var transactionGroup = new TransactionGroup(_hvacDocument, "Create Walls");
        transactionGroup.Start();

        foreach (var space in _cachedSpaces)
        {
            ProcessSpaceWalls(space, northDirection, groundLevel, filter);
        }

        transactionGroup.Assimilate();
        _failedFacesManager.LogFailedOperations();
    }

    private void ProcessSpaceWalls(Space space, string northDirection, Level groundLevel, HashSet<ElementId> filter)
    {
        if (!_spaceRoomKeyMap.TryGetValue(space.Id, out var roomKey))
        {
            _logger.Log($"Space {space.Id}: No linked room found", LogLevel.Warning);
            return;
        }

        if (!_roomKeyCache.TryGetValue(roomKey, out var room))
        {
            _logger.Log($"Room {roomKey} not found in cache", LogLevel.Error);
            return;
        }

        var faces = VerticalWallFacesCalculator
            .GetExternalFaces(_roomDocument, room, filter);
        foreach (var face in faces)
        {
            ProcessSingleFace(space, face, northDirection, groundLevel);
        }
    }

    private void ProcessSingleFace(Space space, ConstructionSurfaceModel face, string north, Level groundLevel)
    {
        if (face?._Face == null)
        {
            _logger.Log($"Invalid face model for space {space.Id}", LogLevel.Error);
            return;
        }

        var faceKey = $"{space.Id}_{face.FaceId}";
        Curve curve = null;

        try
        {
            curve = GetFaceCurveWithValidation(face._Face);
            if (curve == null)
            {
                _failedFacesManager
                    .RegisterFailure(faceKey, space, face, null, "Invalid face geometry");
                return;
            }

            using var transaction = new Transaction(_hvacDocument, $"Create Wall {faceKey}");
            transaction.Start();

            var wall = CreateWallFromCurve(space, curve, face, north, groundLevel);
            if (wall != null)
            {
                CreatedWalls.Add(wall);
                transaction.Commit();
                _failedFacesManager.RemoveFace(faceKey);
            }
            else
            {
                transaction.RollBack();
                _failedFacesManager
                    .RegisterFailure(faceKey, space, face, curve, "Failed to create wall");
            }
        }
        catch (Exception ex)
        {
            _failedFacesManager
                .RegisterFailure(faceKey, space, face, curve, ex.Message);
            LogErrorDetails(curve, face, ex);
        }
    }

    private Curve GetFaceCurveWithValidation(Face face)
    {
        if (face == null)
        {
            _logger.Log("Face object is null", LogLevel.Error);
            return null;
        }

        try
        {
            var curveLoops = face.GetEdgesAsCurveLoops();
            return curveLoops?.FirstOrDefault()?.FirstOrDefault();
        }
        catch (Autodesk.Revit.Exceptions.ArgumentNullException ex)
        {
            _logger.Log($"Invalid face: {ex.Message}", LogLevel.Error);
            return null;
        }
        catch (Autodesk.Revit.Exceptions.InternalException ex)
        {
            _logger.Log($"Revit API Error: {ex.Message}", LogLevel.Error);
            return null;
        }
    }

    private Wall CreateWallFromCurve(Space space, Curve curve, ConstructionSurfaceModel face, 
        string north, Level groundLevel)
    {
        if (space.Level == null)
        {
            _logger.Log($"Space {space.Id} has no level", LogLevel.Error);
            return null;
        }

        var wall = Wall.Create(_hvacDocument, curve, space.Level.Id, false);
        if (wall == null) return null;

        try
        {
            ConfigureWallParameters(wall, space, face, north, groundLevel, curve);
            return wall;
        }
        catch
        {
            _hvacDocument.Delete(wall.Id);
            return null;
        }
    }

    private void ConfigureWallParameters(Wall wall, Space space, ConstructionSurfaceModel face, 
        string north, Level groundLevel, Curve curve)
    {
        var strategy = new WallParametersStrategyFactory(_hvacDocument, north)
            .CreateStrategy(space, groundLevel);
        strategy.ApplyParameters(wall, space, face, curve, groundLevel);
    }

    public void RetryFailedWalls(string northDirection, Level groundLevel)
    {
        if (!IsReady)
        {
            _logger.Log("DrawWalls is not initialized", LogLevel.Error);
            return;
        }

        using var transactionGroup = new TransactionGroup(_hvacDocument, "Retry Failed Walls");
        transactionGroup.Start();

        _failedFacesManager.RetryFailedFaces(data =>
        {
            try
            {
                using var transaction = new Transaction(_hvacDocument, $"Retry Wall {data.FaceKey}");
                transaction.Start();

                if (data.Space == null || !data.Space.IsValidObject)
                {
                    _logger.Log($"Space {data.FaceKey} is invalid", LogLevel.Warning);
                    return;
                }

                var wall = CreateWallFromCurve(data.Space, data.Curve, data.Face, northDirection, groundLevel);
                if (wall != null)
                {
                    CreatedWalls.Add(wall);
                    transaction.Commit();
                }
                else
                {
                    transaction.RollBack();
                    _failedFacesManager.UpdateError(data.FaceKey, "Retry failed: Unknown error");
                }
            }
            catch (Exception ex)
            {
                _failedFacesManager.UpdateError(data.FaceKey, $"Retry failed: {ex.Message}");
                _logger.Log($"Retry error [{data.FaceKey}]: {ex.Message}", LogLevel.Error);
            }
        });

        transactionGroup.Assimilate();
    }

    private void LogErrorDetails(Curve curve, ConstructionSurfaceModel face, Exception ex)
    {
        var logMessage = $"Face Error [{face.FaceId}]:\n" +
                         $"Curve Type: {curve?.GetType().Name ?? "N/A"}\n" +
                         $"Length: {curve?.Length.ToString("F2") ?? "N/A"} m\n" +
                         $"Error: {ex.Message}\n" +
                         $"StackTrace: {ex.StackTrace}";

        _logger.Log(logMessage, LogLevel.Error);
    }

    private static string GetRoomKey(Room room) => $"{room.LevelId}_{room.Number}";

    private static void ValidateInput(string direction, Level level)
    {
        if (string.IsNullOrWhiteSpace(direction))
            throw new ArgumentException("Direction cannot be empty", nameof(direction));

        if (level == null || !level.IsValidObject)
            throw new ArgumentException("Invalid level", nameof(level));
    }
}
