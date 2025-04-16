using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;

namespace HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls.ParametersHandlersStrategy.Interface;

public interface IWallParametersStrategy
{
    void ApplyParameters(
        Wall wall,
        Space space,
        ConstructionSurfaceModel faceModel,
        Curve wallCurve,
        Level groundLevel
    );
}