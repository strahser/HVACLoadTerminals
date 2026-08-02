namespace HVACLoadTerminals.Core.Models
{
    public class PlacementOptions
    {
        /// <summary>Distance from the wall to the device center, mm.</summary>
        public double WallOffsetMm { get; set; } = 500;

        public PlacementMode Mode { get; set; } = PlacementMode.ByCalculation;

        /// <summary>Exact device count, used when Mode == ByCount.</summary>
        public int FixedCount { get; set; } = 1;

        /// <summary>Count increment step, used when Mode == ByStep.</summary>
        public int StepCount { get; set; } = 1;

        /// <summary>Safety cap on the number of devices placed.</summary>
        public int MaxCount { get; set; } = 50;

        /// <summary>Distance between device centers, mm. 0 = auto (even distribution).</summary>
        public double SpacingMm { get; set; } = 0;

        /// <summary>Margin from the edge ends, mm.</summary>
        public double StartOffsetMm { get; set; } = 0;

        public PlacementSide SidePreference { get; set; } = PlacementSide.Any;

        public CoordinateSystem CoordinateSystem { get; set; } = CoordinateSystem.Auto;

        public static PlacementOptions Default { get; } = new PlacementOptions();
    }
}
