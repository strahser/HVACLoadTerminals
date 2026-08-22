namespace HVACLoadTerminals.Core.Models
{
    public enum PlacementMode
    {
        ByCalculation,

        /// <summary>Exact user-specified quantity.</summary>
        ByCount,

        ByStep,

        /// <summary>From the served area: ceil(room area / device service area).</summary>
        ByArea,

        /// <summary>Along an edge of given length: ceil(edge length / directive length).</summary>
        ByLength
    }
}
