namespace HVACLoadTerminals.ModelsStatic
{
    public static class CalculationOptionsTypes
    {
        public static readonly CalculationOption MinimumTerminals = new CalculationOption("MinimumTerminals", "расчетный минимум");
        public static readonly CalculationOption DirectiveTerminalsNumber = new CalculationOption("DirectiveTerminalsNumber", "заданное количество");
        public static CalculationOption DirectiveLength = new CalculationOption("DirectiveLength", "заданная длина");
        public static CalculationOption DeviceArea = new CalculationOption("DeviceArea", "заданная площадь");
    }
}
