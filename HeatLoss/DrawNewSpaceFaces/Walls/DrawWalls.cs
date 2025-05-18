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
    private readonly List<Room> _cachedRooms;
    public readonly List<Space> CachedSpaces;
    public readonly FailedFacesManager FailedFacesManager;
    private readonly Document _hvacDocument;
    private readonly ILogger _logger;
    private readonly Document _roomDocument;
    private readonly Dictionary<string, Room> _roomKeyCache = new();
    private readonly Dictionary<ElementId, string> _spaceRoomKeyMap = new();

    public DrawWalls(Document hvacDocument, Document roomDocument)
    {
        _hvacDocument = hvacDocument;
        _roomDocument = roomDocument;
        _logger = new LoggingService("DrawWalls.txt");
        FailedFacesManager = new FailedFacesManager();
        ParametersHandler.GetProjectInformation(hvacDocument, nameof(ClimateDataModel.TWinterOut092));

        _cachedRooms = CollectorQuery.GetAllRooms(roomDocument);
        CachedSpaces = CollectorQuery.GetAllSpaces(hvacDocument).Cast<Space>().ToList();

        InitializeCaches();
    }

    public List<Wall> CreatedWalls { get; } = [];
    public bool IsReady => _hvacDocument != null && _roomDocument?.IsValidObject == true;
    public List<string> FailedFaceKeys => FailedFacesManager.FailedFaceKeys;

    private void InitializeCaches()
    {
        // Заполнение кэша помещений по ключу LevelId + Number
        foreach (var room in _cachedRooms)
        {
            var key = $"{room.LevelId}_{room.Number}";
            if (!_roomKeyCache.ContainsKey(key))
                _roomKeyCache.Add(key, room);
        }

        // Заполнение _spaceRoomKeyMap с новой логикой связывания
        foreach (var space in CachedSpaces.Where(s => s.Location is LocationPoint))
        {
            var linkedRoom = FindLinkedRoom(space);
            if (linkedRoom != null)
            {
                _spaceRoomKeyMap[space.Id] = GetRoomKey(linkedRoom);
            }
        }
    }
    private Room FindLinkedRoom(Space space)
    {
        // Этап 1: Поиск через приподнятую точку (Z + 5)
        if (space.Location is LocationPoint location)
        {
            var elevatedPoint = new XYZ(location.Point.X, location.Point.Y, location.Point.Z + 5);
            var room = _cachedRooms.FirstOrDefault(r => r.IsPointInRoom(elevatedPoint));
            if (room != null) return room;
        }

        var roomByNumber = _cachedRooms.FirstOrDefault(r => r.Number == space.Number);
        return roomByNumber;
    }

    public void CreateWallsForSpaces(string northDirection, Level groundLevel, HashSet<ElementId> filter = null)
    {
        ValidateInput(northDirection, groundLevel);

        using var transactionGroup = new TransactionGroup(_hvacDocument, "Create Walls");
        transactionGroup.Start();

        foreach (var space in CachedSpaces) ProcessSpaceWalls(space, northDirection, groundLevel, filter);

        transactionGroup.Assimilate();
        FailedFacesManager.LogFailedOperations();
    }

    private void ProcessSpaceWalls(Space space, string northDirection, Level groundLevel, HashSet<ElementId> filter)
    {
        if (!_spaceRoomKeyMap.TryGetValue(space.Id, out var roomKey))
        {
            _logger.Log($"Space id -{space.Id} Number - {space.Number}: No linked room found", LogLevel.Warning);
            return;
        }

        if (!_roomKeyCache.TryGetValue(roomKey, out var room))
        {
            _logger.Log($"Room {roomKey} not found in cache", LogLevel.Error);
            return;
        }

        var faces = VerticalWallFacesCalculator
            .GetExternalFaces(_roomDocument, room, filter);
        foreach (var face in faces) ProcessSingleFace(space, face, northDirection, groundLevel);
    }

    private void ProcessSingleFace(Space space, ConstructionSurfaceModel faceModel, string north, Level groundLevel)
    {
        if (faceModel?._Face == null)
        {
            _logger.Log($"Invalid face model faceModel?._Face == null for space {space.Id}", LogLevel.Error);
            return;
        }

        var faceKey = $"{space.Id}_{faceModel.FaceId}";
        Curve curve = null;
        bool useDirectShape = false;

        try
        {
            curve = GetFaceCurveWithValidation(faceModel._Face);
            if (curve == null)
            {
                curve = GetFallbackCurve(faceModel._Face); // Резервный метод
                if (curve == null)
                {
                    FailedFacesManager.RegisterFailure(faceKey, space, faceModel, null,
                        "Invalid face geometry curve == null");
                   // CreateDirectShapeFromFace(space, faceModel._Face, faceKey);
                }
            }

            using var transaction = new Transaction(_hvacDocument, $"Create Wall {faceKey}");
            transaction.Start();

            var wall = CreateWallFromCurve(space, curve, faceModel, north, groundLevel);
            if (wall != null)
            {
                CreatedWalls.Add(wall);
                transaction.Commit();
                FailedFacesManager.RemoveFace(faceKey);
            }
            else
            {
                transaction.RollBack();
                useDirectShape = true;
            }
        }
        catch (Exception ex)
        {
            if (useDirectShape)
            {
                CreateDirectShapeFromFace(space, faceModel._Face, faceKey);
            }
            else
            {
                FailedFacesManager.RegisterFailure(faceKey, space, faceModel, curve, ex.Message);
                LogErrorDetails(curve, faceModel, ex);
            }
        }
    }
    

    private void CreateDirectShapeFromFace(Space space, Face face, string faceKey)
{
    // Убедиться, что транзакция создается и управляется правильно
    using var transaction = new Transaction(_hvacDocument, $"Create DirectShape from {faceKey}");
    try
    {
        transaction.Start();
        _logger.Log($"Попытка построить DirectShape для space {space.Number} - {faceKey}");

        var ds = Autodesk.Revit.DB.DirectShape.CreateElement(_hvacDocument, new ElementId(BuiltInCategory.OST_GenericModel));
        ds.ApplicationId = "FallbackWall";
        ds.ApplicationDataId = $"FailedWall_{faceKey}";

        Mesh mesh = face.Triangulate();
        if (mesh == null || mesh.NumTriangles < 1)
        {
            _logger.Log($"Invalid mesh data for {faceKey}", LogLevel.Warning);
            transaction.RollBack();
            return;
        }

        var tsb = new TessellatedShapeBuilder
        {
            Target = TessellatedShapeBuilderTarget.Mesh,
            Fallback = TessellatedShapeBuilderFallback.Salvage
        };

        tsb.OpenConnectedFaceSet(true);
        for (int i = 0; i < mesh.NumTriangles; i++)
        {
            MeshTriangle triangle = mesh.get_Triangle(i);
            var vertices = new List<XYZ> { triangle.get_Vertex(0), triangle.get_Vertex(1), triangle.get_Vertex(2) };
            tsb.AddFace(new TessellatedFace(vertices, ElementId.InvalidElementId));
        }
        tsb.CloseConnectedFaceSet();

        tsb.Build();
        var result = tsb.GetBuildResult();

        if (IsBuildSuccessful(result.Outcome))
        {
            ds.SetShape(result.GetGeometricalObjects());
            ds.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.Set($"Fallback wall for {faceKey}");
            transaction.Commit();
            _logger.Log($"Created DirectShape for {faceKey}", LogLevel.Info);
        }
        else
        {
            _logger.Log($"Build failed: {result.Outcome}", LogLevel.Error);
            transaction.RollBack();
        }
    }
    catch (Exception ex)
    {
        transaction.RollBack();
        _logger.Log($"Error creating DirectShape: {ex.Message}", LogLevel.Error);
    }
}

    private static bool IsBuildSuccessful(TessellatedShapeBuilderOutcome outcome)
{
    // Для старых версий (2018-2019)
    return outcome.ToString() == "Success";
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
            if (curveLoops == null || curveLoops.Count == 0)
            {
                _logger.Log("Face has no valid curve loops", LogLevel.Warning);
                return TryGetWallBoundary(face);
            }

            // Выбираем первый контур с допустимой кривой (например, Line или Arc)
            foreach (var loop in curveLoops)
            {
                foreach (var curve in loop)
                {
                    if (curve is Line || curve is Arc)
                    {
                        return curve;
                    }
                }
            }

            _logger.Log("No valid curves found in face loops", LogLevel.Warning);
            return TryGetWallBoundary(face);
        }
        catch (Exception ex)
        {
            _logger.Log($"Error processing face: {ex.Message}", LogLevel.Error);
            return null;
        }
    }
    

    private Curve TryGetWallBoundary(Face face)
    {
        var element = _roomDocument.GetElement(face.Reference.ElementId);
        if (element is not Wall wall) return null;

        var locationCurve = wall.Location as LocationCurve;
        if (locationCurve?.Curve == null)
        {
            _logger.Log($"Wall {wall.Id} has no valid location curve", LogLevel.Warning);
            return null;
        }

        // Проверяем ориентацию стены
        var curve = locationCurve.Curve;
        var normal = face.ComputeNormal(UV.Zero);
        if (normal.IsAlmostEqualTo(XYZ.BasisZ)) return curve; // Убедимся, что грань вертикальна 
        _logger.Log($"Face normal is not vertical for wall {wall.Id}", LogLevel.Warning);
        return null;

    }

    
    private Curve GetFallbackCurve(Face face)
{
    _logger.Log("Создание BoundingBoxUV из Face");
    if (face == null)
    {
        _logger.Log("Некорректная грань (null или невалидный объект)", LogLevel.Error);
        return null;
    }

    try
    {
        // 1. Получаем BoundingBoxUV
        BoundingBoxUV bboxUV = face.GetSurface().GetBoundingBoxUV();
        if (bboxUV == null)
        {
            _logger.Log("BoundingBoxUV не получен", LogLevel.Warning);
            return null;
        }

        // Логирование UV-координат
        _logger.Log($"BoundingBoxUV: Min({bboxUV.Min.U}, {bboxUV.Min.V}), Max({bboxUV.Max.U}, {bboxUV.Max.V})");

        // 2. Проверка UV-координат
        UV centerUV = (bboxUV.Min + bboxUV.Max) * 0.5;
        if (double.IsNaN(centerUV.U))
        {
            _logger.Log($"Некорректные UV-координаты центра: {centerUV}", LogLevel.Error);
            return null;
        }

        // 3. Получаем нормаль грани для проверки вертикальности
        XYZ normal;
        try
        {
            normal = face.ComputeNormal(centerUV);
            _logger.Log($"Нормаль грани: {normal}");
        }
        catch (Exception ex)
        {
            _logger.Log($"Ошибка вычисления нормали: {ex.Message}", LogLevel.Warning);
            return null;
        }

        if (!normal.IsAlmostEqualTo(XYZ.BasisZ, 0.1))
        {
            _logger.Log($"Грань не вертикальна (нормаль: {normal})", LogLevel.Warning);
            return null;
        }

        // 4. Получаем крайние точки через триангуляцию
        Mesh mesh = face.Triangulate();
        if (mesh == null || mesh.Vertices.Count < 3)
        {
            _logger.Log($"Ошибка триангуляции: вершин - {mesh?.Vertices.Count ?? 0}", LogLevel.Warning);
            return null;
        }

        // Логирование вершин меша
        _logger.Log($"Вершин в меше: {mesh.Vertices.Count}");
        for (int i = 0; i < Math.Min(3, mesh.Vertices.Count); i++)
        {
            _logger.Log($"Вершина {i}: {mesh.Vertices[i]}");
        }

        // 5. Создаем линию по первой и последней вершине меша
        XYZ start = mesh.Vertices[0];
        XYZ end = mesh.Vertices[mesh.Vertices.Count - 1];

        // Логирование координат точек
        _logger.Log($"Попытка создать линию: Start({start.X}, {start.Y}, {start.Z}), End({end.X}, {end.Y}, {end.Z})");

        // Проверка валидности точек
        if (start.IsAlmostEqualTo(end))
        {
            _logger.Log("Точки совпадают", LogLevel.Error);
            return null;
        }

        // 6. Создание и проверка линии
        Line line = null;
        try
        {
            line = Line.CreateBound(start, end);
            if (line == null)
            {
                _logger.Log("Не удалось создать линию (возвращено null)", LogLevel.Error);
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.Log($"Ошибка создания линии: {ex.Message}", LogLevel.Error);
            return null;
        }

        // Проверка длины линии
        if (line.Length < 0.01)
        {
            _logger.Log($"Кривая слишком короткая: {line.Length}", LogLevel.Warning);
            return null;
        }

        _logger.Log($"Успешно создана линия длиной {line.Length}");
        return line;
    }
    catch (Exception ex)
    {
        _logger.Log($"Критическая ошибка: {ex.Message}\n{ex.StackTrace}", LogLevel.Error);
        return null;
    }
}

    
    private Wall CreateWallFromCurve(Space space, Curve curve, ConstructionSurfaceModel face, string north, Level groundLevel)
    {
        if (space.Level == null)
        {
            _logger.Log($"Space {space.Id} has no level", LogLevel.Error);
            return null;
        }

        // Проверка валидности кривой
        if (curve == null || curve.IsBound == false || curve.Length < 0.01)
        {
            _logger.Log($"Invalid curve for space {space.Id}", LogLevel.Error);
            return null;
        }

        try
        {
            var wall = Wall.Create(_hvacDocument, curve, space.Level.Id, false);
            if (wall == null) return null;

            ConfigureWallParameters(wall, space, face, north, groundLevel, curve);
            return wall;
        }
        catch (Exception ex)
        {
            _logger.Log($"Failed to create wall: {ex.Message}", LogLevel.Error);
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

        FailedFacesManager.RetryFailedFaces(data =>
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
                    FailedFacesManager.UpdateError(data.FaceKey, "Retry failed: Unknown error");
                }
            }
            catch (Exception ex)
            {
                FailedFacesManager.UpdateError(data.FaceKey, $"Retry failed: {ex.Message}");
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

    private static string GetRoomKey(Room room)
    {
        return $"{room.LevelId}_{room.Number}";
    }

    private static void ValidateInput(string direction, Level level)
    {
        if (string.IsNullOrWhiteSpace(direction))
            throw new ArgumentException("Direction cannot be empty", nameof(direction));

        if (level == null || !level.IsValidObject)
            throw new ArgumentException("Invalid level", nameof(level));
    }
}