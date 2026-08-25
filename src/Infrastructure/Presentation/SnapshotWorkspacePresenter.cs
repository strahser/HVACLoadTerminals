using System;
using System.Collections.Generic;
using System.Linq;
using HVACLoadTerminals.Core.Interfaces;
using HVACLoadTerminals.Core.Models;
using HVACLoadTerminals.Core.Models.Snapshot;
using HVACLoadTerminals.Core.Services;
using HVACLoadTerminals.Infrastructure.Data;

namespace HVACLoadTerminals.Infrastructure.Presentation
{
    /// <summary>U3.1: отложенный однократный вызов — коалесинг правок живого
    /// пересчёта (правка Q на каждый символ больше не пересчитывает всё).</summary>
    public interface ILiveRecalcScheduler
    {
        /// <summary>Отменить предыдущий отложенный вызов (если был).</summary>
        void Cancel();

        /// <summary>Запланировать <paramref name="callback"/> через <paramref name="delay"/>.</summary>
        void Schedule(TimeSpan delay, Action callback);
    }

    /// <summary>Дефолтный планировщик на DispatcherTimer: тики приходят в UI-потоке
    /// хоста, поэтому Calculate безопасно мутирует коллекции, связанные с таблицами.</summary>
    public sealed class DispatcherTimerLiveRecalcScheduler : ILiveRecalcScheduler
    {
        private readonly System.Windows.Threading.DispatcherTimer _timer =
            new System.Windows.Threading.DispatcherTimer();
        private Action? _callback;

        public void Cancel()
        {
            _callback = null;
            _timer.Stop();
        }

        public void Schedule(TimeSpan delay, Action callback)
        {
            _callback = callback;
            _timer.Stop();
            _timer.Interval = delay;
            _timer.Tick -= OnTick;
            _timer.Tick += OnTick;
            _timer.Start();
        }

        private void OnTick(object? sender, EventArgs e)
        {
            _timer.Stop();
            Action? callback = _callback;
            _callback = null;
            callback?.Invoke();
        }
    }

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

        // ---- U2.2: внешний каталог приборов ----

        /// <summary>
        /// Источник каталога для Calculate (JSON-файл и т.п.). Null → встроенный
        /// демо-каталог; недоступный/битый внешний каталог → fallback на демо
        /// с сообщением через ErrorSink.
        /// </summary>
        public ITerminalCatalogRepository? CatalogRepository { get; set; }

        /// <summary>Путь файла каталога из репозитория (для сохранения в проект).</summary>
        public string CatalogPath => (CatalogRepository as JsonCatalogRepository)?.FilePath ?? "";

        /// <summary>Версия последнего прочитанного каталога (0 — неизвестна).</summary>
        public int CatalogVersion => (CatalogRepository as JsonCatalogRepository)?.Version ?? 0;

        /// <summary>Каталог последнего Calculate — доступен хостам после расчёта.</summary>
        public IReadOnlyList<TerminalDevice>? LastUsedCatalog { get; private set; }

        /// <summary>Подключить JSON-каталог по пути (без немедленного чтения).</summary>
        public void UseJsonCatalog(string path) =>
            CatalogRepository = new JsonCatalogRepository(path);

        // ---- Options (validated properties; hosts bind their own controls to these) ----

        // U3.1: числовые поля валидируются с сообщением через ErrorSink —
        // молчаливые Math.Max/клампы убраны; при невалидном вводе значение не меняется.

        /// <summary>Owner requirement: device length ≥ this share of window width (0–1).</summary>
        private double _minWindowLengthRatio = 0.6;
        public double MinWindowLengthRatio
        {
            get => _minWindowLengthRatio;
            set
            {
                if (double.IsNaN(value) || value < 0.0 || value > 1.0)
                {
                    ErrorSink?.Invoke($"Доля от окна должна быть в диапазоне 0–1 " +
                                      $"(получено {value:F2}) — значение не изменено");
                    return;
                }
                _minWindowLengthRatio = value;
            }
        }

        public CeilingCountRule SupplyRule { get; set; } = CeilingCountRule.Auto;
        public CeilingCountRule ExhaustRule { get; set; } = CeilingCountRule.ByFlow;

        private int _fixedSupplyCount = 2;
        /// <summary>Фиксированное количество приборов притока для правила Fixed (≥ 1).</summary>
        public int FixedSupplyCount
        {
            get => _fixedSupplyCount;
            set
            {
                if (value < 1)
                {
                    ErrorSink?.Invoke($"Количество приборов притока N должно быть ≥ 1 " +
                                      $"(получено {value}) — значение не изменено");
                    return;
                }
                _fixedSupplyCount = value;
            }
        }

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

        private double _grilleVelocityMs = 2.0;
        /// <summary>Grille sizing velocity, m/s (must be positive).</summary>
        public double GrilleVelocityMs
        {
            get => _grilleVelocityMs;
            set
            {
                if (double.IsNaN(value) || value <= 0)
                {
                    ErrorSink?.Invoke($"Скорость v в решётке должна быть больше 0 м/с " +
                                      $"(получено {value:F2}) — значение не изменено");
                    return;
                }
                _grilleVelocityMs = value;
            }
        }

        /// <summary>Auto-recalculate after load edits — debounced by
        /// <see cref="LiveRecalcDebounceMs"/> after the LAST edit (U3.1: раньше
        /// комментарий «debounced» был лживым — пересчёт шёл на каждый символ).</summary>
        public bool LiveRecalc { get; set; } = true;

        /// <summary>U3.1: пауза коалесинга правок перед живым пересчётом, мс.</summary>
        public int LiveRecalcDebounceMs { get; set; } = 300;

        /// <summary>Планировщик отложенного пересчёта (тесты подменяют фейком;
        /// по умолчанию — DispatcherTimer UI-потока хоста).</summary>
        public ILiveRecalcScheduler LiveRecalcScheduler { get; set; } =
            new DispatcherTimerLiveRecalcScheduler();

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

        /// <summary>M2.1: сводные по системам последнего расчёта/загрузки проекта
        /// (панель свойств системы).</summary>
        public IReadOnlyList<SystemSummary> LastSystemSummaries { get; private set; }
            = Array.Empty<SystemSummary>();

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

        // ---- S1.1: именованные системы помещения ----

        /// <summary>Пустой список систем → автодефолт П1/В1 из оценки нагрузок;
        /// заданный пользователем список не трогается (обратная совместимость).</summary>
        public void EnsureDefaultSystems(RoomRow row)
        {
            var systems = row.Systems ??= new List<SystemRow>();
            if (systems.Count > 0)
                return;
            if (row.Supply > 0)
                systems.Add(new SystemRow
                {
                    Name = "П1",
                    Type = HVACSystemType.Supply,
                    FlowM3h = Math.Round(row.Supply, 1)
                });
            if (row.Exhaust > 0)
                systems.Add(new SystemRow
                {
                    Name = "В1",
                    Type = HVACSystemType.Exhaust,
                    FlowM3h = Math.Round(row.Exhaust, 1)
                });
        }

        /// <summary>Ошибки списка систем комнаты: пустое имя, дубликат имени,
        /// неположительный расход. Пустой список — валиден (дефолт будет построен).</summary>
        public IReadOnlyList<string> GetSystemErrors(RoomRow row)
        {
            var errors = new List<string>();
            var label = $"{row.Number}. {row.Name}".Trim(' ', '.');
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var s in row.Systems ?? new List<SystemRow>())
            {
                string name = (s.Name ?? "").Trim();
                if (name.Length == 0)
                    errors.Add($"{label}: система с пустым именем");
                else if (!seen.Add(name))
                    errors.Add($"{label}: дубликат имени системы «{name}»");
                if (s.FlowM3h <= 0)
                    errors.Add($"{label}: расход системы «{name}» должен быть > 0 " +
                               $"(сейчас {s.FlowM3h:F1})");
            }
            return errors;
        }

        private void ReportSystemErrors(RoomRow row)
        {
            foreach (var error in GetSystemErrors(row))
                ErrorSink?.Invoke(error);
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

            var catalog = ResolveCatalog();
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
                EnsureDefaultSystems(row);
                ReportSystemErrors(row);
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
                var grilleInfo = new List<string>();
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

                // S2.1: расстановка по КАЖДОЙ именованной системе комнаты;
                // EnsureDefaultSystems уже построил П1/В1 для пустых списков.
                double roomHeightMm = snapRoom.UpperLimitOffset > 0
                    ? LengthUnitConverter.UnitsToMm(snapRoom.UpperLimitOffset)
                    : 0; // M2.2: высота помещения для расчёта отметки установки
                foreach (var system in row.Systems ?? new List<SystemRow>())
                {
                    if (!system.IsIncluded || system.FlowM3h <= 0)
                        continue;
                    // M2.1: опции панели свойств системы (оверрайды → глобальные),
                    // каталог сужается до закреплённого типоразмера, если задан.
                    var options = SystemCeilingOptions(system);
                    options.RoomHeightMm = roomHeightMm;
                    var systemCatalog = CatalogForSystem(catalog, system);
                    if (system.Type == HVACSystemType.Supply)
                    {
                        var res = _ceilingService.PlaceForRoom(
                            row.RoomId, polygon, system.FlowM3h, roomAreaM2,
                            HVACSystemType.Supply, systemCatalog, system.Name, options);
                        placements.AddRange(res.Placements);
                        StoreKef(kefByKey, res, system.FlowM3h);
                        AddPatternEdge(patternEdges, res, snapRoom, system.Name);
                        roomWarnings.AddRange(res.Warnings);
                    }
                    else if (system.Type == HVACSystemType.Exhaust)
                    {
                        var res = _ceilingService.PlaceForRoom(
                            row.RoomId, polygon, system.FlowM3h, roomAreaM2,
                            HVACSystemType.Exhaust, systemCatalog, system.Name, options);
                        placements.AddRange(res.Placements);
                        StoreKef(kefByKey, res, system.FlowM3h);
                        AddPatternEdge(patternEdges, res, snapRoom, system.Name);
                        roomWarnings.AddRange(res.Warnings);

                        // Grille dimensions from the equivalent diameter (C1.5),
                        // sized per exhaust system.
                        if (res.Placements.Count > 0)
                        {
                            var size = _grilleService.Size(
                                system.FlowM3h,
                                new GrilleSizingOptions { VelocityMs = GrilleVelocityMs });
                            grilleInfo.Add(
                                $"{system.Name}: " + (size.Grilles.Count == 1
                                    ? $"решётка {size.Grilles[0].LengthMm:F0}×{size.Grilles[0].HeightMm:F0}"
                                    : $"{size.Grilles.Count} решётки по " +
                                      $"{size.Grilles[0].LengthMm:F0}×{size.Grilles[0].HeightMm:F0}"));
                        }
                    }
                }

                row.Warning = string.Join("; ", grilleInfo);
                if (roomWarnings.Count > 0)
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
        // U2.2: источник каталога
        // ------------------------------------------------------------------

        /// <summary>Внешний каталог; сбой чтения → демо-каталог + ErrorSink
        /// (рабочий расчёт не блокируется, файл не трогается).</summary>
        private IReadOnlyList<TerminalDevice> ResolveCatalog()
        {
            var repo = CatalogRepository;
            if (repo == null)
                return CatalogFactory.CreateDemo();
            try
            {
                var devices = repo.GetAllDevices();
                if (devices.Count > 0)
                {
                    LastUsedCatalog = devices;
                    return devices;
                }
                return FallbackCatalog(
                    $"Каталог приборов пуст ({Describe(repo)}).");
            }
            catch (Exception ex)
            {
                return FallbackCatalog(ex.Message);
            }
        }

        private IReadOnlyList<TerminalDevice> FallbackCatalog(string? reason = null)
        {
            ErrorSink?.Invoke(reason != null
                ? $"{reason}\nИспользуется встроенный каталог приборов."
                : "Используется встроенный каталог приборов.");
            LastUsedCatalog = CatalogFactory.CreateDemo();
            return LastUsedCatalog;
        }

        private static string Describe(ITerminalCatalogRepository repo) =>
            repo is JsonCatalogRepository json ? json.FilePath : repo.GetType().Name;

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
                SingleRule = SingleDeviceRule,

                // U2.2: какой каталог использовал проект
                CatalogPath = CatalogPath,
                CatalogVersion = CatalogVersion
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

            // U2.2: проект хранит путь/версию каталога; файл проекта главнее
            // текущего подключения, если существует.
            string catalogNote = "";
            if (!string.IsNullOrWhiteSpace(dto.CatalogPath) &&
                !string.Equals(dto.CatalogPath, CatalogPath, StringComparison.OrdinalIgnoreCase))
            {
                if (System.IO.File.Exists(dto.CatalogPath))
                {
                    UseJsonCatalog(dto.CatalogPath!);
                    catalogNote = $", каталог: {dto.CatalogPath}";
                    if (dto.CatalogVersion is int v && v > 0 &&
                        CatalogRepository is JsonCatalogRepository projectCatalog &&
                        projectCatalog.Version != v)
                        catalogNote +=
                            $" (в файле версия {projectCatalog.Version}, проект писался с {v})";
                }
                else
                {
                    catalogNote = $", каталог не найден: {dto.CatalogPath}";
                }
            }

            Rooms.Clear();
            foreach (var row in dto.Rooms ?? new List<RoomRow>())
                Rooms.Add(row);
            HookLiveRecalc();
            RefreshSystemSummaries();

            var state = new WorkspaceState
            {
                Rooms = Rooms.ToList(),
                Placements = _lastPlacementRows = dto.Placements ?? new List<PlacementRow>(),
                Levels = Rooms.Select(r => r.LevelName).Distinct().ToList(),
                Status = $"Проект загружен: {Rooms.Count} помещений, " +
                         $"{_lastPlacementRows.Count} приборов{catalogNote}"
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

            // U2.2: путь/версия каталога приборов
            public string? CatalogPath { get; set; }
            public int? CatalogVersion { get; set; }
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

        // ------------------------------------------------------------------
        // M2.1: панель свойств системы — опции, мутаторы, сводные
        // ------------------------------------------------------------------

        /// <summary>Строки (комната, система) с данным именем системы.</summary>
        private IEnumerable<(RoomRow Room, SystemRow System)> FindSystem(string name) =>
            Rooms.SelectMany(r => (r.Systems ?? new List<SystemRow>())
                .Where(s => s.Name == name)
                .Select(s => (Room: r, System: s)));

        /// <summary>M2.3: проёмы комнаты из снимка (для панели свойств помещения).</summary>
        public IReadOnlyList<SnapshotOpening> GetRoomOpenings(string roomId)
        {
            var result = new List<SnapshotOpening>();
            foreach (var o in _snapshot?.Openings ?? Enumerable.Empty<SnapshotOpening>())
                if (o.SpaceId == roomId)
                    result.Add(o);
            return result;
        }

        /// <summary>M2.3: комната снимка по Id (температура/высота для панели).</summary>
        public SnapshotRoom? FindSnapshotRoom(string roomId) =>
            _snapshot?.Rooms.FirstOrDefault(r => r.Id == roomId);

        /// <summary>Эффективные опции системы (оверрайд либо глобальные значения
        /// тулбара); null — система не найдена ни в одной комнате (например «Отопление»).</summary>
        public SystemOptionsView? GetSystemOptions(string name)
        {
            var first = FindSystem(name).FirstOrDefault().System;
            if (first == null)
                return null;
            bool supply = first.Type == HVACSystemType.Supply;
            return new SystemOptionsView
            {
                Type = first.Type,
                DeviceTypeId = first.DeviceTypeId,
                CountRule = first.CountRuleOverride ?? (supply ? SupplyRule : ExhaustRule),
                FixedCount = first.FixedCountOverride ?? FixedSupplyCount,
                Pattern = first.PatternOverride ?? (supply ? SupplyPattern : ExhaustPattern),
                SingleRule = first.SingleRuleOverride ?? SingleDeviceRule,
                EdgeOffsetOverrideMm = first.EdgeOffsetOverrideMm,
                CeilingOffsetOverrideMm = first.CeilingOffsetOverrideMm
            };
        }

        /// <summary>Переименовать систему во всех комнатах. null — успех, иначе
        /// текст ошибки (пустое имя; система не найдена; дубликат имени в комнате).</summary>
        public string? RenameSystem(string oldName, string newName)
        {
            newName = (newName ?? "").Trim();
            if (newName.Length == 0)
                return "Имя системы не может быть пустым";
            if (oldName == newName)
                return null;

            int touched = 0;
            foreach (var room in Rooms)
            {
                var systems = room.Systems;
                if (systems == null || !systems.Any(s => s.Name == oldName))
                    continue;
                if (systems.Any(s => s.Name != oldName &&
                                     string.Equals(s.Name, newName, StringComparison.OrdinalIgnoreCase)))
                    return $"В комнате «{room.Number}. {room.Name}» уже есть система «{newName}»";
                touched++;
            }
            if (touched == 0)
                return $"Система «{oldName}» не найдена";

            foreach (var (_, system) in FindSystem(oldName))
                system.Name = newName;
            foreach (var room in Rooms)
                room.RefreshSystemSummary();
            return null;
        }

        /// <summary>Применить мутацию ко всем строкам системы и поднять статус.</summary>
        private void ApplyToSystem(string name, Action<SystemRow> mutate, string label)
        {
            int n = 0;
            foreach (var (_, system) in FindSystem(name))
            {
                mutate(system);
                n++;
            }
            RaiseStatusOnly($"Система «{name}»: {label} обновлено в {n} помещениях");
        }

        public void SetSystemDeviceTypeId(string name, string? deviceTypeId) =>
            ApplyToSystem(name,
                s => s.DeviceTypeId = string.IsNullOrWhiteSpace(deviceTypeId) ? null : deviceTypeId,
                "типоразмер прибора");

        public void SetSystemCountRule(string name, CeilingCountRule rule) =>
            ApplyToSystem(name, s => s.CountRuleOverride = rule, "правило количества");

        public void SetSystemFixedCount(string name, int count)
        {
            if (count < 1)
            {
                ErrorSink?.Invoke($"N для правила Fixed должно быть ≥ 1 " +
                                  $"(получено {count}) — значение не изменено");
                return;
            }
            ApplyToSystem(name, s => s.FixedCountOverride = count, $"N = {count}");
        }

        public void SetSystemPattern(string name, WallPattern pattern) =>
            ApplyToSystem(name, s => s.PatternOverride = pattern, "паттерн расстановки");

        public void SetSystemSingleRule(string name, SingleRule rule) =>
            ApplyToSystem(name, s => s.SingleRuleOverride = rule, "правило одиночного прибора");

        /// <summary>M2.2: отступ зоны размещения от стен, мм; null — сброс на каталог.</summary>
        public void SetSystemEdgeOffset(string name, double? edgeOffsetMm)
        {
            if (!IsValidOffset(edgeOffsetMm, "Отступ от стен")) return;
            ApplyToSystem(name, s => s.EdgeOffsetOverrideMm = edgeOffsetMm,
                edgeOffsetMm is null ? "отступ от стен сброшен" : $"отступ от стен {edgeOffsetMm:F0} мм");
        }

        /// <summary>M2.2: заглубление от потолка, мм; null — сброс на типоразмер.</summary>
        public void SetSystemCeilingOffset(string name, double? ceilingOffsetMm)
        {
            if (!IsValidOffset(ceilingOffsetMm, "Заглубление от потолка")) return;
            ApplyToSystem(name, s => s.CeilingOffsetOverrideMm = ceilingOffsetMm,
                ceilingOffsetMm is null
                    ? "заглубление от потолка сброшено"
                    : $"заглубление от потолка {ceilingOffsetMm:F0} мм");
        }

        private bool IsValidOffset(double? mm, string label)
        {
            if (mm is double v && (double.IsNaN(v) || v < 0 || v > 100_000))
            {
                ErrorSink?.Invoke($"{label} должно быть в диапазоне 0–100000 мм " +
                                  $"(получено {v:F0}) — значение не изменено");
                return false;
            }
            return true;
        }

        /// <summary>
        /// P5 (Detail-режим): массовое применение оверрайдов к системам выбранных
        /// комнат. Применяются только взведённые поля спеки; <paramref name="spec"/>.SystemName
        /// сужает получателей до одной системы. Возвращает число изменённых строк.
        /// </summary>
        public int ApplyOverridesToRooms(
            Func<RoomRow, bool> roomFilter, MassOverrideSpec spec)
        {
            if (spec == null || !spec.HasAny)
                return 0;

            int touched = 0;
            foreach (var room in Rooms.Where(roomFilter))
            {
                var systems = room.Systems;
                if (systems == null)
                    continue;
                bool roomChanged = false;
                foreach (var system in systems)
                {
                    if (!string.IsNullOrEmpty(spec.SystemName) &&
                        system.Name != spec.SystemName)
                        continue;
                    if (spec.SetDeviceType)
                        system.DeviceTypeId =
                            string.IsNullOrWhiteSpace(spec.DeviceTypeId)
                                ? null : spec.DeviceTypeId;
                    if (spec.SetRule)
                        system.CountRuleOverride = spec.Rule;
                    if (spec.SetFixedCount && spec.FixedCount >= 1)
                        system.FixedCountOverride = spec.FixedCount;
                    if (spec.SetPattern)
                        system.PatternOverride = spec.Pattern;
                    if (spec.SetSingleRule)
                        system.SingleRuleOverride = spec.SingleRule;
                    if (spec.SetEdgeOffset)
                        system.EdgeOffsetOverrideMm = spec.EdgeOffsetMm;
                    if (spec.SetCeilingOffset)
                        system.CeilingOffsetOverrideMm = spec.CeilingOffsetMm;
                    roomChanged = true;
                    touched++;
                }
                if (roomChanged)
                    room.RefreshSystemSummary();
            }

            RaiseStatusOnly($"Массовое применение: изменено {touched} систем");
            return touched;
        }

        /// <summary>M2.1: опции потолочной расстановки конкретной системы —
        /// оверрайды панели свойств, при отсутствии — глобальные тулбара.</summary>
        private CeilingPlacementOptions SystemCeilingOptions(SystemRow system)
        {
            bool supply = system.Type == HVACSystemType.Supply;
            return new CeilingPlacementOptions
            {
                CountRule = system.CountRuleOverride ?? (supply ? SupplyRule : ExhaustRule),
                FixedCount = Math.Max(1, system.FixedCountOverride ?? FixedSupplyCount),
                Pattern = system.PatternOverride ?? (supply ? SupplyPattern : ExhaustPattern),
                SingleRule = system.SingleRuleOverride ?? SingleDeviceRule,

                // M2.2: отступы системы — высший приоритет движка.
                EdgeOffsetOverrideMm = system.EdgeOffsetOverrideMm,
                CeilingOffsetOverrideMm = system.CeilingOffsetOverrideMm
            };
        }

        /// <summary>M2.1: каталог, суженный до закреплённого за системой типоразмера;
        /// неизвестный Id → предупреждение и полный каталог (автоподбор).</summary>
        private IReadOnlyList<TerminalDevice> CatalogForSystem(
            IReadOnlyList<TerminalDevice> catalog, SystemRow system)
        {
            string? pinnedId = system.DeviceTypeId;
            if (string.IsNullOrWhiteSpace(pinnedId))
                return catalog;
            var pinned = catalog.FirstOrDefault(d =>
                d.Id == pinnedId && d.SystemType == system.Type);
            if (pinned == null)
            {
                ErrorSink?.Invoke($"Система «{system.Name}»: закреплённый типоразмер " +
                                  $"{pinnedId} не найден в каталоге — используется автоподбор");
                return catalog;
            }
            return new[] { pinned };
        }

        /// <summary>M2.1: пересобрать сводные по системам из строк последнего
        /// расчёта/загрузки проекта (без side-effects на ErrorSink).</summary>
        private void RefreshSystemSummaries()
        {
            var rows = _lastPlacementRows;
            if (rows.Count == 0)
            {
                LastSystemSummaries = Array.Empty<SystemSummary>();
                return;
            }

            var catalog = LookupCatalogQuiet();
            var roomsById = new Dictionary<string, SnapshotRoom>();
            foreach (var room in _snapshot?.Rooms ?? Enumerable.Empty<SnapshotRoom>())
                roomsById[room.Id] = room;

            var result = new List<SystemSummary>();
            foreach (var g in rows.GroupBy(r => r.SystemName))
            {
                var first = g.First();
                var device = FindCatalogDevice(catalog, first.Family, first.TypeName);
                var kefs = g.Where(x => x.KEf > 0).Select(x => x.KEf).ToList();

                // Пример формулы — комната-лидер по числу приборов.
                var sample = g.GroupBy(x => x.RoomId)
                    .OrderByDescending(rg => rg.Count()).First();
                int n = sample.Count();
                double roomFlow = sample.Sum(x => x.CalculatedFlow);
                double roomAreaM2 = roomsById.TryGetValue(sample.Key, out var room)
                    ? room.Area : 0;

                result.Add(new SystemSummary
                {
                    Name = g.Key,
                    Type = device?.SystemType ?? InferSystemTypeByName(g.Key),
                    RoomCount = g.Select(x => x.RoomId).Distinct().Count(),
                    DeviceCount = g.Count(),
                    TotalFlowM3h = g.Sum(x => x.CalculatedFlow),
                    AvgKef = kefs.Count > 0 ? kefs.Average() : 0,
                    TypeName = device == null
                        ? first.TypeName
                        : $"{first.Family} · {first.TypeName}",
                    FormulaText = BuildFormulaText(
                        first.CalculationOption, device, roomAreaM2, roomFlow, n)
                });
            }
            LastSystemSummaries = result;
        }

        private static HVACSystemType InferSystemTypeByName(string summaryName) =>
            summaryName == "Отопление"
                ? HVACSystemType.Heating
                : summaryName.StartsWith("В", StringComparison.OrdinalIgnoreCase)
                    ? HVACSystemType.Exhaust
                    : HVACSystemType.Supply;

        /// <summary>Тихое чтение каталога для сводных: без ErrorSink/fallback-статусов.</summary>
        private IReadOnlyList<TerminalDevice> LookupCatalogQuiet()
        {
            if (LastUsedCatalog is IReadOnlyList<TerminalDevice> used && used.Count > 0)
                return used;
            try
            {
                var devices = CatalogRepository?.GetAllDevices();
                if (devices is { Count: > 0 })
                    return devices;
            }
            catch
            {
                // сводные остаются без формул по параметрам прибора — не критично
            }
            return Array.Empty<TerminalDevice>();
        }

        private static TerminalDevice? FindCatalogDevice(
            IReadOnlyList<TerminalDevice> catalog, string family, string typeName) =>
            catalog.FirstOrDefault(d =>
                d.FamilyName == family &&
                (d.TypeName == typeName ||
                 (d.TypeName ?? "").Equals(typeName, StringComparison.OrdinalIgnoreCase)));

        /// <summary>Пояснение «почему такое N» по метке расчёта (словарь прототипа),
        /// на примере одной комнаты: расход комнаты, параметры типоразмера, итог N.</summary>
        private static string BuildFormulaText(
            string optionLabel, TerminalDevice? device,
            double roomAreaM2, double roomFlowM3h, int n)
        {
            switch (optionLabel)
            {
                case CalculationOptionLabels.Area:
                    return device != null && device.ServiceAreaM2 > 0 && roomAreaM2 > 0
                        ? $"N = ⌈S {roomAreaM2:F1} / {device.ServiceAreaM2:F0} м²⌉ = {n}"
                        : "N = ⌈S помещения / S обслуживания⌉";
                case CalculationOptionLabels.MinByFlow:
                    return device != null && device.MaxFlowRate > 0 && roomFlowM3h > 0
                        ? $"N = ⌈Q {roomFlowM3h:F0} / {device.MaxFlowRate:F0} м³/ч⌉ = {n}"
                        : "N = ⌈Q системы / Q прибора⌉";
                case CalculationOptionLabels.FixedN:
                    return $"N = задано вручную = {n}";
                case CalculationOptionLabels.Length:
                    return device != null && device.DirectiveLengthMm > 0
                        ? $"N = ⌈L участка / {device.DirectiveLengthMm:F0} мм⌉ = {n}"
                        : "N = ⌈L участка / L директивная⌉";
                default:
                    return "";
            }
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
            RefreshSystemSummaries();
            return new WorkspaceState
            {
                Rooms = Rooms.ToList(),
                Placements = rows,
                Levels = Rooms.Select(r => r.LevelName).Distinct().ToList(),
                Status = $"Выбрано {CountIncluded()} из {Rooms.Count} · " +
                         $"Размещение: {placements.Count} приборов за {elapsedMs:F0} мс, " +
                         $"предупреждений: {warnings.Count}",
                TotalDevices = placements.Count,
                HeatingCount = placements.Count(p => p.Device.SystemType == HVACSystemType.Heating),
                SupplyCount = placements.Count(p => p.Device.SystemType == HVACSystemType.Supply),
                ExhaustCount = placements.Count(p => p.Device.SystemType == HVACSystemType.Exhaust),
                ElapsedMs = elapsedMs,
                IsCalculation = true
            };
        }

        private void RaiseState(string status)
        {
            // Levels обязателен в любом состоянии: после загрузки снимка хост
            // заполняет селектор уровней до первого расчёта.
            StateChanged?.Invoke(new WorkspaceState
            {
                Rooms = Rooms.ToList(),
                Levels = Rooms.Select(r => r.LevelName).Distinct().ToList(),
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

        private List<PlacementRow> ToRows(
            IEnumerable<DevicePlacement> placements, Dictionary<string, double> kefByKey)
        {
            var roomsById = new Dictionary<string, SnapshotRoom>();
            foreach (var room in _snapshot?.Rooms ?? Enumerable.Empty<SnapshotRoom>())
                roomsById[room.Id] = room;

            var result = new List<PlacementRow>();
            foreach (var p in placements)
            {
                string key = p.RoomId + "|" + p.SystemName;
                kefByKey.TryGetValue(key, out double k);
                roomsById.TryGetValue(p.RoomId, out var room);
                result.Add(new PlacementRow
                {
                    // U3.1: «№. Имя» вместо внутреннего Id + уровень комнаты.
                    RoomId = p.RoomId,
                    RoomName = room == null
                        ? p.RoomId
                        : $"{room.Number}. {room.Name}",
                    LevelName = room?.LevelName ?? "",
                    Family = p.Device.FamilyName,
                    TypeName = p.Device.TypeName,
                    SystemName = p.SystemName,

                    // U3.1: координаты инженеру — в мм (снимок/Revit хранят футы).
                    X = Math.Round(LengthUnitConverter.UnitsToMm(p.Position.X), 0),
                    Y = Math.Round(LengthUnitConverter.UnitsToMm(p.Position.Y), 0),
                    RotationDeg = Math.Round(p.Rotation * 180.0 / Math.PI, 1),
                    KEf = k,
                    CalculatedFlow = Math.Round(p.CalculatedFlowM3h, 1),

                    // P2/P3: правило количества и высота установки.
                    CalculationOption = p.CalculationOption,
                    MountHeightMm = Math.Round(p.MountHeightMm, 0)
                });
            }
            return result;
        }

        /// <summary>
        /// U3.1: PlacementResult по комнатам из последнего Calculate — общая основа
        /// HTML-сцены для обоих хостов (App и ревит-стенд).
        /// </summary>
        public List<PlacementResult> BuildPlacementResults()
        {
            return BuildPlacementResults(null);
        }

        /// <summary>M3.2: сцена, ограниченная уровнем (null/пусто — все уровни).</summary>
        public List<PlacementResult> BuildPlacementResults(string? levelName)
        {
            var snapshot = _snapshot ?? throw new InvalidOperationException(
                "Снимок не загружен — HTML-сцена не может быть построена");
            if (LastRawPlacements.Count == 0)
                throw new InvalidOperationException("Нет расчёта — HTML-сцена пуста");

            bool filterByLevel = !string.IsNullOrEmpty(levelName);
            var roomsById = new Dictionary<string, SnapshotRoom>();
            foreach (var room in snapshot.Rooms)
                roomsById[room.Id] = room;

            return LastRawPlacements.GroupBy(p => p.RoomId)
                .Select(g =>
                {
                    if (!roomsById.TryGetValue(g.Key, out var room))
                        return null;
                    if (filterByLevel &&
                        !string.Equals(room.LevelName, levelName, StringComparison.Ordinal))
                        return null;
                    var polygon = room.ToPolygon();
                    if (polygon == null)
                        return null;
                    var rp = new RoomPolygon(
                        room.Id, $"{room.Number}. {room.Name}", polygon,
                        room.LevelElevation, Array.Empty<HVACSystem>());
                    return new PlacementResult(rp, g.ToList(), true, null);
                })
                .Where(r => r != null)
                .Cast<PlacementResult>()
                .ToList();
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
                        ScheduleLiveRecalc();
                    }
                };
            }
        }

        /// <summary>U3.1: правки Q/расходов коалесируются — ровно один пересчёт через
        /// <see cref="LiveRecalcDebounceMs"/> после ПОСЛЕДНЕЙ правки серии.</summary>
        private void ScheduleLiveRecalc()
        {
            LiveRecalcScheduler.Cancel();
            LiveRecalcScheduler.Schedule(
                TimeSpan.FromMilliseconds(LiveRecalcDebounceMs), RunLiveRecalc);
        }

        private void RunLiveRecalc()
        {
            if (!LiveRecalc) return; // флажок сняли, пока пересчёт был отложен
            try
            {
                Calculate();
            }
            catch (Exception ex)
            {
                ErrorSink?.Invoke("Живой пересчёт: " + ex.Message);
            }
        }
    }
}
