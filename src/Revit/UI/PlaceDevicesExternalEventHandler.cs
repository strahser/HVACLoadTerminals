using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using HVACLoadTerminals.Core.Models;
using HVACLoadTerminals.Revit.Services;

namespace HVACLoadTerminals.Revit.UI
{
    /// <summary>Pending write request from the modeless window to the API context.</summary>
    public class PlaceRequest
    {
        public List<DevicePlacement> Placements { get; set; } = new();
        /// <summary>roomId → level name (from the snapshot).</summary>
        public Dictionary<string, string> RoomLevels { get; set; }
            = new Dictionary<string, string>();
        public string DocumentTitle { get; set; } = "";
    }

    /// <summary>
    /// Executes the pending placement inside a VALID Revit API context. Raised via
    /// ExternalEvent from the modeless window — Revit is never blocked (plan C3.3).
    /// Idempotent: markers of exactly the rooms/systems being placed are replaced.
    /// </summary>
    public class PlaceDevicesExternalEventHandler : IExternalEventHandler
    {
        private readonly UIDocument _uiDoc;
        private PlaceRequest? _pending;

        public PlaceDevicesExternalEventHandler(UIDocument uiDoc)
        {
            _uiDoc = uiDoc ?? throw new ArgumentNullException(nameof(uiDoc));
        }

        public void SetPending(PlaceRequest request) => _pending = request;

        /// <summary>Status text for the window status bar (fired on UI thread).</summary>
        public event Action<string>? Completed;

        public void Execute(UIApplication app)
        {
            var request = _pending;
            _pending = null;

            if (request == null || request.Placements.Count == 0)
            {
                Completed?.Invoke("Нет размещений для записи");
                return;
            }

            var doc = app.ActiveUIDocument?.Document;
            if (doc == null)
            {
                Completed?.Invoke("Нет активного документа");
                return;
            }

            try
            {
                var placer = new RevitDevicePlacer(app.ActiveUIDocument);
                var markers = request.Placements
                    .Select(p => $"HLT|{request.DocumentTitle}|{p.RoomId}|{p.SystemName}")
                    .ToHashSet();

                using var tx = new Transaction(doc, "Стенд: запись приборов по снимку");
                tx.Start();

                int deleted = placer.DeleteMarkedInstancesExact(markers);

                var levelIndex = LevelIndex(doc);
                var placed =
                    new List<(DevicePlacement Placement, FamilyInstance Instance)>();
                placer.PlaceDevicesInTransaction(
                    request.Placements,
                    tx,
                    commentsFactory: p =>
                        $"HLT|{request.DocumentTitle}|{p.RoomId}|{p.SystemName}",
                    levelResolver: roomId =>
                        request.RoomLevels.TryGetValue(roomId, out var levelName) &&
                        levelIndex.TryGetValue(levelName, out var level)
                            ? level
                            : null,
                    instanceCreated: (p, instance) => placed.Add((p, instance)));

                // S3.2: «разместил → назначил систему» в той же транзакции.
                var assignment = new RevitSystemAssigner(doc).Assign(placed);

                tx.Commit();

                string systemsNote = assignment.Entries.Count > 0
                    ? "; системы: " + string.Join(", ", assignment.Entries.Select(e =>
                        $"{e.SystemName}×{e.ElementCount}"))
                    : "";
                Completed?.Invoke($"Записано {request.Placements.Count} приборов " +
                                  $"(заменено {deleted}){systemsNote}");
            }
            catch (Exception ex)
            {
                Completed?.Invoke("Ошибка записи: " + ex.Message);
            }
        }

        public string GetName() => "HLT: запись приборов по снимку";

        private static Dictionary<string, Level> LevelIndex(Document doc) =>
            new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .GroupBy(l => l.Name)
                .ToDictionary(g => g.Key, g => g.First());
    }
}
