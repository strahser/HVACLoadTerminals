using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Analysis;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI;
using HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls.RayCasting;
using HVACLoadTerminals.Utils;

namespace HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls.Commands;


[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public class _EnergyModelFromBoundariesCommand : IExternalCommand
{
    private LoggingService _logger = new();
    private const double _perpendicularLength = 3.0;
    private Document _doc;

    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        UIApplication uiApp = commandData.Application;
        _doc = uiApp.ActiveUIDocument.Document;

        try
        {
            _logger.Log("=== Начало обработки границ ===");

            var energyModelCurves = GetEnergyModelCurves();
            _logger.Log($"Кривых из энергомодели: {energyModelCurves.Count}");

            var spaceBoundaries = new BoundaryProcessor(_doc).GetAllBoundaryData();
            _logger.Log($"Граничных сегментов: {spaceBoundaries.Count}");

            var matchedCurves = MatchCurves(spaceBoundaries, energyModelCurves);
            _logger.Log($"Найдено совпадений: {matchedCurves.Count}");

            CreateWallsFromCurves(matchedCurves);
            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            _logger.Log($"Ошибка: {ex}");
            message = ex.ToString();
            return Result.Failed;
        }
    }

    private List<Curve> GetEnergyModelCurves()
    {
        var curves = new List<Curve>();
        var energyModel = new FilteredElementCollector(_doc)
            .OfClass(typeof(EnergyAnalysisDetailModel))
            .FirstOrDefault() as EnergyAnalysisDetailModel;

        if (energyModel == null) return curves;

        foreach (EnergyAnalysisSurface surface in energyModel.GetAnalyticalSurfaces())
        {
            if (surface.SurfaceType != EnergyAnalysisSurfaceType.ExteriorWall) continue;

            foreach (Polyloop polyloop in surface.GetPolyloops())
            {
                curves.AddRange(ConvertPolyloopToCurves(polyloop));
            }
        }
        return curves;
    }

    private List<Curve> ConvertPolyloopToCurves(Polyloop polyloop)
    {
        var curves = new List<Curve>();
        var points = polyloop.GetPoints();
        
        for (int i = 0; i < points.Count; i++)
        {
            XYZ start = points[i];
            XYZ end = points[(i + 1) % points.Count];
            
            if (start.DistanceTo(end) > _doc.Application.ShortCurveTolerance)
            {
                curves.Add(Line.CreateBound(start, end));
            }
        }
        return curves;
    }

    private List<Curve> MatchCurves(List<BoundaryData> spaceBoundaries, List<Curve> energyCurves)
    {
        var matched = new HashSet<Curve>();
        
        foreach (var energyCurve in energyCurves)
        {
            var testSegments = GeneratePerpendicularSegments(energyCurve);
            
            foreach (var segment in testSegments)
            {
                foreach (var boundary in spaceBoundaries)
                {
                    if (DoCurvesIntersect(segment, boundary.CurveData))
                    {
                        matched.Add(boundary.CurveData);
                    }
                }
            }
        }
        return matched.ToList();
    }

    private List<Curve> GeneratePerpendicularSegments(Curve baseCurve)
    {
        var segments = new List<Curve>();
        
        if (!(baseCurve is Line line) || 
            line.Length < _doc.Application.ShortCurveTolerance)
        {
            return segments;
        }

        try
        {
            XYZ midpoint = line.Evaluate(0.5, true);
            XYZ direction = line.Direction.Normalize();
            XYZ perpendicular = new XYZ(-direction.Y, direction.X, 0).Normalize();

            double minLength = _doc.Application.ShortCurveTolerance * 2;
            
            if (_perpendicularLength > minLength)
            {
                segments.Add(Line.CreateBound(
                    midpoint, 
                    midpoint + perpendicular * _perpendicularLength
                ));
                
                segments.Add(Line.CreateBound(
                    midpoint, 
                    midpoint - perpendicular * _perpendicularLength
                ));
            }
        }
        catch (Exception ex)
        {
            _logger.Log($"Ошибка в GeneratePerpendicularSegments: {ex.Message}");
        }
        
        return segments;
    }

    private bool DoCurvesIntersect(Curve a, Curve b)
    {
        try
        {
            IntersectionResultArray results;
            return a.Intersect(b, out results) == SetComparisonResult.Overlap;
        }
        catch
        {
            return false;
        }
    }

    private void CreateWallsFromCurves(List<Curve> curves)
    {
        WallType wallType = new FilteredElementCollector(_doc)
            .OfClass(typeof(WallType))
            .Cast<WallType>()
            .FirstOrDefault(wt => wt.Name.Contains("Наружная"));

        Level level = new FilteredElementCollector(_doc)
            .OfClass(typeof(Level))
            .Cast<Level>()
            .FirstOrDefault();

        if (wallType == null || level == null) return;

        using (Transaction tx = new Transaction(_doc, "Создание стен"))
        {
            tx.Start();
            
            foreach (var curve in curves)
            {
                try
                {
                    if (curve.Length < _doc.Application.ShortCurveTolerance) continue;
                    
                    List<Curve> wallCurves = new List<Curve> { curve };
                    XYZ normal = ComputeWallNormal(curve);
                    
                    Wall.Create(_doc, wallCurves, wallType.Id, level.Id, true, normal);
                }
                catch (Exception ex)
                {
                    _logger.Log($"Ошибка создания стены: {ex.Message}");
                }
            }
            tx.Commit();
        }
    }

    private XYZ ComputeWallNormal(Curve curve)
    {
        if (curve is Line line)
        {
            XYZ dir = line.Direction.Normalize();
            return new XYZ(-dir.Y, dir.X, 0).Normalize();
        }
        return XYZ.BasisZ;
    }
}



