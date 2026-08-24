using System;
using System.Collections.Generic;
using System.Linq;
using ClosedXML.Excel;
using HVACLoadTerminals.Infrastructure.Presentation;

namespace HVACLoadTerminals.Infrastructure.Visualization
{
    /// <summary>
    /// P6: выгрузка результатов расстановки в Excel (аналог вкладки Downloads
    /// прототипа: лист «level_values»). Два листа:
    ///  «Сводка» — по (помещение × система): количество, расход, k_ef, правило;
    ///  «Приборы» — по-приборные строки с координатами в мм.
    /// </summary>
    public static class PlacementExcelExporter
    {
        private static readonly string[] SummaryColumns =
        {
            "S_ID", "S_Number", "S_Name", "S_level",
            "family_device_name", "family_instance_name",
            "minimum_device_number", "flow_to_device_calculated",
            "system_name", "k_ef", "calculation_option"
        };

        public static void Save(string path, IReadOnlyList<PlacementRow> rows)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Путь к файлу не задан", nameof(path));
            if (rows == null || rows.Count == 0)
                throw new ArgumentException("Нет размещений для выгрузки", nameof(rows));

            using var wb = new XLWorkbook();
            var summary = wb.Worksheets.Add("level_values");
            for (int c = 0; c < SummaryColumns.Length; c++)
                summary.Cell(1, c + 1).Value = SummaryColumns[c];

            int r = 2;
            foreach (var g in rows.GroupBy(p => (p.RoomId, p.SystemName))
                     .OrderBy(g => g.Key.RoomId, StringComparer.Ordinal)
                     .ThenBy(g => g.Key.SystemName, StringComparer.Ordinal))
            {
                var first = g.First();
                double avgFlow = g.Average(p => p.CalculatedFlow);
                double avgKef = g.Average(p => p.KEf);

                string number = first.RoomName, name = "";
                int dot = first.RoomName.IndexOf(". ", StringComparison.Ordinal);
                if (dot > 0)
                {
                    number = first.RoomName.Substring(0, dot);
                    name = first.RoomName.Substring(dot + 2);
                }

                summary.Cell(r, 1).Value = g.Key.RoomId;
                summary.Cell(r, 2).Value = number;
                summary.Cell(r, 3).Value = name;
                summary.Cell(r, 4).Value = first.LevelName;
                summary.Cell(r, 5).Value = first.Family;
                summary.Cell(r, 6).Value = first.TypeName;
                summary.Cell(r, 7).Value = g.Count();
                summary.Cell(r, 8).Value = Math.Round(avgFlow, 1);
                summary.Cell(r, 9).Value = first.SystemName;
                summary.Cell(r, 10).Value = avgKef > 0 ? Math.Round(avgKef, 2) : "";
                summary.Cell(r, 11).Value = first.CalculationOption;
                r++;
            }
            summary.Columns().AdjustToContents(1, Math.Max(1, r - 1));
            summary.SheetView.FreezeRows(1);

            var devices = wb.Worksheets.Add("Приборы");
            string[] deviceColumns =
            {
                "S_ID", "Помещение", "Уровень", "Система", "Прибор", "Типоразмер",
                "X, мм", "Y, мм", "Высота, мм", "Расход, м³/ч", "k_ef", "Расчёт"
            };
            for (int c = 0; c < deviceColumns.Length; c++)
                devices.Cell(1, c + 1).Value = deviceColumns[c];

            int dr = 2;
            foreach (var p in rows)
            {
                devices.Cell(dr, 1).Value = p.RoomId;
                devices.Cell(dr, 2).Value = p.RoomName;
                devices.Cell(dr, 3).Value = p.LevelName;
                devices.Cell(dr, 4).Value = p.SystemName;
                devices.Cell(dr, 5).Value = p.Family;
                devices.Cell(dr, 6).Value = p.TypeName;
                devices.Cell(dr, 7).Value = p.X;
                devices.Cell(dr, 8).Value = p.Y;
                devices.Cell(dr, 9).Value = p.MountHeightMm;
                devices.Cell(dr, 10).Value = p.CalculatedFlow;
                devices.Cell(dr, 11).Value = p.KEf > 0 ? p.KEf : "";
                devices.Cell(dr, 12).Value = p.CalculationOption;
                dr++;
            }
            devices.Columns().AdjustToContents(1, Math.Max(1, dr - 1));
            devices.SheetView.FreezeRows(1);

            wb.SaveAs(path);
        }
    }
}
