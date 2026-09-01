namespace HVACLoadTerminals.Core.Models
{
    /// <summary>Форма прибора в плане — для масштабированного отображения.</summary>
    public enum DevicePlanShape
    {
        /// <summary>Прямоугольник (вытяжная решётка, радиатор, фанкойл кассетный и т.д.).</summary>
        Rectangular = 0,
        /// <summary>Круг (круглый диффузор Ø).</summary>
        Circular = 1
    }

    public class TerminalDevice
    {
        public string Id { get; }
        public string FamilyName { get; }
        public string TypeName { get; }
        public string Manufacturer { get; }
        public double MaxFlowRate { get; }
        public string FlowParameterName { get; }
        public HVACSystemType SystemType { get; }

        /// <summary>Cooling capacity, Watts. 0 = not applicable.</summary>
        public double CoolingCapacityW { get; }

        /// <summary>Heating capacity, Watts. 0 = not applicable.</summary>
        public double HeatingCapacityW { get; }

        /// <summary>Served floor area per unit, m2. 0 = unknown (flow-based sizing).</summary>
        public double ServiceAreaM2 { get; }

        /// <summary>Форма отображения в плане (прямоугольник / круг).</summary>
        public DevicePlanShape PlanShape { get; }

        /// <summary>Диаметр для круглого прибора, мм. Если 0 — берётся WidthMm.</summary>
        public double DiameterMm { get; }

        /// <summary>Device footprint width, mm. 0 = unknown. Для круга — диаметр, если DiameterMm==0.</summary>
        public double WidthMm { get; }

        /// <summary>Device footprint height (depth from the wall), mm. 0 = unknown.</summary>
        public double HeightMm { get; }

        /// <summary>P3/M0.2: потолочный offset типоразмера, мм — заглубление прибора
        /// от чистого потолка (аналог ceiling_offset прототипа). 0 = не задан.</summary>
        public double CeilingOffsetMm { get; }

        /// <summary>P1: отступ от стены для этого типоразмера, мм (аналог
        /// wall_offset прототипа). &gt;0 переопределяет общий WallClearanceMm.</summary>
        public double WallOffsetMm { get; }

        /// <summary>P1: директивное количество приборов (аналог directive_terminals
        /// прототипа). 0 = правило не задано на типоразмере.</summary>
        public int DirectiveTerminals { get; }

        /// <summary>P1: директивная длина размещения, мм (аналог directive_length:
        /// N = ceil(длина участка / директивная длина)). 0 = не задана.</summary>
        public double DirectiveLengthMm { get; }

        /// <summary>P1: ориентация option1 (аналог device_orientation_option1):
        /// сторона длинного плеча для расстановки вдоль стен.</summary>
        public string OrientationOption1 { get; }

        /// <summary>P1: ориентация option2 (аналог device_orientation_option2).</summary>
        public string OrientationOption2 { get; }

        /// <summary>P1: ориентация одиночного прибора (аналог
        /// single_device_orientation): center / corner.</summary>
        public string SingleOrientation { get; }

        /// <summary>Effective capacity for the given system type, Watts or m3/h.</summary>
        public double CapacityFor(HVACSystemType type) =>
            type == HVACSystemType.Heating && HeatingCapacityW > 0
                ? HeatingCapacityW
                : MaxFlowRate;

        public TerminalDevice(
            string id,
            string familyName,
            string typeName,
            string manufacturer,
            double maxFlowRate,
            string flowParameterName,
            HVACSystemType systemType,
            double coolingCapacityW = 0,
            double widthMm = 0,
            double heightMm = 0,
            double heatingCapacityW = 0,
            double serviceAreaM2 = 0,
            double ceilingOffsetMm = 0,
            double wallOffsetMm = 0,
            int directiveTerminals = 0,
            double directiveLengthMm = 0,
            string orientationOption1 = "",
            string orientationOption2 = "",
            string singleOrientation = "",
            DevicePlanShape planShape = DevicePlanShape.Rectangular,
            double diameterMm = 0)
        {
            Id = id;
            FamilyName = familyName;
            TypeName = typeName;
            Manufacturer = manufacturer;
            MaxFlowRate = maxFlowRate;
            FlowParameterName = flowParameterName;
            SystemType = systemType;
            CoolingCapacityW = coolingCapacityW;
            WidthMm = widthMm;
            HeightMm = heightMm;
            HeatingCapacityW = heatingCapacityW;
            ServiceAreaM2 = serviceAreaM2;
            CeilingOffsetMm = ceilingOffsetMm;
            WallOffsetMm = wallOffsetMm;
            DirectiveTerminals = directiveTerminals;
            DirectiveLengthMm = directiveLengthMm;
            OrientationOption1 = orientationOption1 ?? "";
            OrientationOption2 = orientationOption2 ?? "";
            SingleOrientation = singleOrientation ?? "";
            PlanShape = planShape;
            DiameterMm = diameterMm;
        }

        /// <summary>Эффективная ширина в плане, мм (для прямоугольника — WidthMm, для круга — диаметр).</summary>
        public double EffectiveWidthMm
        {
            get
            {
                if (PlanShape == DevicePlanShape.Circular)
                {
                    if (DiameterMm > 0) return DiameterMm;
                    if (WidthMm > 0) return WidthMm;
                    if (HeightMm > 0) return HeightMm;
                    return 0;
                }
                return WidthMm;
            }
        }

        /// <summary>Эффективная высота в плане, мм (для прямоугольника — HeightMm, для круга — = ширине).</summary>
        public double EffectiveHeightMm
        {
            get
            {
                if (PlanShape == DevicePlanShape.Circular)
                    return EffectiveWidthMm;
                return HeightMm;
            }
        }

        /// <summary>Размер для отрисовки в масштабе: если габариты не заданы — фолбэк 400×400 (или Ø400).</summary>
        public (double wMm, double hMm) GetPlanSizeFallback()
        {
            double w = EffectiveWidthMm;
            double h = EffectiveHeightMm;
            if (PlanShape == DevicePlanShape.Circular)
            {
                if (w <= 0) w = 400;
                h = w;
                return (w, h);
            }
            if (w <= 0) w = 600;
            if (h <= 0) h = 400;
            // Для отопительных — длина по Width, высота-малая (например 100 мм глубина)
            // но в плане важна длина, глубину можно условно 150 мм
            if (SystemType == HVACSystemType.Heating && h <= 0) h = 150;
            return (w, h);
        }

        public override string ToString() => $"{FamilyName} - {TypeName} ({MaxFlowRate} m3/h)";
    }
}
