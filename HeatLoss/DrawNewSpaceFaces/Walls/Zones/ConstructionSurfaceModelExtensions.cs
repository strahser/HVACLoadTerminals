namespace HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls.Zones;

public static class ConstructionSurfaceModelExtensions
{
    public static ConstructionSurfaceModel CloneWithZone(this ConstructionSurfaceModel original, string zoneNumber, double zoneValue)
    {
        return new ConstructionSurfaceModel
        {
            _Face = original._Face,
            ConstructionName = $"{original.ConstructionName} [Зона {zoneNumber}]",
            UndergroundZoneNumber = zoneNumber,
            UndergroundZoneValue = zoneValue,
            Orientation = original.Orientation,
            // Добавьте инициализацию всех остальных обязательных полей
        };
    }
}