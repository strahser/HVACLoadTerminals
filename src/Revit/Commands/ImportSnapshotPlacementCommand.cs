using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using HVACLoadTerminals.Core.Models.Snapshot;
using HVACLoadTerminals.Core.Services;
using HVACLoadTerminals.Infrastructure.Data;
using HVACLoadTerminals.Revit.Logging;
using HVACLoadTerminals.Revit.Services;

namespace HVACLoadTerminals.Revit.Commands
{
    /// <summary>
    /// Mass placement from a HeatLossRevit2 room snapshot (plan cards C3.1 + C3.2):
    /// load snapshot → auto loads → core placement (heating under windows, ceiling
    /// grid) → place family instances with idempotency markers
    /// HLT|&lt;DocumentTitle&gt;|&lt;roomId&gt;|&lt;systemName&gt; in Comments.
    /// Re-runs never duplicate: Skip keeps marked rooms, Replace deletes old marked
    /// instances first (owner decision 2026-08-22).
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class ImportSnapshotPlacementCommand : IExternalCommand
    {
        private const string MarkerPrefix = "HLT|";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uiDoc = commandData.Application.ActiveUIDocument;
            var doc = uiDoc.Document;

            try
            {
                return Run(uiDoc, doc);
            }
            catch (Exception ex)
            {
                message = ex.Message;
                HvacLogger.LogException("Snapshot placement command failed", ex);
                return Result.Failed;
            }
        }

        private Result Run(UIDocument uiDoc, Document doc)
        {
            // 1. Choose snapshot file.
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Выберите снимок помещений HeatLossRevit2",
                Filter = "Снимки помещений (*.json)|*.json|Все файлы|*.*"
            };
            if (dlg.ShowDialog() != true)
                return Result.Cancelled;

            RoomSnapshot snapshot;
            try
            {
                snapshot = new RoomSnapshotLoader().LoadFromFile(dlg.FileName);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Импорт снимка", "Не удалось прочитать снимок:\n" + ex.Message);
                return Result.Cancelled;
            }

            string snapshotTitle = snapshot.Metadata?.DocumentTitle ?? "";
            string documentTitle = !string.IsNullOrEmpty(snapshotTitle)
                ? Path.GetFileNameWithoutExtension(snapshotTitle)
                : doc.Title;

            // 2. Catalog from the model (three device classes).
            var catalog = new RevitFamilyCatalogProvider(doc).GetAllDevices();
            if (catalog.Count == 0)
            {
                TaskDialog.Show("Импорт снимка",
                    "В модели не найдено семейств приборов " +
                    "(воздухораспределители/вентиляционное оборудование/радиаторы).");
                return Result.Cancelled;
            }

            // 3. Build placements by the shared engine.
            var build = new SnapshotPlacementEngine().Build(snapshot, catalog);
            if (build.Placements.Count == 0)
            {
                TaskDialog.Show("Импорт снимка",
                    "Размещение не построено.\n\n" +
                    string.Join("\n", build.Warnings.Take(10)));
                return Result.Cancelled;
            }

            // 4. Idempotency: what is already placed?
            var placer = new RevitDevicePlacer(uiDoc);
            var existing = placer.CollectMarkers();
            string ourPrefix = MarkerPrefix + documentTitle + "|";
            var existingOurs = existing.Where(m => m.StartsWith(ourPrefix, StringComparison.Ordinal)).ToList();

            // 5. Mode dialog.
            var td = new TaskDialog("Расстановка по снимку")
            {
                MainInstruction = $"Помещений: {build.RoomsTotal}, приборов к размещению: {build.Placements.Count}",
                MainContent =
                    $"Уровней: {snapshot.Rooms.Select(r => r.LevelName).Distinct().Count()}\n" +
                    $"Отопление: {build.Placements.Count(p => p.SystemName == "Отопление")}, " +
                    $"приток: {build.Placements.Count(p => p.SystemName == "Приток")}, " +
                    $"вытяжка: {build.Placements.Count(p => p.SystemName == "Вытяжка")}\n" +
                    $"Предупреждений: {build.Warnings.Count}\n\n" +
                    (existingOurs.Count > 0
                        ? $"В модели уже есть {existingOurs.Count} маркированных приборов этого документа."
                        : "Маркированных приборов этого документа в модели нет.")
            };
            td.AddCommandLink(TaskDialogCommandLinkId.CommandLink1,
                "Разместить, пропуская уже размещённые помещения");
            td.AddCommandLink(TaskDialogCommandLinkId.CommandLink2,
                "Заменить ранее размещённые (удалить маркерные и поставить новые)");
            td.AddCommandLink(TaskDialogCommandLinkId.CommandLink3,
                "Разместить всё (возможны дубли)");
            td.MainIcon = TaskDialogIcon.TaskDialogIconInformation;

            var choice = td.Show();
            if (choice == TaskDialogResult.CommandLink2 || choice == TaskDialogResult.CommandLink1
                || choice == TaskDialogResult.CommandLink3)
            {
                // continue below
            }
            else
            {
                return Result.Cancelled;
            }

            var toPlace = build.Placements.ToList();

            using var tx = new Transaction(doc, "Расстановка приборов по снимку");
            tx.Start();

            try
            {
                int deleted = 0;
                if (choice == TaskDialogResult.CommandLink2)
                {
                    deleted = placer.DeleteMarkedInstances(ourPrefix);
                }
                else if (choice == TaskDialogResult.CommandLink1 && existingOurs.Count > 0)
                {
                    var skipKeys = ParseMarkerKeys(existingOurs);
                    toPlace = build.Placements
                        .Where(p => !skipKeys.Contains((p.RoomId, p.SystemName)))
                        .ToList();
                }

                var levelByName = LevelIndex(doc);

                placer.PlaceDevicesInTransaction(
                    toPlace,
                    tx,
                    commentsFactory: p =>
                        $"{MarkerPrefix}{documentTitle}|{p.RoomId}|{p.SystemName}",
                    levelResolver: roomId =>
                    {
                        var room = snapshot.Rooms.FirstOrDefault(r => r.Id == roomId);
                        if (room != null &&
                            levelByName.TryGetValue(room.LevelName ?? "", out var lvl))
                            return lvl;
                        return null; // fallback: element id / first level inside placer
                    });

                tx.Commit();

                var report = "Готово.\n" +
                             $"Помещений в снимке: {build.RoomsTotal}\n" +
                             $"С контуром: {build.RoomsTotal - build.RoomsSkippedNoPolygon}\n" +
                             $"Размещено приборов: {toPlace.Count}\n" +
                             (deleted > 0 ? $"Удалено старых (замена): {deleted}\n" : "") +
                             $"Предупреждений: {build.Warnings.Count}";
                if (build.Warnings.Count > 0)
                    report += "\n\nПервые предупреждения:\n" +
                              string.Join("\n", build.Warnings.Take(8));

                TaskDialog.Show("Расстановка по снимку", report);
                HvacLogger.Info(
                    $"Snapshot placement: rooms={build.RoomsTotal} placed={toPlace.Count} " +
                    $"deleted={deleted} warnings={build.Warnings.Count}");
                return Result.Succeeded;
            }
            catch
            {
                tx.RollBack();
                throw;
            }
        }

        /// <summary>Keys (roomId, systemName) parsed from marker strings.</summary>
        private static HashSet<(string, string)> ParseMarkerKeys(IEnumerable<string> markers)
        {
            var set = new HashSet<(string, string)>();
            foreach (var m in markers)
            {
                var parts = m.Split('|');
                if (parts.Length >= 4)
                    set.Add((parts[2], parts[3]));
            }
            return set;
        }

        private static Dictionary<string, Level> LevelIndex(Document doc) =>
            new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .GroupBy(l => l.Name)
                .ToDictionary(g => g.Key, g => g.First());
    }
}
