using System;
using System.Collections.Generic;
using System.Linq;
using HVACLoadTerminals.Core.Models;
using HVACLoadTerminals.Core.Models.Snapshot;
using HVACLoadTerminals.Core.Services;
using HVACLoadTerminals.Infrastructure.Data;

namespace HVACLoadTerminals.Infrastructure.Presentation
{
    /// <summary>Aggregated workspace state pushed to hosts via StateChanged.</summary>
    public class WorkspaceState
    {
        public IReadOnlyList<RoomRow> Rooms { get; set; } = Array.Empty<RoomRow>();
        public IReadOnlyList<PlacementRow> Placements { get; set; }
            = Array.Empty<PlacementRow>();
        public IReadOnlyList<string> Levels { get; set; } = Array.Empty<string>();
        public string Status { get; set; } = "";
        public int TotalDevices { get; set; }
        public int HeatingCount { get; set; }
        public int SupplyCount { get; set; }
        public int ExhaustCount { get; set; }
        public double ElapsedMs { get; set; }

        /// <summary>True when this state comes from Calculate (placements valid).</summary>
        public bool IsCalculation { get; set; }
    }

    /// <summary>
    /// Snapshot workspace presenter — plan card C2.3. Pure C# (Core + Infrastructure),
    /// NO WPF and NO Revit dependencies: the same instance drives the standalone App
    /// window and the modeless Revit window. Hosts subscribe to <see cref="StateChanged"/>
    /// and rebind their UI.
    /// </summary>
    public class SnapshotWorkspacePresenter
    {
        private readonly LoadsEstimatorService _estimator = new();
        private readonly HeatingPlacementService _heatingService = new();
        private readonly CeilingPlacementService _ceilingService = new();
        private readonly GrilleSizingService _grilleService = new();
        private readonly RoomSnapshotLoader _loader = new();

        private RoomSnapshot? _snapshot;
        private string _snapshotPath = "";
                private List<PlacementRow> _lastPlacementRows = new List<PlacementRow>();

        /// <summary>Current snapshot for hosts that need geometry (OxyPlot etc.).</summary>
        public RoomSnapshot? CurrentSnapshot => _snapshot;
        public string SnapshotPath => _snapshotPath;

        // ---- Options (plain fields; hosts bind their own controls to these) ----

        /// <summary>Owner requirement: device length ≥ this share of window width.</summary>
        public double MinWindowLengthRatio { get; set; } = 0.6;

        public CeilingCountRule SupplyRule { get; set; } = CeilingCountRule.Auto;
        public CeilingCountRule ExhaustRule { get; set; } = CeilingCountRule.ByFlow;
        public int FixedSupplyCount { get; set; } = 2;

        // ---- U2.1: mass placement patterns (owner defaults: supply = long side,
        // exhaust = short side, single device in the centre) ----

        public WallPattern SupplyPattern { get; set; } = WallPattern.LongSide;
        public WallPattern ExhaustPattern { get; set; } = WallPattern.ShortSide;
        public SingleRule SingleDeviceRule { get; set; } = SingleRule.Center;

        /// <summary>For hosts binding a ComboBox of wall patterns.</summary>
        public WallPattern[] WallPatterns { get; } =
            Enum.GetValues(typeof(WallPattern)).Cast<WallPattern>().ToArray();

        /// <summary>For hosts binding a ComboBox of single-device rules.</summary>
        public SingleRule[] SingleRules { get; } =
            Enum.GetValues(typeof(SingleRule)).Cast<SingleRule>().ToArray();

        /// <summary>Wall edges chosen by the patterns of the last Calculate —
        /// hosts highlight them on the plan.</summary>
        public IReadOnlyList<PatternEdge> LastPatternEdges { get; private set; }
            = Array.Empty<PatternEdge>();

        /// <summary>Grille sizing velocity, m/s.</summary>
        public double GrilleVelocityMs { get; set; } = 2.0;

        /// <summary>Auto-recalculate after every load edit (debounced).</summary>
        public bool LiveRecalc { get; set; } = true;

        /// <summary>For hosts binding a ComboBox of count rules.</summary>
        public CeilingCountRule[] CountRules { get; } =
            Enum.GetValues(typeof(CeilingCountRule)).Cast<CeilingCountRule>().ToArray();

        /// <summary>Persistent collection — hosts bind to this instance once;
        /// it is mutated in place on every load.</summary>
        public System.Collections.ObjectModel.ObservableCollection<RoomRow> Rooms { get; } =
            new System.Collections.ObjectModel.ObservableCollection<RoomRow>();

        /// <summary>Raw placements of the last Calculate — for model writers.</summary>
        public IReadOnlyList<DevicePlacement> LastRawPlacements { get; private set; }
            = Array.Empty<DevicePlacement>();

        public event Action<WorkspaceState>? StateChanged;

        /// <summary>Host-provided sink for non-fatal errors (status bar + log).</summary>
        public Action<string>? ErrorSink { get; set; }

        private void SafeRaise(WorkspaceState state, string context)
        {
            try
            {
                StateChanged?.Invoke(state);
            }
            catch (Exception ex)
            {
                ErrorSink?.Invoke($"{context}: ошибка обновления UI — {ex.Message}");
            }
        }

        // ------------------------------------------------------------------
        // Operations
        // ------------------------------------------------------------------

        public void LoadSnapshot(string path)
        {
            _snapshot = _loader.LoadFromFile(path);
            _snapshotPath = path;
            RegenerateLoads();
        }

        public void RegenerateLoads()
        {
            if (_snapshot == null)
                throw new InvalidOperationException("Снимок не загружен");

            var loads = _estimator.EstimateAll(_snapshot);
            var byId = loads.GroupBy(l => l.RoomId).ToDictionary(g => g.Key, g => g.First());

            Rooms.Clear();
            foreach (var r in _snapshot.Rooms)
            {
                byId.TryGetValue(r.Id ?? "", out var l);
                Rooms.Add(new RoomRow
                {
                    RoomId = r.Id ?? "",
                    Number = r.Number ?? "",
                    Name = r.Name ?? "",
                    LevelName = r.LevelName ?? "",
                    Area = Math.Round(r.Area, 1),
                    IsCorner = r.IsCorner,
                    Purpose = l?.Purpose.ToString() ?? "",
                    HeatingW = Math.Round(l?.HeatingLoadW ?? 0),
                    Supply = Math.Round(l?.SupplyFlowM3h ?? 0),
                    Exhaust = Math.Round(l?.ExhaustFlowM3h ?? 0)
                });
            }

            HookLiveRecalc();
            RaiseState($"Снимок: {Rooms.Count} помещений, " +
                       $"ΣQ={loads.Sum(x => x.HeatingLoadW) / 1000:F0} кВт");
        }

        public void ApplyPurpose(Func<RoomRow, bool> rowFilter, string purpose)
        {
            int n = 0;
            foreach (var row in Rooms.Where(rowFilter))
            {
                row.Purpose = purpose;
                n++;
            }
            RaiseStatusOnly($"Назначение «{purpose}» применено к {n} помещениям");
        }

        // ---- U1.2: выбор комнат (чекбокс «Включено») ----

        /// <summary>Rooms currently checked for placement.</summary>
        public int CountIncluded() => Rooms.Count(r => r.IsIncluded);

        /// <summary>Bulk include/exclude over a row filter.</summary>
        public void SetIncluded(Func<RoomRow, bool> rowFilter, bool included)
        {
            foreach (var row in Rooms.Where(rowFilter))
                row.IsIncluded = included;
            RaiseStatusOnly($"Включено помещений: {CountIncluded()} из {Rooms.Count}");
        }

        /// <summary>«Включить уровень»: mark the whole level included.</summary>
        public void IncludeLevel(string levelName) =>
            SetIncluded(r => r.LevelName == levelName, true);

        /// <summary>«Только видимые»: selection becomes exactly the visible rows.</summary>
        public void IncludeOnlyVisible(Func<RoomRow, bool> visibleFilter)
        {
            foreach (var row in Rooms)
                row.IsIncluded = visibleFilter(row);
            RaiseStatusOnly($"Включено помещений: {CountIncluded()} из {Rooms.Count}");
        }

        /// <summary>Full recalculation of all three classes. Raises StateChanged.</summary>
        public WorkspaceState Calculate()
        {
            if (_snapshot == null || Rooms.Count == 0)
            {
                var empty = new WorkspaceState { Status = "Откройте снимок и сгенерируйте нагрузки" };
                StateChanged?.Invoke(empty);
                return empty;
            }

            var catalog = CatalogFactory.CreateDemo();
            var roomsById = new Dictionary<string, SnapshotRoom>();
            foreach (var room in _snapshot.Rooms)
                roomsById[room.Id] = room;

            var openingsByRoom = _snapshot.Openings
                .Where(o => o?.SpaceId != null)
                .GroupBy(o => o.SpaceId)
                .ToDictionary(g => g.Key, g => (IEnumerable<SnapshotOpening>)g.ToList());
            var wallsByRoom = _snapshot.Walls
                .Where(w => w?.SpaceId != null)
                .GroupBy(w => w.SpaceId)
                .ToDictionary(g => g.Key, g => (IEnumerable<SnapshotWall>)g.ToList());

            var placements = new List<DevicePlacement>();
            var warnings = new List<string>();
            var kefByKey = new Dictionary<string, double>();
            var patternEdges = new List<PatternEdge>();
            var sw = System.Diagnostics.Stopwatch.StartNew();

            var includedRows = Rooms.Where(r => r.IsIncluded).ToList();
            if (includedRows.Count == 0)
            {
                sw.Stop();
                LastRawPlacements = Array.Empty<DevicePlacement>();
                var none = new WorkspaceState
                {
                    Rooms = Rooms.ToList(),
                    Placements = new List<PlacementRow>(),
                    Levels = Rooms.Select(r => r.LevelName).Distinct().ToList(),
                    Status = "Не выбрано ни одного помещения",
                    IsCalculation = true
                };
                SafeRaise(none, "Обновление");
                return none;
            }

            foreach (var row in Rooms)
            {
                if (!row.IsIncluded)
                    continue;
                if (!roomsById.TryGetValue(row.RoomId, out var snapRoom))
                    continue;
                var polygon = snapRoom.ToPolygon();
                if (polygon == null)
                {
                    row.Warning = "нет контура";
                    continue;
                }
                openingsByRoom.TryGetValue(row.RoomId, out var openings);
                wallsByRoom.TryGetValue(row.RoomId, out var walls);

                var roomWarnings = new List<string>();
                double roomAreaM2 = row.Area > 0 ? row.Area : polygon.Area * LengthUnitConverter.MmPerFoot / 1_000_000.0;

                if (row.HeatingW > 0)
                {
                    var res = _heatingService.PlaceForRoom(
                        snapRoom, polygon, openings, walls,
                        row.HeatingW, catalog,
                        new HeatingPlacementOptions
                        {
                            MinLengthToWindowRatio = MinWindowLengthRatio
                        });
                    placements.AddRange(res.Placements);
                    roomWarnings.AddRange(res.Warnings);
                }

                if (row.Supply > 0)
                {
                    var res = _ceilingService.PlaceForRoom(
                        row.RoomId, polygon, row.Supply, roomAreaM2,
                        HVACSystemType.Supply, catalog, "Приток",
                        new CeilingPlacementOptions
                        {
                            CountRule = SupplyRule,
                            FixedCount = FixedSupplyCount,
                            Pattern = SupplyPattern,
                            SingleRule = SingleDeviceRule
                        });
                    placements.AddRange(res.Placements);
                    StoreKef(kefByKey, res, row.Supply);
                    AddPatternEdge(patternEdges, res, snapRoom, "Приток");
                    roomWarnings.AddRange(res.Warnings);
                }

                if (row.Exhaust > 0)
                {
                    var res = _ceilingService.PlaceForRoom(
                        row.RoomId, polygon, row.Exhaust, roomAreaM2,
                        HVACSystemType.Exhaust, catalog, "Вытяжка",
                        new CeilingPlacementOptions
                        {
                            CountRule = ExhaustRule,
                            Pattern = ExhaustPattern,
                            SingleRule = SingleDeviceRule
                        });
                    placements.AddRange(res.Placements);
                    StoreKef(kefByKey, res, row.Exhaust);
                    AddPatternEdge(patternEdges, res, snapRoom, "Вытяжка");
                    roomWarnings.AddRange(res.Warnings);

                    // Grille dimensions from the equivalent diameter (C1.5).
                    if (res.Placements.Count > 0)
                    {
                        var size = _grilleService.Size(
                            row.Exhaust,
                            new GrilleSizingOptions { VelocityMs = GrilleVelocityMs });
                        row.Warning = size.Grilles.Count == 1
                            ? $"решётка {size.Grilles[0].LengthMm:F0}×{size.Grilles[0].HeightMm:F0}"
                            : $"{size.Grilles.Count} решётки по " +
                              $"{size.Grilles[0].LengthMm:F0}×{size.Grilles[0].HeightMm:F0}";
                    }
                }
                else
                {
                    row.Warning = "";
                }

                row.Warning = CombineExhaustInfo(roomWarnings, row.Warning);
                AddWarnings(warnings, $"{row.Number}. {row.Name}", roomWarnings);
            }

            sw.Stop();

            LastRawPlacements = placements;
            LastPatternEdges = patternEdges;
            var state = BuildState(placements, warnings, kefByKey, sw.Elapsed.TotalMilliseconds);
            SafeRaise(state, "Обновление");
            return state;
        }

        // ------------------------------------------------------------------
        // Project persistence (round-trip, card C2.2)
        // ------------------------------------------------------------------

        private const string ProjectFilter =
            "Проект размещения (*.hvacproj.json)|*.hvacproj.json|Все файлы|*.*";

        public void SaveProject(string path)
        {
            var dto = new ProjectDto
            {
                SnapshotPath = _snapshotPath,
                Rooms = Rooms.ToList(),
                Placements = _lastPlacementRows,
                SupplyPattern = SupplyPattern,
                ExhaustPattern = ExhaustPattern,
                SingleRule = SingleDeviceRule
            };
            System.IO.File.WriteAllText(path,
                Newtonsoft.Json.JsonConvert.SerializeObject(dto,
                    Newtonsoft.Json.Formatting.Indented,
                    new Newtonsoft.Json.Converters.StringEnumConverter()));
        }

        public void LoadProject(string path)
        {
            string json = System.IO.File.ReadAllText(path);
            var dto = Newtonsoft.Json.JsonConvert.DeserializeObject<ProjectDto>(json)
                ?? throw new System.IO.InvalidDataException("Файл проекта повреждён");

            _snapshotPath = dto.SnapshotPath ?? "";
            if (System.IO.File.Exists(_snapshotPath))
                _snapshot = _loader.LoadFromFile(_snapshotPath);

            // U2.1: patterns round-trip; legacy files keep the owner defaults.
            SupplyPattern = dto.SupplyPattern ?? WallPattern.LongSide;
            ExhaustPattern = dto.ExhaustPattern ?? WallPattern.ShortSide;
            SingleDeviceRule = dto.SingleRule ?? SingleRule.Center;

            Rooms.Clear();
            foreach (var row in dto.Rooms ?? new List<RoomRow>())
                Rooms.Add(row);
            HookLiveRecalc();

            var state = new WorkspaceState
            {
                Rooms = Rooms.ToList(),
                Placements = _lastPlacementRows = dto.Placements ?? new List<PlacementRow>(),
                Levels = Rooms.Select(r => r.LevelName).Distinct().ToList(),
                Status = $"Проект загружен: {Rooms.Count} помещений, " +
                         $"{_lastPlacementRows.Count} приборов"
            };
            SafeRaise(state, "Обновление");
        }

        private class ProjectDto
        {
            public string? SnapshotPath { get; set; }
            public List<RoomRow>? Rooms { get; set; }
            public List<PlacementRow>? Placements { get; set; }

            // U2.1: mass placement patterns (nullable → legacy files keep defaults)
            public WallPattern? SupplyPattern { get; set; }
            public WallPattern? ExhaustPattern { get; set; }
            public SingleRule? SingleRule { get; set; }
        }

        private static void AddPatternEdge(
            ICollection<PatternEdge> sink,
            CeilingPlacementResult res,
            SnapshotRoom room,
            string systemName)
        {
            if (res.SelectedEdge == null)
                return;
            sink.Add(new PatternEdge
            {
                LevelName = room.LevelName ?? "",
                SystemName = systemName,
                Start = res.SelectedEdge.Start,
                End = res.SelectedEdge.End
            });
        }

        private static void StoreKef(
            Dictionary<string, double> sink,
            CeilingPlacementResult res,
            double roomFlow)
        {
            if (res.Placements.Count == 0) return;
            var device = res.Placements[0].Device;
            if (device.MaxFlowRate <= 0) return;
            double perDevice = roomFlow / res.Placements.Count;
            double k = LoadFactorCalculator.LoadFactor(perDevice, device.MaxFlowRate);
            string key = res.Placements[0].RoomId + "|" + res.Placements[0].SystemName;
            sink[key] = Math.Round(k, 2);
        }

        /// <summary>Scene JSON for HTML preview of the current placements.</summary>
        public WorkspaceState BuildState(
            List<DevicePlacement> placements, List<string> warnings,
            Dictionary<string, double> kefByKey, double elapsedMs)
        {
            var rows = ToRows(placements, kefByKey);
            _lastPlacementRows = rows;
            return new WorkspaceState
            {
                Rooms = Rooms.ToList(),
                Placements = rows,
                Levels = Rooms.Select(r => r.LevelName).Distinct().ToList(),
                Status = $"Выбрано {CountIncluded()} из {Rooms.Count} · " +
                         $"Размещение: {placements.Count} приборов за {elapsedMs:F0} мс, " +
                         $"предупреждений: {warnings.Count}",
                TotalDevices = placements.Count,
                HeatingCount = placements.Count(p => p.SystemName == "Отопление"),
                SupplyCount = placements.Count(p => p.SystemName == "Приток"),
                ExhaustCount = placements.Count(p => p.SystemName == "Вытяжка"),
                ElapsedMs = elapsedMs,
                IsCalculation = true
            };
        }

        private void RaiseState(string status)
        {
            StateChanged?.Invoke(new WorkspaceState
            {
                Rooms = Rooms.ToList(),
                Status = status
            });
        }

        private void RaiseStatusOnly(string status) =>
            RaiseState(status);

        private static string CombineExhaustInfo(List<string> warnings, string grilleInfo) =>
            warnings.Count == 0
                ? grilleInfo
                : grilleInfo.Length == 0
                    ? string.Join("; ", warnings.Distinct())
                    : grilleInfo + "; " + string.Join("; ", warnings.Distinct());

        private static void AddWarnings(
            ICollection<string> sink, string roomLabel, IEnumerable<string> roomWarnings)
        {
            foreach (var w in roomWarnings.Distinct())
                sink.Add($"{roomLabel}: {w}");
        }

        private static List<PlacementRow> ToRows(
            IEnumerable<DevicePlacement> placements, Dictionary<string, double> kefByKey)
        {
            var result = new List<PlacementRow>();
            foreach (var p in placements)
            {
                string key = p.RoomId + "|" + p.SystemName;
                kefByKey.TryGetValue(key, out double k);
                result.Add(new PlacementRow
                {
                    RoomName = p.RoomId,
                    Family = p.Device.FamilyName,
                    TypeName = p.Device.TypeName,
                    SystemName = p.SystemName,
                    X = Math.Round(p.Position.X, 3),
                    Y = Math.Round(p.Position.Y, 3),
                    RotationDeg = Math.Round(p.Rotation * 180.0 / Math.PI, 1),
                    KEf = k
                });
            }
            return result;
        }

        private void HookLiveRecalc()
        {
            foreach (var row in Rooms)
            {
                row.PropertyChanged += (_, e) =>
                {
                    if (!LiveRecalc) return;
                    if (e.PropertyName == nameof(RoomRow.HeatingW) ||
                        e.PropertyName == nameof(RoomRow.Supply) ||
                        e.PropertyName == nameof(RoomRow.Exhaust))
                    {
                        try
                        {
                            Calculate();
                        }
                        catch (Exception ex)
                        {
                            ErrorSink?.Invoke("Живой пересчёт: " + ex.Message);
                        }
                    }
                };
            }
        }
    }
}
