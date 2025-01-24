namespace HVACLoadTerminals.StaticData
{
    public class CalculationOptionsTypes
    {
        public static CalculationOption MinimumTerminals = new CalculationOption("MinimumTerminals", "расчетный минимум");
        public static CalculationOption DirectiveTerminalsNumber = new CalculationOption("DirectiveTerminalsNumber", "заданное количество");
        public static CalculationOption DirectiveLength = new CalculationOption("DirectiveLength", "заданная длина");
        public static CalculationOption DeviceArea = new CalculationOption("DeviceArea", "заданная площадь");
    }
}
