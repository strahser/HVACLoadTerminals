using System;
using System.Linq;
using HVACLoadTerminals.App.Commands;
using HVACLoadTerminals.Infrastructure.Presentation;
using HVACLoadTerminals.Infrastructure.Visualization;

namespace HVACLoadTerminals.App.ViewModels
{
    /// <summary>
    /// Экспортные методы MainViewModel: HTML, задание, отчёт, Excel.
    /// </summary>
    partial class MainViewModel
    {
        private void ExportHtml()
        {
            if (Workspace.LastRawPlacements.Count == 0 || Workspace.CurrentSnapshot == null)
            {
                StatusMessage = "Рассчитайте размещение перед экспортом HTML";
                return;
            }

            try
            {
                string title = $"Расстановка — {SelectedLevel}";

                var cmd = new OpenHtmlPreviewCommand(
                    getSceneJson: () =>
                    {
                        CalculateSafe();
                        return PlacementSceneSerializer.ToJson(
                            Workspace.BuildPlacementResults(), title);
                    },
                    report: msg => StatusMessage = msg,
                    title: title,
                    modal: false);

                cmd.Execute(null);
                StatusMessage = "HTML-превью открыт";
            }
            catch (Exception ex)
            {
                StatusMessage = "Ошибка экспорта HTML: " + ex.Message;
            }
        }

        private void ExportTask()
        {
            if (Workspace.LastRawPlacements.Count == 0 ||
                Workspace.CurrentSnapshot == null)
            {
                StatusMessage = "Рассчитайте размещение перед экспортом задания";
                return;
            }

            try
            {
                string snapshotName = System.IO.Path.GetFileNameWithoutExtension(
                    string.IsNullOrEmpty(Workspace.SnapshotPath)
                        ? "placement"
                        : Workspace.SnapshotPath);
                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "Экспорт задания JSON (формат прототипа)",
                    Filter = "Задание (*.json)|*.json|Все файлы|*.*",
                    FileName = snapshotName + "_task.json"
                };
                if (dlg.ShowDialog() != true)
                    return;

                var levelOffsets = Workspace.CurrentSnapshot.Rooms
                    .Where(r => r.Id != null)
                    .GroupBy(r => r.Id)
                    .ToDictionary(g => g.Key, g => g.First().LevelElevation);
                PlacementTaskExporter.Save(
                    dlg.FileName,
                    PlacementTaskExporter.Build(
                        Workspace.LastRawPlacements, levelOffsets));

                StatusMessage = "Задание сохранено: " + dlg.FileName;
                AppLogger.Info("Placement task exported: " + dlg.FileName);
            }
            catch (Exception ex)
            {
                StatusMessage = "Ошибка экспорта задания: " + ex.Message;
                AppLogger.Error("ExportTask failed", ex);
            }
        }

        /// <summary>M3.2: самодостаточный HTML-отчёт по уровню.</summary>
        private void ExportLevelReport()
        {
            if (Workspace.LastRawPlacements.Count == 0 || Workspace.CurrentSnapshot == null)
            {
                StatusMessage = "Рассчитайте размещение перед экспортом отчёта";
                return;
            }

            try
            {
                string level = SelectedLevel;
                var results = Workspace.BuildPlacementResults(
                    level.Length == 0 ? null : level);
                if (results.Count == 0)
                {
                    StatusMessage = $"На уровне «{SelectedLevel}» нет приборов";
                    return;
                }

                string json = PlacementSceneSerializer.ToJson(
                    results, $"Отчёт — {SelectedLevel}");

                var rows = Placements
                    .Where(p => level.Length == 0 || p.LevelName == level)
                    .ToList();
                var reportData = new
                {
                    Level = SelectedLevel,
                    Summary = Workspace.LastSystemSummaries.Select(s => new
                    {
                        s.Name,
                        s.RoomCount,
                        s.DeviceCount,
                        s.TotalFlowM3h,
                        s.AvgKef
                    }).ToList(),
                    Formulas = Workspace.LastSystemSummaries
                        .Where(s => !string.IsNullOrEmpty(s.FormulaText))
                        .Select(s => $"{s.Name}: {s.FormulaText}")
                        .ToList(),
                    Devices = rows.Select(p => new
                    {
                        Room = p.RoomName,
                        p.LevelName,
                        p.Family,
                        p.TypeName,
                        System = p.SystemName,
                        Flow = p.CalculatedFlow,
                        p.MountHeightMm,
                        p.X,
                        p.Y,
                        KefText = p.KEfText,
                        Option = p.CalculationOption
                    }).ToList()
                };

                string snapshotName = System.IO.Path.GetFileNameWithoutExtension(
                    string.IsNullOrEmpty(Workspace.SnapshotPath)
                        ? "placement"
                        : Workspace.SnapshotPath);
                string fileLevel = level.Length == 0
                    ? "все_уровни"
                    : MakeSafeFileName(level);
                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "Экспорт HTML-отчёта уровня",
                    Filter = "HTML-отчёт (*.html)|*.html|Все файлы|*.*",
                    FileName = $"{snapshotName}_{fileLevel}_отчёт.html"
                };
                if (dlg.ShowDialog() != true)
                    return;

                string html = HtmlSceneExporter.BuildReportHtml(
                    $"Отчёт — {SelectedLevel}", json, reportData);
                System.IO.File.WriteAllText(dlg.FileName, html,
                    new System.Text.UTF8Encoding(false));

                StatusMessage = $"Отчёт сохранён: {dlg.FileName} ({rows.Count} приборов)";
                AppLogger.Info("Level report exported: " + dlg.FileName);
            }
            catch (Exception ex)
            {
                StatusMessage = "Ошибка экспорта отчёта: " + ex.Message;
                AppLogger.Error("ExportLevelReport failed", ex);
            }
        }

        private static string MakeSafeFileName(string name)
        {
            foreach (char c in System.IO.Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name.Replace(' ', '_');
        }

        /// <summary>P6: выгрузка результатов в Excel.</summary>
        private void ExportExcel()
        {
            if (Placements.Count == 0)
            {
                StatusMessage = "Рассчитайте размещение перед экспортом в Excel";
                return;
            }

            try
            {
                string snapshotName = System.IO.Path.GetFileNameWithoutExtension(
                    string.IsNullOrEmpty(Workspace.SnapshotPath)
                        ? "placement"
                        : Workspace.SnapshotPath);
                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "Экспорт результатов в Excel",
                    Filter = "Книга Excel (*.xlsx)|*.xlsx|Все файлы|*.*",
                    FileName = snapshotName + "_отчёт.xlsx"
                };
                if (dlg.ShowDialog() != true)
                    return;

                PlacementExcelExporter.Save(dlg.FileName, Placements.ToList());
                StatusMessage = $"Excel сохранён: {dlg.FileName} ({Placements.Count} приборов)";
                AppLogger.Info("Placement excel exported: " + dlg.FileName);
            }
            catch (Exception ex)
            {
                StatusMessage = "Ошибка экспорта Excel: " + ex.Message;
                AppLogger.Error("ExportExcel failed", ex);
            }
        }
    }
}
