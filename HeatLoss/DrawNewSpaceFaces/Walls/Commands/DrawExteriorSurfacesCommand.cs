using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Analysis;
using Autodesk.Revit.UI;
using HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls.RayCasting;

namespace HVACLoadTerminals.HeatLoss.Commands;

[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public class DrawExteriorSurfacesCommand : IExternalCommand
{
    private LoggingService _logger = new();

    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        _logger.Log("=== Запуск команды создания стен ===");

        UIApplication uiApp = commandData.Application;
        UIDocument uiDoc = uiApp.ActiveUIDocument;
        Document doc = uiDoc.Document;

        try
        {
            _logger.Log("Поиск энергетической модели...");
            EnergyAnalysisDetailModel energyModel = GetEnergyModel(doc);

            if (energyModel == null)
            {
                _logger.Log("Ошибка: Энергетическая модель не найдена");
                TaskDialog.Show("Ошибка", "Энергетическая модель не найдена");
                return Result.Failed;
            }

            _logger.Log($"Найдена энергетическая модель: {energyModel.Id}");

            using (Transaction tx = new Transaction(doc, "Create Exterior Walls"))
            {
                tx.Start();
                _logger.Log("Транзакция начата");

                _logger.Log("Поиск типа стены...");
                WallType wallType = GetFirstWallType(doc);
                if (wallType == null)
                {
                    _logger.Log("Ошибка: Не найден тип стены");
                    return Result.Failed;
                }

                _logger.Log($"Используется тип стены: {wallType.Name} ({wallType.Id})");

                _logger.Log("Получение поверхностей...");
                var surfaces = energyModel.GetAnalyticalSurfaces()
                    .Where(IsExteriorWall)
                    .ToList();

                _logger.Log($"Найдено поверхностей: {surfaces.Count}");
                if (surfaces.Count == 0)
                {
                    _logger.Log("Предупреждение: Нет внешних стен для обработки");
                }

                foreach (var surface in surfaces)
                {
                    _logger.Log($"Обработка поверхности {surface.Id}:");
                    CreateWallsFromSurface(doc, surface, wallType);
                }

                tx.Commit();
                _logger.Log("Транзакция завершена успешно");
            }

            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            _logger.Log($"Критическая ошибка: {ex}");
            message = ex.ToString();
            return Result.Failed;
        }
    }

    private EnergyAnalysisDetailModel GetEnergyModel(Document doc)
    {
        var models = new FilteredElementCollector(doc)
            .OfClass(typeof(EnergyAnalysisDetailModel))
            .Cast<EnergyAnalysisDetailModel>()
            .ToList();

        _logger.Log($"Найдено энергетических моделей: {models.Count}");
        return models.FirstOrDefault();
    }

    private bool IsExteriorWall(EnergyAnalysisSurface surface)
    {
        try
        {
            var surfaceTypeValue = surface.SurfaceType.ToString();
            _logger.Log($"Поверхность {surface.Id}: тип = {surfaceTypeValue}");
            return surfaceTypeValue.Equals("ExteriorWall", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _logger.Log($"Ошибка определения типа поверхности {surface.Id}: {ex}");
            return false;
        }
    }

    private void CreateWallsFromSurface(Document doc, EnergyAnalysisSurface surface, WallType wallType)
    {
        try
        {
            _logger.Log($"Получение уровня для поверхности {surface.Id}");
            Level level = GetSurfaceLevel(doc, surface);
            _logger.Log($"Уровень: {level?.Name ?? "Не определен"}");

            var polyloops = surface.GetPolyloops().ToList();
            _logger.Log($"Найдено полилопов: {polyloops.Count}");

            foreach (Polyloop polyloop in polyloops)
            {
                _logger.Log($"Обработка полилопа с {polyloop.GetPoints().ToList().Count()} точками");
                CreateWallFromPolyloop(doc, polyloop, level, wallType);
            }
        }
        catch (Exception ex)
        {
            _logger.Log($"Ошибка поверхности {surface.Id}: {ex}");
            TaskDialog.Show("Ошибка поверхности", ex.Message);
        }
    }

    private Level GetSurfaceLevel(Document doc, EnergyAnalysisSurface surface)
    {
        var space = surface.GetAnalyticalSpace();
        if (space == null)
        {
            _logger.Log("Пространство не найдено");
            return GetDefaultLevel(doc);
        }

        var levelId = space.LevelId;
        if (levelId == null)
        {
            _logger.Log("Параметр уровня не найден");
            return GetDefaultLevel(doc);
        }

        _logger.Log($"ID уровня: {levelId}");

        return doc.GetElement(levelId) as Level ?? GetDefaultLevel(doc);
    }

    private Level GetDefaultLevel(Document doc)
    {
        var levels = new FilteredElementCollector(doc)
            .OfClass(typeof(Level))
            .Cast<Level>()
            .ToList();

        _logger.Log($"Найдено уровней: {levels.Count}");
        return levels.FirstOrDefault();
    }

    private WallType GetFirstWallType(Document doc)
    {
        var wallTypes = new FilteredElementCollector(doc)
            .OfClass(typeof(WallType))
            .Cast<WallType>()
            .ToList();

        _logger.Log($"Найдено типов стен: {wallTypes.Count}");
        return wallTypes.FirstOrDefault();
    }

    private void CreateWallFromPolyloop(Document doc, Polyloop polyloop, Level level, WallType wallType)
    {
        try
        {
            List<Curve> curves = new List<Curve>();
            IList<XYZ> points = polyloop.GetPoints();

            // Проверка минимального количества точек
            if (points.Count < 3)
            {
                _logger.Log("Ошибка: Полилоп имеет менее 3 точек");
                return;
            }

            // Вычисление нормали поверхности
            XYZ normal = ComputeSurfaceNormal(points);
            if (normal == null)
            {
                _logger.Log("Ошибка: Не удалось вычислить нормаль поверхности");
                return;
            }

            _logger.Log($"Вычисленная нормаль: {normal}");

            // Создание кривых для стены
            for (int i = 0; i < points.Count; i++)
            {
                XYZ start = points[i];
                XYZ end = points[(i + 1) % points.Count];
                Line line = Line.CreateBound(start, end);

                // Проверка валидности линии
                if (!line.IsBound)
                {
                    _logger.Log("Ошибка: Некорректная линия в полилопе");
                    return;
                }

                curves.Add(line);
            }

            // Проверка замкнутости профиля
            if (!IsProfileClosed(curves))
            {
                _logger.Log("Ошибка: Профиль стены не замкнут");
                return;
            }

            // Создание стены с вычисленной нормалью
            Wall wall = Wall.Create(doc, curves, wallType.Id, level.Id, true, normal);

            if (wall != null)
            {
                _logger.Log($"Стена успешно создана: {wall.Id}");
            }
            else
            {
                _logger.Log("Ошибка: Не удалось создать стену");
            }
        }
        catch (Exception ex)
        {
            _logger.Log($"Ошибка создания стены: {ex}");
            TaskDialog.Show("Ошибка создания стены", ex.Message);
        }
    }

    private XYZ ComputeSurfaceNormal(IList<XYZ> points)
    {
        try
        {
            // Вычисление нормали через векторное произведение
            if (points.Count < 3) return null;

            XYZ vector1 = points[1] - points[0];
            XYZ vector2 = points[2] - points[0];

            XYZ normal = vector1.CrossProduct(vector2).Normalize();

            // Проверка на нулевую нормаль
            if (normal.IsZeroLength())
            {
                _logger.Log("Ошибка: Нулевая нормаль");
                return null;
            }

            return normal;
        }
        catch (Exception ex)
        {
            _logger.Log($"Ошибка вычисления нормали: {ex}");
            return null;
        }
    }

    private bool IsProfileClosed(List<Curve> curves)
    {
        try
        {
            XYZ firstStart = curves.First().GetEndPoint(0);
            XYZ lastEnd = curves.Last().GetEndPoint(1);

            return firstStart.DistanceTo(lastEnd) < 0.001;
        }
        catch
        {
            return false;
        }
    }
}