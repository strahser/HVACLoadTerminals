using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI;
using HVACLoadTerminals.CalculateSpaceDevice;
using HVACLoadTerminals.ModelsStatic;
using HVACLoadTerminals.Utils;
namespace HVACLoadTerminals.DrawNewSpaceFaces.FloorsRoofs
{
    public abstract class DrawFloors
    {  
        public static void DrawFloorsForSelectedSpaces(Document hvacDocument,List<Space> spaces,Level level, 
            FloorType floorType,string enclosureType=EnclosureTypeOptions.Floor)
        {
            //Находим тип перекрытия
            var collector = new FilteredElementCollector(hvacDocument);

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

                using (var transaction = new Transaction(hvacDocument, $"Create Floor in {space.Number}"))
                {
                    transaction.Start();
                    try
                    {
                        var floor = Floor.Create(hvacDocument, curveLoops, floorType.Id, level.Id);
                        var calculateArea = ParameterDisplayConvertor.SquareMeters(floor.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED).AsDouble());
                        ParametersUtility.SetParameterByValue(floor, "Orientation", OrientationNames.Horizontal);
                        ParametersUtility.SetParameterByValue(floor, "SpaceId", space.Id.ToString());
                        ParametersUtility.SetParameterByValue(floor, "SpaceNumber", space.Number.ToString());
                        ParametersUtility.SetParameterByValue(floor, "TransferCoefficient", 0);
                        ParametersUtility.SetParameterByValue(floor, "ConstructionType", "no construction");
                        ParametersUtility.SetParameterByValue(floor, "EnclosureType", enclosureType);
                        ParametersUtility.SetParameterByValue(floor, "ConstructionArea", calculateArea);
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