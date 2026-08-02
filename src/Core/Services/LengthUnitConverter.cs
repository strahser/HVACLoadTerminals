namespace HVACLoadTerminals.Core.Services
{
    /// <summary>
    /// Length unit conversion helpers. Revit internal units are feet;
    /// 1 foot = 304.8 mm.
    /// </summary>
    public static class LengthUnitConverter
    {
        public const double MmPerFoot = 304.8;

        public static double MmToUnits(double mm) => mm / MmPerFoot;

        public static double UnitsToMm(double units) => units * MmPerFoot;
    }
}
