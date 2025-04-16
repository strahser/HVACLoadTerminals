using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls.Calculators;
using HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls.ParametersHandlersStrategy.Interface;
using HVACLoadTerminals.Utils;

namespace HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls.ParametersHandlersStrategy.Strategies;


// Базовая стратегия
public class BaseWallParametersStrategy(Document hvacDoc, string northDirection) : IWallParametersStrategy
{
    public void ApplyParameters(
        Wall wall,
        Space space,
        ConstructionSurfaceModel faceModel,
        Curve wallCurve,
        Level groundLevel)
        {
                // Установка ориентации
                var orientation = new OrientationCalculator().Calculate(wallCurve, northDirection);
                ParametersUtility.SetParameterByValueAndName(wall, nameof(faceModel.Orientation), orientation);

                // Применение параметров пространства
                ApplyParametersHandler.ApplySpaceParameters(wall, space);

                // Применение параметров модели через рефлексию
                ApplyParametersHandler.ApplyModelParameters(wall, faceModel,ApplyParametersHandler.WallFields);

                // Высотные параметры стены
                var roomBoundingParam = wall.get_Parameter(BuiltInParameter.WALL_ATTR_ROOM_BOUNDING);
                roomBoundingParam?.Set(0);

                var heightParam = wall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM);
                double spaceHeight = space.get_Parameter(BuiltInParameter.ROOM_HEIGHT)?.AsDouble() ?? 0;
                heightParam?.Set(spaceHeight);

                // Вычисляемые параметры стены
                var areaParam = wall.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED);
                double calculateArea = ParameterDisplayConvertor.SquareMeters(areaParam.AsDouble());
                ParametersUtility.SetParameterByValueAndName(wall, nameof(faceModel.ConstructionArea), calculateArea);
            }
}