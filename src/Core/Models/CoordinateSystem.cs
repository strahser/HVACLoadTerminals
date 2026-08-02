namespace HVACLoadTerminals.Core.Models
{
    /// <summary>
    /// Placement coordinate system: which wall edge of the room's bounding box
    /// devices align to (Bottom = along the bottom wall edge, etc.).
    /// </summary>
    public enum CoordinateSystem
    {
        Auto,
        Bottom,
        Right,
        Top,
        Left
    }
}
