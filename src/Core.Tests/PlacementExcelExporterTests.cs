using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using HVACLoadTerminals.Infrastructure.Presentation;
using HVACLoadTerminals.Infrastructure.Visualization;
using Xunit;

namespace HVACLoadTerminals.Core.Tests
{
    /// <summary>P6: Excel-выгрузка — заголовки листа «level_values» соответствуют
    /// схеме прототипа (df_device_result_columns), значения группируются верно.</summary>
    public class PlacementExcelExporterTests : IDisposable
    {
        private readonly string _path =
            Path.Combine(Path.GetTempPath(), Guid.NewGuid() + "-report.xlsx");

        public void Dispose()
        {
            if (File.Exists(_path)) File.Delete(_path);
        }

        [Fact]
        public void Save_Writes_Prototype_Columns_And_Groups_Rows()
        {
            var rows = new List<PlacementRow>
            {
                new()
                {
                    RoomId = "r1", RoomName = "101. Кабинет", LevelName = "Уровень 1",
                    Family = "Диффузор", TypeName = "Ø200", SystemName = "П1",
                    CalculatedFlow = 150, KEf = 0.75, CalculationOption = "device_area"
                },
                new()
                {
                    RoomId = "r1", RoomName = "101. Кабинет", LevelName = "Уровень 1",
                    Family = "Диффузор", TypeName = "Ø200", SystemName = "П1",
                    CalculatedFlow = 150, KEf = 0.75, CalculationOption = "device_area"
                },
                new()
                {
                    RoomId = "r1", RoomName = "101. Кабинет", LevelName = "Уровень 1",
                    Family = "Решётка", TypeName = "400x200", SystemName = "В1",
                    CalculatedFlow = 200, KEf = 0, CalculationOption = "minimum_terminals"
                }
            };

            PlacementExcelExporter.Save(_path, rows);

            using var wb = new XLWorkbook(_path);
            var sheet = wb.Worksheet("level_values");

            string[] expectedColumns =
            {
                "S_ID", "S_Number", "S_Name", "S_level",
                "family_device_name", "family_instance_name",
                "minimum_device_number", "flow_to_device_calculated",
                "system_name", "k_ef", "calculation_option"
            };
            for (int c = 0; c < expectedColumns.Length; c++)
                Assert.Equal(expectedColumns[c], sheet.Cell(1, c + 1).GetString());

            // П1 (2 прибора) + В1 (1 прибор) → две строки сводки.
            // Ordinal-сортировка: «В1» (U+0412) раньше «П1» (U+041F).
            Assert.Equal(3, sheet.LastRowUsed().RowNumber());
            Assert.Equal("r1", sheet.Cell(2, 1).GetString());
            Assert.Equal("В1", sheet.Cell(2, 9).GetString());
            Assert.Equal(1, sheet.Cell(2, 7).GetDouble());       // minimum_device_number
            Assert.Equal("", sheet.Cell(2, 10).GetString());     // k_ef неприменим

            Assert.Equal("П1", sheet.Cell(3, 9).GetString());
            Assert.Equal(2, sheet.Cell(3, 7).GetDouble());       // minimum_device_number
            Assert.Equal(150, sheet.Cell(3, 8).GetDouble(), 1);  // flow per device
            Assert.Equal(0.75, sheet.Cell(3, 10).GetDouble(), 2);
            Assert.Equal("device_area", sheet.Cell(3, 11).GetString());

            // Лист «Приборы»: по-приборные строки (последняя — В1 с расходом 200).
            var devices = wb.Worksheet("Приборы");
            Assert.Equal(4, devices.LastRowUsed().RowNumber()); // header + 3
            Assert.Equal(200, devices.Cell(4, 10).GetDouble(), 1);
            Assert.Equal("В1", devices.Cell(4, 4).GetString());
            Assert.Equal("device_area", devices.Cell(2, 12).GetString());
        }
    }
}
