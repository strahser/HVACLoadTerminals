using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI;
using HVACLoadTerminals.CalculateSpaceDevice;
using HVACLoadTerminals.ClimateData;
using HVACLoadTerminals.ModelsStatic;
using HVACLoadTerminals.Utils;

namespace HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.FloorsRoofs
{
    public  class DrawFloors
    {
        private static  Document _hvacDocument = RevitConfig.Document;  
        public static int DrawFloorsForSelectedSpaces(Document hvacDocument,List<Space> spaces, 
            FloorType floorType,string enclosureType,Level level=null)
        {
            var options = new SpatialElementBoundaryOptions()
            {
                SpatialElementBoundaryLocation = SpatialElementBoundaryLocation.Finish,
                StoreFreeBoundaryFaces = true
            };

            var count = 0;
            foreach (var space in spaces.Cast<Space>())
            {
                var segments = space.GetBoundarySegments(options).First();
                var loop = new CurveLoop();
                foreach (var segment in segments)
                {
                    loop.Append(segment.GetCurve());
                }

                IList<CurveLoop> curveLoops = [loop];

                if (floorType == null)
                {
                    TaskDialog.Show("Error", "Не найден тип перекрытия");
                }

                using var transaction = new Transaction(hvacDocument, $"Create Floor in {space.Number}");
                transaction.Start();
                try
                {
                    if (level == null)
                    {
                        var floor = Floor.Create(hvacDocument, curveLoops, floorType.Id, space.LevelId);
                        AddParametersToFloor(floor, space, enclosureType);
                    }
                    else
                    {
                        var floor = Floor.Create(hvacDocument, curveLoops, floorType.Id, level.Id);
                        AddParametersToFloor(floor, space, enclosureType);
                    }

                    count++;
                    transaction.Commit();
                }
                catch (Exception ex)
                {
                    transaction.RollBack();
                    TaskDialog.Show("Error", $"Ошибка при создании перекрытия: {ex.Message}");
                }
                
            }

            return count;
        }

             public static void DrawFloorsForSelectedSpaces(Document hvacDocument,List<Space> spaces,Level level, FloorType floorType)
         {
 
             foreach (var space in spaces.Cast<Space>())
             {
                 var spaceBoundary = new SpaceBoundaryCurve(space as Space);
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

                 using var transaction = new Transaction(hvacDocument, $"Create Floor in {space.Number}");
                 transaction.Start();
                 try
                 {
                     var floor = Floor.Create(hvacDocument, curveLoops, floorType.Id, level.Id);
                     var calculateArea = ParameterDisplayConvertor.SquareMeters(floor.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED).AsDouble());

                     transaction.Commit();
                 }
                 catch (Exception ex)
                 {
                     transaction.RollBack();
                     TaskDialog.Show("Error", $"Ошибка при создании перекрытия: {ex.Message}");
                 }
             }
         }
        private static  void AddParametersToFloor(Floor floor, Space space, string enclosureType )
        {
            var calculateArea = ParameterDisplayConvertor.SquareMeters(floor.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED).AsDouble());
            ParametersUtility.SetParameterByValueAndName(floor, nameof(ConstructionSurfaceModel.Orientation), OrientationNames.Horizontal);
            ParametersUtility.SetParameterByValueAndName(floor, nameof(ConstructionSurfaceModel.SpaceId), space.Id.ToString());
            ParametersUtility.SetParameterByValueAndName(floor, nameof(ConstructionSurfaceModel.SpaceNumber), space.Number.ToString());
            ParametersUtility.SetParameterByValueAndName(floor, nameof(ConstructionSurfaceModel.SpaceName), space.Name.ToString());
            ParametersUtility.SetParameterByValueAndName(floor, nameof(ConstructionSurfaceModel.TransferCoefficient), 1);
            ParametersUtility.SetParameterByValueAndName(floor, nameof(ConstructionSurfaceModel.ConstructionName), nameof(ConstructionSurfaceModel.ConstructionName));
            ParametersUtility.SetParameterByValueAndName(floor, nameof(ConstructionSurfaceModel.EnclosureType), enclosureType);
            ParametersUtility.SetParameterByValueAndName(floor, nameof(ConstructionSurfaceModel.ConstructionArea), calculateArea);
            ParametersUtility.SetParameterByValueAndName(floor, nameof(ConstructionSurfaceModel.TemperatureInSpace), ParametersHandler.GetSpaceSetHeatPoint(_hvacDocument,space));
            ParametersUtility.SetParameterByValueAndName(floor, nameof(ConstructionSurfaceModel.TemperatureOut), ParametersHandler.GetProjectInformation(_hvacDocument,nameof(ClimateDataModel.TWinterOut092)));
        }
    }
    

}