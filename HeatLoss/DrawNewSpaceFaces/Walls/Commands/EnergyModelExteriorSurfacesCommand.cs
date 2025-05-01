using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Util;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Analysis;
using Autodesk.Revit.UI;
using HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls.RayCasting;
using HVACLoadTerminals.Utils;

namespace HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls.Commands;


[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public class EnergyModelExteriorSurfacesCommand : IExternalCommand
{
    private readonly LoggingService _logger = new();
    private readonly EnergyModelService _energyModelService;
    private readonly WallCreationService _wallCreationService;

    public EnergyModelExteriorSurfacesCommand()
    {
        _energyModelService = new EnergyModelService(_logger);
        _wallCreationService = new WallCreationService(_logger);
    }

    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        _logger.Log("=== Запуск команды создания стен ===");
        var doc = commandData.Application.ActiveUIDocument.Document;

        try
        {
            var energyModel = _energyModelService.GetEnergyModel(doc);
            if (energyModel == null) return HandleError("Энергетическая модель не найдена");

            using var tx = new Transaction(doc, "Create Exterior Walls");
            tx.Start();
            
            var result = _wallCreationService.CreateWallsFromEnergyModel(doc, energyModel);
            
            tx.Commit();
            return result;
        }
        catch (Exception ex)
        {
            _logger.Log($"Критическая ошибка: {ex}");
            message = ex.ToString();
            return Result.Failed;
        }
    }

    private Result HandleError(string message)
    {
        _logger.Log($"Ошибка: {message}");
        TaskDialog.Show("Ошибка", message);
        return Result.Failed;
    }
}

public class EnergyModelService
{
    private readonly ILogger _logger;

    public EnergyModelService(ILogger logger) => _logger = logger;

    public EnergyAnalysisDetailModel GetEnergyModel(Document doc)
    {
        var model = new FilteredElementCollector(doc)
            .OfClass(typeof(EnergyAnalysisDetailModel))
            .Cast<EnergyAnalysisDetailModel>()
            .FirstOrDefault();

        _logger.Log(model != null 
            ? $"Найдена энергетическая модель: {model.Id}" 
            : "Энергетическая модель не найдена");
        
        return model;
    }
}

public class WallCreationService
{
    private readonly ILogger _logger;
    private readonly SurfaceProcessor _surfaceProcessor;
    private readonly ElementService _elementService;

    public WallCreationService(ILogger logger)
    {
        _logger = logger;
        _surfaceProcessor = new SurfaceProcessor(logger);
        _elementService = new ElementService(logger);
    }

    public Result CreateWallsFromEnergyModel(Document doc, EnergyAnalysisDetailModel energyModel)
    {
        var surfaces = energyModel.GetAnalyticalSurfaces()
            .Where(s => _surfaceProcessor.IsExteriorWall(s))
            .ToList();

        if (!surfaces.Any()) return HandleWarning("Нет внешних стен для обработки");
        
        var wallType = _elementService.GetFirstWallType(doc);
        if (wallType == null) return Result.Failed;

        foreach (var surface in surfaces)
        {
            _surfaceProcessor.ProcessSurface(doc, surface, wallType);
        }
        return Result.Succeeded;
    }

    private Result HandleWarning(string message)
    {
        _logger.Log($"Предупреждение: {message}");
        TaskDialog.Show("Предупреждение", message);
        return Result.Succeeded;
    }
}

public class OpeningCreationService
{
    private readonly ILogger _logger;
    private readonly HashSet<ElementId> _processedOpenings = new();

    public OpeningCreationService(ILogger logger) => _logger = logger;

    public void CreateOpenings(Document doc, EnergyAnalysisSurface surface, Level level, List<BoundingBoxXYZ> wallsGeometry)
    {
        try
        {
            _logger.Log("Запуск создания проемов...");
            var openings = surface.GetAnalyticalOpenings()?.ToList() ?? new List<EnergyAnalysisOpening>();
            _logger.Log($"Обнаружено {openings.Count} проемов");

            foreach (var opening in openings)
            {
                if (opening == null || _processedOpenings.Contains(opening.Id))
                {
                    _logger.Log($"Пропуск проема {opening?.Id}");
                    continue;
                }
                
                _processedOpenings.Add(opening.Id);
                _logger.Log($"Обработка проема {opening.Id}");
                ProcessSingleOpening(doc, opening, level, wallsGeometry);
            }
        }
        catch (Exception ex)
        {
            _logger.Log($"Ошибка создания проемов: {ex.Message}");
        }
    }

   private void ProcessSingleOpening(
        Document doc,
        EnergyAnalysisOpening opening,
        Level level,
        List<BoundingBoxXYZ> wallsGeometry)
    {
        try
        {
            // Проверка входных параметров
            if (opening == null || level == null) //|| wallsGeometry == null)
            {
                _logger.Log("Обнаружены null-параметры");
                return;
            }

            // Логирование параметров уровня
            _logger.Log($"Уровень: {level.Name} (Высота: {level.Elevation:F3})");

            // 1. Проверка типа проема
            if (opening.OpeningType != EnergyAnalysisOpeningType.Window && 
                opening.OpeningType != EnergyAnalysisOpeningType.Door)
            {
                _logger.Log($"Пропущен проем типа {opening.OpeningType}");
                return;
            }

            // 2. Получение геометрии проема
            var polyloops = opening.GetPolyloops()?.ToList() ?? new List<Polyloop>();
            if (polyloops.Count == 0)
            {
                _logger.Log("Нет полилопов");
                return;
            }

            // 3. Извлечение и валидация точек
            var allPoints = polyloops
                .SelectMany(p => p?.GetPoints() ?? new List<XYZ>())
                .Where(p => p != null)
                .ToList();

            _logger.Log($"Найдено {allPoints.Count} точек проема");
            if (allPoints.Count < 3)
            {
                _logger.Log("Недостаточно точек");
                return;
            }

            // 4. Расчет параметров с логированием
            var (location, width, height) = CalculateOpeningParameters(allPoints);
            _logger.Log($"Расчетные параметры: " +
                       $"Location=[X:{location?.X:F2}, Y:{location?.Y:F2}, Z:{location?.Z:F2}], " +
                       $"Width={width:F2}, Height={height:F2}");

            if (location == null || width <= 0.01 || height <= 0.01)
            {
                _logger.Log($"Некорректные параметры: {GetInvalidReason(location, width, height)}");
                return;
            }

            // 5. Проверка принадлежности к стене
            /*if (!IsPointInsideWalls(location, wallsGeometry))
            {
                _logger.Log($"Проем вне стен. Координаты: {location}");
                return;
            }*/

            // 6. Выбор символа
            var symbols = opening.OpeningType == EnergyAnalysisOpeningType.Window 
                ? CollectorQuery.GetAllWindowsFamilySymbols(doc) 
                : CollectorQuery.GetAllDoorsFamilySymbols(doc);
            
            var symbol = GetActivatedSymbol(doc, symbols);
            _logger.Log($"Выбранный символ: {symbol?.Name ?? "Не найден"}");

            if (symbol == null) return;

            // 7. Создание экземпляра

            try
            {
                var instance = doc.Create.NewFamilyInstance(
                    location,
                    symbol,
                    level,
                    Autodesk.Revit.DB.Structure.StructuralType.NonStructural
                );
                
                //SetInstanceParameters(instance, width, height);
                _logger.Log($"Успешно создан проем {instance.Id}");
            }
            catch (Exception ex)
            {
                _logger.Log($"Ошибка создания проема: {ex.Message}");
            }
        }
        catch (NullReferenceException nre)
        {
            _logger.Log($"NullReference: {nre.TargetSite?.Name}");
        }
        catch (Exception ex)
        {
            _logger.Log($"Ошибка обработки проема: {ex.Message}");
        }
    }
   
    private string GetInvalidReason(XYZ location, double width, double height)
    {
        var reasons = new List<string>();
        if (location == null) reasons.Add("location is null");
        if (width <= 0.01) reasons.Add($"width={width:F2}");
        if (height <= 0.01) reasons.Add($"height={height:F2}");
        return string.Join(", ", reasons);
    }

    private (XYZ location, double width, double height) CalculateOpeningParameters(List<XYZ> points)
    {
        try
        {
            if (points == null || points.Count < 3)
            {
                _logger.Log("Недостаточно точек для расчета");
                return (null, 0, 0);
            }

            // Логирование координат
            _logger.Log("Координаты точек проема:");
            foreach (var point in points)
            {
                _logger.Log($"X:{point.X:F2}, Y:{point.Y:F2}, Z:{point.Z:F2}");
            }

            var minX = points.Min(p => p.X);
            var maxX = points.Max(p => p.X);
            var minZ = points.Min(p => p.Z);
            var maxZ = points.Max(p => p.Z);

            var location = new XYZ(
                (minX + maxX) / 2,
                points.Average(p => p.Y),
                (minZ + maxZ) / 2
            );

            return (location, maxX - minX, maxZ - minZ);
        }
        catch (Exception ex)
        {
            _logger.Log($"Ошибка расчета параметров: {ex.Message}");
            return (null, 0, 0);
        }
    }

    private bool IsPointInsideWalls(XYZ point, List<BoundingBoxXYZ> walls)
    {
        return walls.Any(bbox =>
            point.X > bbox.Min.X && point.X < bbox.Max.X &&
            point.Y > bbox.Min.Y && point.Y < bbox.Max.Y &&
            point.Z > bbox.Min.Z && point.Z < bbox.Max.Z
        );
    }

    private FamilySymbol GetActivatedSymbol(Document doc, IEnumerable<Element> symbols)
    {
        var symbol = symbols
            .Cast<FamilySymbol>()
            .FirstOrDefault(s => s.IsActive);
        if (symbol != null) return symbol;

        symbol = symbols.Cast<FamilySymbol>().FirstOrDefault();
        if (symbol == null) return null;

        using var t = new SubTransaction(doc);
        t.Start();
        symbol.Activate();
        t.Commit();
        return symbol;
    }

    private void SetInstanceParameters(FamilyInstance instance, double width, double height)
    {
        instance.LookupParameter("Width")?.Set(width);
        instance.LookupParameter("Height")?.Set(height);
        instance.LookupParameter("Default Width")?.Set(width);
        instance.LookupParameter("Default Height")?.Set(height);
    }
}

public class SurfaceProcessor
{
    private readonly ILogger _logger;
    private readonly GeometryHelper _geometryHelper;

    public SurfaceProcessor(ILogger logger)
    {
        _logger = logger;
        _geometryHelper = new GeometryHelper(logger);
    }

    public bool IsExteriorWall(EnergyAnalysisSurface surface)
    {
        try
        {
            var surfaceType = surface.SurfaceType.ToString();
            return surfaceType is "ExteriorWall" or "UndergroundWall";
        }
        catch
        {
            _logger.Log($"Ошибка определения типа поверхности {surface.Id}");
            return false;
        }
    }

    public void ProcessSurface(
        Document doc, 
        EnergyAnalysisSurface surface, 
        WallType wallType)
    {
        var logger = new LoggingService();
        var levelService = new LevelService(logger);
        
        try
        {
            Level level = levelService.GetSurfaceLevel(doc, surface);
            if (level == null)
            {
                logger.Log("Не удалось определить уровень");
                return;
            }

            foreach (var polyloop in surface.GetPolyloops())
            {
                using var wallTx = new SubTransaction(doc);
                wallTx.Start();
                
                try
                {
                    var wall = new GeometryHelper(logger)
                        .CreateWallFromPolyloop(doc, polyloop, level, wallType);

                    if (wall?.Id == null) continue;
                    logger.Log($"Создана стена {wall.Id}");
                    wallTx.Commit();
                    var bbox = wall.get_BoundingBox(null);
                    if (bbox == null)
                    {
                        logger.Log("Не удалось получить BoundingBox стены");
                        //continue;
                    }
                    // Создание проемов только для текущей стены
                    new OpeningCreationService(logger)
                        .CreateOpenings(doc, surface, level, new List<BoundingBoxXYZ> { bbox });
                }
                catch (Exception ex)
                {
                    wallTx.RollBack();
                    logger.Log($"Ошибка создания стены: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            logger.Log($"Критическая ошибка: {ex}");
        }
    }
}

public class ElementService
{
    private readonly ILogger _logger;

    public ElementService(ILogger logger) => _logger = logger;

    public WallType GetFirstWallType(Document doc)
    {
        var wallType = new FilteredElementCollector(doc)
            .OfClass(typeof(WallType))
            .Cast<WallType>()
            .FirstOrDefault();

        _logger.Log(wallType != null 
            ? $"Найден тип стены: {wallType.Name}" 
            : "Типы стен не найдены");
        
        return wallType;
    }
}

public class LevelService
{
    private readonly ILogger _logger;

    public LevelService(ILogger logger) => _logger = logger;

    public Level GetSurfaceLevel(Document doc, EnergyAnalysisSurface surface)
    {
        var space = surface.GetAnalyticalSpace();
        if (space == null)
        {
            _logger.Log("Пространство не найдено");
            return GetDefaultLevel(doc);
        }

        // Получаем параметр "Этаж" через BuiltInParameter
        Parameter levelParam = space.get_Parameter(
            BuiltInParameter.SPACE_REFERENCE_LEVEL_PARAM
        );

        if (levelParam == null || levelParam.StorageType != StorageType.ElementId)
        {
            _logger.Log("Параметр уровня не найден");
            return GetDefaultLevel(doc);
        }

        ElementId levelId = levelParam.AsElementId();
        if (levelId == ElementId.InvalidElementId)
        {
            _logger.Log("ID уровня недействителен");
            return GetDefaultLevel(doc);
        }

        Level level = doc.GetElement(levelId) as Level;
        if (level != null) return level;

        _logger.Log($"Уровень с ID {levelId} не найден");
        return GetDefaultLevel(doc);
    }

    private Level GetDefaultLevel(Document doc)
    {
        return new FilteredElementCollector(doc)
            .OfClass(typeof(Level))
            .Cast<Level>()
            .OrderBy(l => l.Elevation)
            .FirstOrDefault(); // Берем самый нижний уровень
    }
}

public class GeometryHelper
{
    private readonly ILogger _logger;
    private readonly ProfileValidator _validator;
    private readonly SurfaceNormalCalculator _normalCalculator;

    public GeometryHelper(ILogger logger)
    {
        _logger = logger;
        _validator = new ProfileValidator(logger);
        _normalCalculator = new SurfaceNormalCalculator();
    }

    public Wall CreateWallFromPolyloop(
        Document doc, 
        Polyloop polyloop, 
        Level level, 
        WallType wallType)
    {
        try
        {
            // Проверка входных параметров
            if (doc == null || polyloop == null || level == null || wallType?.Id == null)
            {
                _logger.Log("Некорректные параметры для создания стены");
                return null;
            }

            // Получение точек полилопа
            IList<XYZ> points = polyloop.GetPoints();
            if (points == null || points.Count < 3)
            {
                _logger.Log("Недостаточно точек для создания стены");
                return null;
            }

            // Создание кривых
            List<Curve> curves = CreateCurvesFromPoints(points);
            if (curves.Count < 3 || !_validator.IsValidProfile(curves))
            {
                _logger.Log("Профиль стены невалиден");
                return null;
            }

            // Вычисление нормали
            XYZ normal = _normalCalculator.ComputeNormal(points);
            if (normal == null || normal.IsZeroLength())
            {
                _logger.Log("Не удалось вычислить нормаль");
                return null;
            }

            // Создание стены
            return Wall.Create(doc, curves, wallType.Id, level.Id, true, normal);
        }
        catch (Exception ex)
        {
            _logger.Log($"Ошибка создания стены: {ex.Message}");
            return null;
        }
    }

    private List<Curve> CreateCurvesFromPoints(IList<XYZ> points)
    {
        var curves = new List<Curve>();
        for (int i = 0; i < points.Count; i++)
        {
            XYZ start = points[i];
            XYZ end = points[(i + 1) % points.Count];
            curves.Add(Line.CreateBound(start, end));
        }
        return curves;
    }
}

public class ProfileValidator
{
    private readonly ILogger _logger;

    public ProfileValidator(ILogger logger) => _logger = logger;

    public bool IsValidProfile(List<Curve> curves)
    {
        if (curves.Count < 3)
        {
            _logger.Log("Недостаточно кривых для профиля");
            return false;
        }
        return IsClosed(curves);
    }

    private bool IsClosed(List<Curve> curves)
    {
        var first = curves.First().GetEndPoint(0);
        var last = curves.Last().GetEndPoint(1);
        return first.DistanceTo(last) < 0.001;
    }
}

public class SurfaceNormalCalculator
{
    public XYZ ComputeNormal(IList<XYZ> points)
    {
        if (points == null || points.Count < 3) return null;

        try
        {
            XYZ vector1 = points[1] - points[0];
            XYZ vector2 = points[2] - points[0];
            XYZ normal = vector1.CrossProduct(vector2).Normalize();
            return normal.IsZeroLength() ? null : normal;
        }
        catch
        {
            return null;
        }
    }
}