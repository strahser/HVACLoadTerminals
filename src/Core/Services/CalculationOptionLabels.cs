namespace HVACLoadTerminals.Core.Services
{
    /// <summary>
    /// Метки «почему выбрано такое количество приборов» (план P2). Значения —
    /// словарь прототипа InsertTerminalsPandas (`calculation_options`,
    /// Static/CalculationOptions.py), чтобы таблицы/экспорт были читаемы
    /// инженерам, привыкшим к прототипу.
    /// </summary>
    public static class CalculationOptionLabels
    {
        /// <summary>Количество задано явно (Fixed N / directive_terminals).</summary>
        public const string FixedN = "directive_terminals";

        /// <summary>N = ceil(площадь / площадь обслуживания) — по площади.</summary>
        public const string Area = "device_area";

        /// <summary>N = ceil(расход / max_flow) — минимум по расходу.</summary>
        public const string MinByFlow = "minimum_terminals";

        /// <summary>N = ceil(длина / директивная длина) — по длине (окно/стена).</summary>
        public const string Length = "directive_length";
    }
}
