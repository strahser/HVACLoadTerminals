using System;
using System.Collections.Generic;
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
      private readonly LoggingService _logger = new LoggingService();

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;

            try
            {
                _logger.Log("Запуск команды");

                // 1. Получение или создание энергетической модели
                EnergyAnalysisDetailModel energyModel = GetExistingEnergyModel(doc);
                _logger.Log(energyModel != null 
                    ? "Найдена существующая энергетическая модель" 
                    : "Энергетическая модель не найдена");

                if (energyModel == null)
                {
                    energyModel = CreateNewEnergyModel(doc);
                    _logger.Log("Создана новая энергетическая модель");
                }

                // 2. Отрисовка элементов
                DrawExteriorElements(doc, uidoc.ActiveView, energyModel);

                _logger.Log("Команда успешно выполнена");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                _logger.Log($"Критическая ошибка: {ex}");
                message = $"Ошибка выполнения: {ex.Message}";
                return Result.Failed;
            }
        }

        private EnergyAnalysisDetailModel GetExistingEnergyModel(Document doc)
        {
            try
            {
                var models = new FilteredElementCollector(doc)
                    .OfClass(typeof(EnergyAnalysisDetailModel))
                    .Cast<EnergyAnalysisDetailModel>()
                    .ToList();

                _logger.Log($"Найдено энергетических моделей: {models.Count}");
                return models.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger.Log($"Ошибка при поиске энергетической модели: {ex}");
                return null;
            }
        }

        private EnergyAnalysisDetailModel CreateNewEnergyModel(Document doc)
        {
            using (Transaction tx = new Transaction(doc, "Create Energy Model"))
            {
                tx.Start();
                try
                {
                    var options = new EnergyAnalysisDetailModelOptions();
                    var energyModel = EnergyAnalysisDetailModel.Create(doc, options);
                    tx.Commit();
                    return energyModel;
                }
                catch (Exception ex)
                {
                    _logger.Log($"Ошибка создания модели: {ex}");
                    tx.RollBack();
                    throw;
                }
            }
        }

        private void DrawExteriorElements(Document doc, View view, EnergyAnalysisDetailModel energyModel)
        {
            using (Transaction tx = new Transaction(doc, "Draw Exterior Elements"))
            {
                tx.Start();
                try
                {
                    var surfaces = energyModel.GetAnalyticalSurfaces();
                    _logger.Log($"Обработка поверхностей: {surfaces.Count} шт.");

                    int exteriorCount = 0;
                    foreach (var surface in surfaces)
                    {
                        if (IsExteriorWall(surface))
                        {
                            exteriorCount++;
                            ProcessSurface(doc, view, surface);
                            ProcessOpenings(doc, view, surface);
                        }
                    }

                    _logger.Log($"Найдено наружных стен: {exteriorCount} шт.");
                    tx.Commit();
                }
                catch (Exception ex)
                {
                    _logger.Log($"Ошибка отрисовки: {ex}");
                    tx.RollBack();
                    throw;
                }
            }
        }

        private bool IsExteriorWall(EnergyAnalysisSurface surface)
        {
            try
            {
                var param = surface.get_Parameter(BuiltInParameter.RBS_GBXML_SURFACE_TYPE);
                if (param == null) return false;

                var value = param.AsValueString();
                // Учитываем локализацию типа поверхности
                return value == "Наружная стена"; // Было "ExteriorWall"
            }
            catch
            {
                return false;
            }
        }

        private void ProcessSurface(Document doc, View view, EnergyAnalysisSurface surface)
        {
            try
            {
                _logger.Log($"Обработка поверхности {surface.Id}");
                var options = new Options { View = view };
                
                var geometry = surface.get_Geometry(options);
                if (geometry == null)
                {
                    _logger.Log($"Геометрия не найдена для поверхности {surface.Id}");
                    return;
                }

                foreach (var geomObj in geometry)
                {
                    ProcessGeometry(doc, view, geomObj);
                }
            }
            catch (Exception ex)
            {
                _logger.Log($"Ошибка обработки поверхности {surface.Id}: {ex}");
            }
        }

        private void ProcessOpenings(Document doc, View view, EnergyAnalysisSurface surface)
        {
            try
            {
                var openings = surface.GetAnalyticalOpenings();
                _logger.Log($"Найдено проемов: {openings.Count} для поверхности {surface.Id}");

                foreach (var opening in openings)
                {
                    var options = new Options { View = view };
                    foreach (var geomObj in opening.get_Geometry(options))
                    {
                        ProcessGeometry(doc, view, geomObj);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Log($"Ошибка обработки проемов: {ex}");
            }
        }

    private void ProcessGeometry(Document doc, View view, GeometryObject geomObj)
    {
        switch (geomObj)
        {
            case Solid solid:
                ProcessSolid(doc, view, solid);
                break;
            case GeometryInstance instance:
                ProcessGeometryInstance(doc, view, instance);
                break;
            case Curve curve:
                CreateDetailCurve(doc, view, curve);
                break;
        }
    }

    private void ProcessGeometryInstance(Document doc, View view, GeometryInstance instance)
    {
        foreach (GeometryObject obj in instance.GetSymbolGeometry())
        {
            ProcessGeometry(doc, view, obj);
        }
    }

    private void ProcessSolid(Document doc, View view, Solid solid)
    {
        foreach (Face face in solid.Faces)
        {
            ProcessFace(doc, view, face);
        }
    }

    private void ProcessFace(Document doc, View view, Face face)
    {
        foreach (EdgeArray loop in face.EdgeLoops)
        {
            List<XYZ> vertices = new List<XYZ>();
            foreach (Edge edge in loop)
            {
                vertices.AddRange(edge.Tessellate());
            }
            CreateContour(doc, view, vertices);
        }
    }

    private void CreateContour(Document doc, View view, IList<XYZ> vertices)
    {
        for (int i = 0; i < vertices.Count; i++)
        {
            XYZ start = vertices[i];
            XYZ end = vertices[(i + 1) % vertices.Count];
                
            if (Line.CreateBound(start, end) is Line line)
            {
                CreateDetailCurve(doc, view, line);
            }
        }
    }

    private void CreateDetailCurve(Document doc, View view, Curve curve)
    {
        try
        {
            doc.Create.NewDetailCurve(view, curve);
        }
        catch (Exception ex)
        {
            TaskDialog.Show("Ошибка создания кривой", ex.Message);
        }
    }
    
    
}