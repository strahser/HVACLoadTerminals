using System;
using System.Diagnostics;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using HVACLoadTerminals.ClimateData;
using HVACLoadTerminals.ModelsStatic;
using HVACLoadTerminals.Utils;

namespace HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls;

public class WallHandler
{
    private static readonly Document HvacDocument = RevitConfig.Document;
    
    public static Wall CreateWallWithOffset(
        Document doc,
        ConstructionSurfaceModel faceData,
        string northDirection,
        Level baseLevel,
        double baseOffset,
        double height)
    {
        if (faceData?._Face == null) return null;

        var curve = GetMainCurve(faceData._Face);
        if (curve == null) return null;

        try
        {
            var wall = Wall.Create(
                doc,
                curve,
                baseLevel.Id,
                structural: false
            );

            wall.get_Parameter(BuiltInParameter.WALL_BASE_OFFSET)
                .Set(UnitUtils.ConvertToInternalUnits(baseOffset, UnitTypeId.Meters));

            wall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM)
                .Set(UnitUtils.ConvertToInternalUnits(height, UnitTypeId.Meters));

            SetOrientation(wall, curve, northDirection);
            return wall;
        }
        catch
        {
            return null;
        }
    }
    private static void SetOrientation(Wall wall, Curve curve, string north)
    {
        var orientation = CalculateOrientation(curve, north);
        ParametersUtility.SetParameterByValueAndName(wall, "Orientation", orientation);
    }

    private static Curve GetMainCurve(Face face)
    {
        var loops = face.GetEdgesAsCurveLoops();
        return loops?.FirstOrDefault()?.FirstOrDefault();
    }

    
    public static Wall CreateSingleWall(Space space, ConstructionSurfaceModel faceModel, string northDirection)
            
        {   // Проверка на null space, faceModel или face
            if (space == null || faceModel == null || faceModel._Face == null)
            {
                Debug.WriteLine($"Предупреждение: Пропущен вызов DrawWallBySpaceAndFace из-за null аргументов.");
                return null;
            }
         
            // Создаем транзакцию
            using var transaction = new Transaction(HvacDocument, $"Создать стену {space.Name}-{space.Number}");
            transaction.Start();
            // Получаем CurveLoops из Face
            var curveLoops = faceModel._Face.GetEdgesAsCurveLoops();
            if (curveLoops == null || curveLoops.Count == 0)
            {
                Debug.WriteLine($"Предупреждение: Не найдены кривые для грани пространства {space.Name}.");
                transaction.RollBack();
                return null; // Если нет CurveLoops, завершаем
            }
            // Создаем CurveArray для определения стены
            var curveArray = new CurveArray();
            foreach (var loop in curveLoops)
            {
                if (loop == null) continue;
                foreach (var curve in loop)
                {
                    if(curve != null) curveArray.Append(curve);
                }
            }
            if (curveArray.IsEmpty)
            {
                Debug.WriteLine($"Предупреждение: Не удалось сформировать CurveArray для стены в пространстве {space.Name}.");
                transaction.RollBack();
                return null;
            }
            var wallCurve = curveArray.get_Item(0);
            if (wallCurve == null)
            {
                Debug.WriteLine($"Предупреждение: Не удалось получить кривую для создания стены в пространстве {space.Name}.");
                transaction.RollBack();
                return null;
            }
            var wall = Wall.Create(HvacDocument, wallCurve, space.Level.Id, structural: false);
            var orientationValue = CalculateOrientation(wallCurve, northDirection);
            ParametersUtility.SetParameterByValueAndName(wall, nameof(ConstructionSurfaceModel.Orientation), orientationValue);
            SetWallParameters(space, faceModel, wall);   
            transaction.Commit();
            return wall;
        }

    public static void SetWallParameters(Space space, ConstructionSurfaceModel faceModel, Wall wall,double? height=null)
        {
            // Устанавливаем параметры стены (если они есть)
            var roomBoundingParam = wall.get_Parameter(BuiltInParameter.WALL_ATTR_ROOM_BOUNDING);
            var heightParameter = wall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM);
            var calculateArea = ParameterDisplayConvertor.SquareMeters(wall.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED).AsDouble());
            var wallHeight = height ?? space.get_Parameter(BuiltInParameter.ROOM_HEIGHT)?.AsDouble() ?? 0;;
            ////устанавливаем параметры в Ревит поверхности
            roomBoundingParam?.Set(0); // 0 - означает false
            heightParameter?.Set(wallHeight); // берем и устанавливаем в футах
            ParametersUtility.SetParameterByValueAndName(wall, nameof(ConstructionSurfaceModel.UndergroundZoneNumber), faceModel.UndergroundZoneNumber);
            ParametersUtility.SetParameterByValueAndName(wall, nameof(ConstructionSurfaceModel.UndergroundZoneValue), faceModel.UndergroundZoneValue);
            ParametersUtility.SetParameterByValueAndName(wall, nameof(ConstructionSurfaceModel.SpaceId), space.Id.ToString());
            ParametersUtility.SetParameterByValueAndName(wall, nameof(ConstructionSurfaceModel.SpaceNumber), space.Number.ToString());
            ParametersUtility.SetParameterByValueAndName(wall, nameof(ConstructionSurfaceModel.SpaceName), space.Name.ToString());
            ParametersUtility.SetParameterByValueAndName(wall, nameof(ConstructionSurfaceModel.TransferCoefficient), faceModel.TransferCoefficient);
            ParametersUtility.SetParameterByValueAndName(wall, nameof(ConstructionSurfaceModel.ConstructionName), faceModel.ConstructionName);
            ParametersUtility.SetParameterByValueAndName(wall, nameof(ConstructionSurfaceModel.EnclosureType), faceModel.EnclosureType);
            ParametersUtility.SetParameterByValueAndName(wall, nameof(ConstructionSurfaceModel.ConstructionArea), calculateArea);
            ParametersUtility.SetParameterByValueAndName(wall, nameof(ConstructionSurfaceModel.TemperatureInSpace), ParametersHandler.GetSpaceSetHeatPoint(HvacDocument,space));
            ParametersUtility.SetParameterByValueAndName(wall, nameof(ConstructionSurfaceModel.TemperatureOut), ParametersHandler.GetProjectInformation(HvacDocument,nameof(ClimateDataModel.TWinterOut092)));
        }

    private static string CalculateOrientation(Curve curve, string northDirection)
        {
            //northDirection up,down,left,right)
            var mapping = OrientationMapping.OrientationMappings.FirstOrDefault(m =>
                m.MainDirection.ToLower() == northDirection.ToLower());
            if (curve is Arc)
            {
                // Если кривая - дуга, преобразуем ее в линию
                var startPoint = curve.GetEndPoint(0);
                var endPoint = curve.GetEndPoint(1);
                curve = Line.CreateBound(startPoint, endPoint);
            }

            // Получение вектора направления кривой
            var curveDirection = curve.GetEndPoint(1) - curve.GetEndPoint(0);
            curveDirection.Normalize(); // Нормализация вектора
            return CurveNormalizeMappingOrientation(curveDirection, mapping);
        }

    private static string CurveNormalizeMappingOrientation(XYZ curveDirection, OrientationMapping mapping)
        {
            // Определение ориентации
            if (Math.Abs(curveDirection.Y) > 0.9) // Вертикальное направление (С/Ю)
            {
                return curveDirection.Y > 0 ? mapping.N : mapping.S;
            }
            else if (Math.Abs(curveDirection.X) > 0.9) // Горизонтальное направление (В/З)
            {
                return curveDirection.X > 0 ? mapping.E : mapping.W;
            }
            else
            {
                // Промежуточные направления
                if (curveDirection.X > 0 && curveDirection.Y > 0)
                {
                    return mapping.NE;  // Северо-восток
                }
                else if (curveDirection.X < 0 && curveDirection.Y > 0)
                {
                    return mapping.NW; // Северо-запад
                }
                else if (curveDirection.X > 0 && curveDirection.Y < 0)
                {
                    return mapping.SE; // Юго-восток
                }
                else if (curveDirection.X < 0 && curveDirection.Y < 0)
                {
                    return mapping.SW; // Юго-запад
                }
                else
                {
                    return "Не определено"; // Ориентация не определена
                }
            }
        }
}