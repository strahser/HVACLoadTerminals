// ReSharper disable UnusedMember.Global
// ReSharper disable InconsistentNaming

using HVACLoadTerminals.ModelsStatic;

namespace HVACLoadTerminals.ClimateData;
//https://helpeng.ru/engineer/climatology/climatology-2020
    public class ClimateDataModel
    {
        [Description("Регион")]
        [RevitParameter("SpecTypeId.String.Text")]
        public string Region { get; set; }
        
        [Description("Категория зданий")]
        [RevitParameter("SpecTypeId.String.Text")]
        public string BuildingCategory { get; set; }

        [Description("Город")]
        [RevitParameter("SpecTypeId.String.Text")]
        public string City { get; set; }

        [Description("Температура воздуха наиболее холодных суток,(°C) обеспеченностью 0,92")]
        [RevitParameter]
        public double TWinterOut092Max { get; set; }
        
        [Description("Температура воздуха наиболее холодных суток,(°C) обеспеченностью 0,98")]
        [RevitParameter]
        public double TWinterOut098Max { get; set; }
        
        
        [Description("Температура воздуха наиболее холодной пятидневки (°C) обеспеченностью 0,92")]
        [RevitParameter]
        public double TWinterOut092 { get; set; }

        [Description("Температура воздуха наиболее холодной пятидневки  (°C) обеспеченностью 0,98")]
        [RevitParameter]
        public double TWinterOut098 { get; set; }
        
        [Description("Продолжительность отопительного периода (сутки) со средней суточной температурой воздуха ≤ 8 °C")]
        [RevitParameter]
        public double heatingPeriodDuration8C { get; set; }

        [Description("Продолжительность отопительного периода (сутки) со средней суточной температурой воздуха ≤ 10 °C")]
        [RevitParameter]
        public double HeatingPeriodDuration10C { get; set; }
        
        [Description("Средняя температура наружного воздуха для ≤ 8 °C")]
        [RevitParameter]
        public double heatingPeriodAvgTemperature8C { get; set; }
        
        [Description("Средняя температура наружного воздуха для ≤ 10 °C")]
        [RevitParameter]
        public double heatingPeriodAvgTemperature10C { get; set; }
        
        [Description("Максимальная из средних скоростей ветра по румбам за январь (м/с)")]
        [RevitParameter]
        public double WinterWindSpeed { get; set; }

        [Description("Относительная влажность воздуха в наиболее холодный период (%)")]
        [RevitParameter]
        public double WinterRelativeHumidity { get; set; }
        
        [Description("Абсолютная влажность воздуха наиболее холодного периода (г/кг)")]
        [RevitParameter]
        public double WinterAbsoluteHumidity { get; set; }
        
        [Description("градусо-сутки отопительного периода, (°C·сут)/год")]
        [RevitParameter]
        public double Gsop{ get; set; }

        [Description("Температура воздуха наиболее жаркого периода (°C) обеспеченностью 0,92")]
        [RevitParameter]
        public double THotOut092 { get; set; }

        [Description("Радиационный баланс поверхности горизонтальной (кДж/(м2 мес.))")]
        [RevitParameter]
        public double RadiationBalance { get; set; }

        [Description("расчетная температура внутреннего воздуха здания, °C,")]
        [RevitParameter]
        public double Tin { get; set; } 
        
        
        /* расчетная температура внутреннего воздуха здания, °С, принимаемая при расчете
        ограждающих конструкций групп зданий, указанных в таблице 3:
        по поз.1 – по минимальным значениям оптимальной температуры соответствующих зданий по
        ГОСТ 30494; 
        по поз.2 – согласно классификации помещений и минимальных значений оптимальной температуры по ГОСТ 30494;
        по поз. 3 – по нормам проектирования соответствующих зданий;*/
        /*tот, zот – средняя температура наружного воздуха, °С, и продолжительность, сут/год,
        отопительного периода соответственно, принимаемые по СП 131.13330 для жилых и
        общественных зданий для периода со среднесуточной температурой наружного воздуха не более 8 °С,
        а при проектировании дошкольных образовательных  организаций, общеобразовательных организаций,
         медицинских организаций и домовинтернатов для престарелых не более 10 °С.*/


    }

