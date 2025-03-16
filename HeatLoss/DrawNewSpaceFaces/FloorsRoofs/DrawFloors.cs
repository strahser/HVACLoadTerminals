using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI;
using HVACLoadTerminals.CalculateSpaceDevice;
using HVACLoadTerminals.ModelsStatic;
using HVACLoadTerminals.Utils;

namespace HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.FloorsRoofs
{
    public abstract class DrawFloors
    {  
        public static void DrawFloorsForSelectedSpaces(Document hvacDocument,List<Space> spaces,Level level, 
            FloorType floorType,string enclosureType)
        {

            foreach (var space in spaces.Cast<Space>())
            {
                var spaceBoundary = new SpaceBoundaryCurve(space);
                var curves = spaceBoundary.GetCurves();
                var curveLoop = new CurveLoop();
                foreach (var curve in curves)
                {
                    curveLoop.Append(curve);
                }

                IList<CurveLoop> curveLoops = new List<CurveLoop>() { curveLoop };

                if (floorType == null)
                {
                    TaskDialog.Show("Error", "Не найден тип перекрытия");
                }

                using (var transaction = new Transaction(hvacDocument, $"Create Floor in {space.Number}"))
                {
                    transaction.Start();
                    try
                    {
                        var floor = Floor.Create(hvacDocument, curveLoops, floorType.Id, level.Id);
                        var calculateArea = ParameterDisplayConvertor.SquareMeters(floor.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED).AsDouble());
                        ParametersUtility.SetParameterByValueAndName(floor, nameof(ConstructionSurfaceModel.Orientation), OrientationNames.Horizontal);
                        ParametersUtility.SetParameterByValueAndName(floor, nameof(ConstructionSurfaceModel.SpaceId), space.Id.ToString());
                        ParametersUtility.SetParameterByValueAndName(floor, nameof(ConstructionSurfaceModel.SpaceNumber), space.Number.ToString());
                        ParametersUtility.SetParameterByValueAndName(floor, nameof(ConstructionSurfaceModel.TransferCoefficient), 1);
                        ParametersUtility.SetParameterByValueAndName(floor, nameof(ConstructionSurfaceModel.ConstructionType), nameof(ConstructionSurfaceModel.ConstructionType));
                        ParametersUtility.SetParameterByValueAndName(floor, nameof(ConstructionSurfaceModel.EnclosureType), enclosureType);
                        ParametersUtility.SetParameterByValueAndName(floor, nameof(ConstructionSurfaceModel.ConstructionArea), calculateArea);
                        ParametersUtility.SetParameterByValueAndName(floor, nameof(ConstructionSurfaceModel.TemperatureInSpace), ParametersHandler.GetSpaceSetHeatPoint(hvacDocument,space));
                        ParametersUtility.SetParameterByValueAndName(floor, nameof(ConstructionSurfaceModel.TemperatureOut), ParametersHandler.GetProjectInformation(hvacDocument,nameof(ClimateData.TWinterOut092)));
                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        transaction.RollBack();
                        TaskDialog.Show("Error", $"Ошибка при создании перекрытия: {ex.Message}");
                    }
                }
            }
        }
    }
    

}