using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using HVACLoadTerminals.Utils;

namespace HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls.ParametersHandlersStrategy.Strategies;

public static class ApplyParametersHandler
{
    
    public static readonly HashSet<string> WallFields =
    [
        nameof(ConstructionSurfaceModel.EnclosureType),
        nameof(ConstructionSurfaceModel.ConstructionName),
        nameof(ConstructionSurfaceModel.TransferCoefficient)
    ];

    public static void ApplySpaceParameters(Wall wall, Space space)
    {
        var spaceParameters = new Dictionary<string, object>
        {
            { nameof(ConstructionSurfaceModel.SpaceId), space.Id.ToString() },
            { nameof(ConstructionSurfaceModel.SpaceNumber), space.Number.ToString() },
            { nameof(ConstructionSurfaceModel.SpaceName), space.Name.ToString() }
        };

        ApplyParametersHandler.SetMultipleParameters(wall, spaceParameters.Select(kv => (kv.Key, kv.Value)).ToArray());
    }

    public static void ApplyModelParameters(Wall wall, ConstructionSurfaceModel faceModel,HashSet<string> allowedFields)
    {
        foreach (var property in typeof(ConstructionSurfaceModel).GetProperties())
        {
            if (allowedFields.Contains(property.Name))
            {
                var parameterValue = property.GetValue(faceModel)?.ToString();
                if (parameterValue != null)
                {
                    ParametersUtility.SetParameterByValueAndName(wall, property.Name, parameterValue);
                }
            }
        }
    }

    public static void SetMultipleParameters(Wall wall, params (string parameterName, object value)[] parameters)
    {
        foreach (var (parameterName, value) in parameters)
        {
            ParametersUtility.SetParameterByValueAndName(wall, parameterName, value);
        }
    }
}