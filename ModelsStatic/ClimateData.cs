// ReSharper disable UnusedMember.Global
// ReSharper disable InconsistentNaming

namespace HVACLoadTerminals.ModelsStatic;
//https://helpeng.ru/engineer/climatology/climatology-2020
    public class ClimateData
    {
        [Description("Регион")]
        [RevitParameter("SpecTypeId.String.Text")]
        public string Region { get; set; }

        [Description("Город")]
        [RevitParameter("SpecTypeId.String.Text")]
        public string City { get; set; }

        #region Winter
        
        [Description("Температура воздуха наиболее холодной пятидневки (°C) обеспеченностью 0,92")]
        [RevitParameter("SpecTypeId.Number")]
        public double TWinterOut092 { get; set; }

        [Description("Температура воздуха наиболее холодных суток (°C) обеспеченностью 0,98")]
        [RevitParameter("SpecTypeId.Number")]
        public double TWinterOut098 { get; set; }
        
        [Description("Продолжительность отопительного периода (сутки) со средней суточной температурой воздуха ≤ 8 °C")]
        [RevitParameter("SpecTypeId.Number")]
        public double HeatingPeriodDuration { get; set; }

        [Description("Средняя температура отопительного периода (°C)")]
        [RevitParameter("SpecTypeId.Number")]
        public double HeatingPeriodAvgTemperature { get; set; }
        
        [Description("Максимальная из средних скоростей ветра по румбам за январь (м/с)")]
        [RevitParameter("SpecTypeId.Number")]
        public double WinterWindSpeed { get; set; }

        [Description("Относительная влажность воздуха в наиболее холодный период (%)")]
        [RevitParameter("SpecTypeId.Number")]
        public double WinterRelativeHumidity { get; set; }
        
        [Description("Абсолютная влажность воздуха наиболее холодного периода (г/кг)")]
        [RevitParameter("SpecTypeId.Number")]
        public double WinterAbsoluteHumidity { get; set; }
        #endregion

        #region Summer

        [Description("Температура воздуха наиболее жаркого периода (°C) обеспеченностью 0,92")]
        [RevitParameter("SpecTypeId.Number")]
        public double THotOut092 { get; set; }

        [Description("Радиационный баланс поверхности горизонтальной (кДж/(м2 мес.))")]
        [RevitParameter("SpecTypeId.Number")]
        public double RadiationBalance { get; set; }

        #endregion

    }

