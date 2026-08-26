using System;
using System.Collections.Generic;
using HVACLoadTerminals.Core.Services;

namespace HVACLoadTerminals.Infrastructure.Data
{
    /// <summary>
    /// Персист UI-настроек App (панели/колонки/размер окна).
    /// Хранится в %AppData%\HVACLoadTerminals\ui-settings.json.
    /// Реконсиляция при загрузке отбрасывает устаревшие ключи колонок,
    /// клампит окно и отбрасывает неизвестные значения.
    /// </summary>
    public class UiSettings
    {
        public const int CurrentVersion = 1;

        public int Version { get; set; } = CurrentVersion;

        // ---- Окно ----
        public double WindowWidth { get; set; } = 1500;
        public double WindowHeight { get; set; } = 900;
        public double WindowLeft { get; set; } = double.NaN;
        public double WindowTop { get; set; } = double.NaN;
        /// <summary>Normal | Maximized</summary>
        public string WindowState { get; set; } = "Normal";

        // ---- Панели (минимализм этапа C) ----
        public bool ShowTreePanel { get; set; } = false;
        public bool ShowPropsPanel { get; set; } = false;
        public double TreePanelWidth { get; set; } = 250;
        public double PropsPanelWidth { get; set; } = 300;

        // ---- План/фильтры ----
        public bool ShowEnclosureCurves { get; set; } = true;
        public bool ShowRoomLabels { get; set; } = false;
        public string SelectedColorMode { get; set; } = "По k_ef";
        public string RoomFilterMode { get; set; } = "Все помещения";
        public bool LiveRecalc { get; set; } = false;
        /// <summary>RW8: показывать план уровня внизу главного окна (по умолчанию — только 🗺 модально).</summary>
        public bool ShowBottomPlan { get; set; } = false;

        // ---- Глобальные правила размещения (модалка Сервис→Правила) ----
        public double MinWindowLengthRatio { get; set; } = 0.6;
        public CeilingCountRule SupplyRule { get; set; } = CeilingCountRule.Auto;
        public CeilingCountRule ExhaustRule { get; set; } = CeilingCountRule.ByFlow;
        public int FixedSupplyCount { get; set; } = 2;
        public WallPattern SupplyPattern { get; set; } = WallPattern.LongSide;
        public WallPattern ExhaustPattern { get; set; } = WallPattern.ShortSide;
        public SingleRule SingleDeviceRule { get; set; } = SingleRule.Center;
        public double GrilleVelocityMs { get; set; } = 2.0;
        public double WallClearanceMm { get; set; } = 500;

        // ---- Колонки гридов (ключ = заголовок столбца) ----
        public Dictionary<string, double> RoomsGridColumnWidths { get; set; }
            = new Dictionary<string, double>(StringComparer.Ordinal);

        public Dictionary<string, double> PlacementsGridColumnWidths { get; set; }
            = new Dictionary<string, double>(StringComparer.Ordinal);

        /// <summary>Известные ключи колонок грида помещений.</summary>
        public static readonly HashSet<string> KnownRoomsColumns = new HashSet<string>(StringComparer.Ordinal)
        {
            "☑", "№", "Помещение", "Уровень", "S, м²", "Угл.",
            "Назначение", "Q, Вт", "Приток, м³/ч", "Вытяжка, м³/ч",
            "Системы", "Action", "Примечание"
        };

        /// <summary>Известные ключи колонок грида приборов.</summary>
        public static readonly HashSet<string> KnownPlacementsColumns = new HashSet<string>(StringComparer.Ordinal)
        {
            "Помещение", "Уровень", "Прибор", "Типоразмер", "Система",
            "Расход, м³/ч", "Высота, мм", "X, мм", "Y, мм", "Поворот°",
            "k_ef", "Расчёт"
        };

        /// <summary>Реконсиляция: отбросить устаревшие ключи, клампить значения.</summary>
        public void Reconcile()
        {
            if (Version <= 0) Version = CurrentVersion;

            WindowWidth = Clamp(WindowWidth, 800, 3840, 1500);
            WindowHeight = Clamp(WindowHeight, 600, 2160, 900);
            if (!double.IsNaN(WindowLeft))
                WindowLeft = Clamp(WindowLeft, -10000, 10000, double.NaN);
            if (!double.IsNaN(WindowTop))
                WindowTop = Clamp(WindowTop, -10000, 10000, double.NaN);
            if (WindowState != "Maximized" && WindowState != "Normal")
                WindowState = "Normal";

            TreePanelWidth = Clamp(TreePanelWidth, 150, 600, 250);
            PropsPanelWidth = Clamp(PropsPanelWidth, 200, 600, 300);

            // Цветовой режим: только из известного списка
            var allowedModes = new HashSet<string> { "По k_ef", "По системам" };
            if (!allowedModes.Contains(SelectedColorMode))
                SelectedColorMode = "По k_ef";

            // Режим фильтра: сверяем с RoomRowFilter.Modes без жёсткой зависимости от Infrastructure.Presentation
            // (дублируем список для реконсиляции без циклической ссылки; при расширении — обновить оба места).
            var allowedFilterModes = new HashSet<string>
            {
                "Все помещения", "Без назначенной системы", "Есть назначения",
                "Нет притока", "Нет вытяжки"
            };
            if (!allowedFilterModes.Contains(RoomFilterMode))
                RoomFilterMode = "Все помещения";

            // Глобальные правила размещения
            MinWindowLengthRatio = Clamp(MinWindowLengthRatio, 0.3, 1.0, 0.6);
            GrilleVelocityMs = Clamp(GrilleVelocityMs, 0.5, 5.0, 2.0);
            WallClearanceMm = Clamp(WallClearanceMm, 100, 1000, 500);
            FixedSupplyCount = (int)Clamp(FixedSupplyCount, 1, 10, 2);
            if (!Enum.IsDefined(typeof(CeilingCountRule), SupplyRule)) SupplyRule = CeilingCountRule.Auto;
            if (!Enum.IsDefined(typeof(CeilingCountRule), ExhaustRule)) ExhaustRule = CeilingCountRule.ByFlow;
            if (!Enum.IsDefined(typeof(WallPattern), SupplyPattern)) SupplyPattern = WallPattern.LongSide;
            if (!Enum.IsDefined(typeof(WallPattern), ExhaustPattern)) ExhaustPattern = WallPattern.ShortSide;
            if (!Enum.IsDefined(typeof(SingleRule), SingleDeviceRule)) SingleDeviceRule = SingleRule.Center;

            ReconcileColumns(RoomsGridColumnWidths, KnownRoomsColumns);
            ReconcileColumns(PlacementsGridColumnWidths, KnownPlacementsColumns);
        }

        private static void ReconcileColumns(
            Dictionary<string, double> dict,
            HashSet<string> known)
        {
            if (dict == null) return;
            var toRemove = new List<string>();
            foreach (var kv in dict)
            {
                if (!known.Contains(kv.Key))
                    toRemove.Add(kv.Key);
                else if (double.IsNaN(kv.Value) || double.IsInfinity(kv.Value) ||
                         kv.Value < 10 || kv.Value > 1000)
                    toRemove.Add(kv.Key);
            }
            foreach (var k in toRemove)
                dict.Remove(k);
        }

        private static double Clamp(double value, double min, double max, double fallback)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                return fallback;
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}
