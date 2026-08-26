using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace HVACLoadTerminals.Infrastructure.Presentation
{
    /// <summary>
    /// M1.1b: общее ядро CRM-каркаса для обоих хостов (App и ревит-стенд):
    /// дерево «Системы → Уровни → Помещения» + панели свойств системы/помещения.
    /// Хост подписывается на <see cref="HostRecalcRequested"/> (живой пересчёт),
    /// <see cref="HostStatus"/> (статусная строка) и <see cref="SelectionChanged"/>
    /// (фильтр таблицы приборов, перерисовка плана).
    /// </summary>
    public class CrmViewModel : INotifyPropertyChanged
    {
        public SnapshotWorkspacePresenter Workspace { get; }

        public ObservableCollection<CrmNode> TreeRoots { get; } = new();

        private string _treeSearchText = "";
        public string TreeSearchText
        {
            get => _treeSearchText;
            set
            {
                if (_treeSearchText == value) return;
                _treeSearchText = value ?? "";
                OnPropertyChanged(nameof(TreeSearchText));
                RebuildTree();
            }
        }

        private CrmNode? _selectedNode;
        public CrmNode? SelectedNode
        {
            get => _selectedNode;
            set
            {
                _selectedNode = value;
                OnPropertyChanged(nameof(SelectedNode));
                UpdatePanels();
                SelectionChanged?.Invoke();
            }
        }

        public SystemPropertiesViewModel SelectedSystem { get; }
        public RoomPropertiesViewModel SelectedRoom { get; }

        private bool _hasSelectedSystem;
        /// <summary>Выбран узел-система: показать редактор в панели свойств.</summary>
        public bool HasSelectedSystem
        {
            get => _hasSelectedSystem;
            private set { _hasSelectedSystem = value; OnPropertyChanged(nameof(HasSelectedSystem)); }
        }

        private bool _hasSelectedRoom;
        /// <summary>Выбран узел-помещение: показать свойства помещения.</summary>
        public bool HasSelectedRoom
        {
            get => _hasSelectedRoom;
            private set { _hasSelectedRoom = value; OnPropertyChanged(nameof(HasSelectedRoom)); }
        }

        /// <summary>Хост: выполнить пересчёт (при включённом LiveRecalc).</summary>
        public event Action? HostRecalcRequested;

        /// <summary>Хост: непафатальное сообщение в статусную строку.</summary>
        public event Action<string>? HostStatus;

        /// <summary>Выбор узла дерева изменился — хост обновляет фильтры/план.</summary>
        public event Action? SelectionChanged;

        internal void RequestRecalc() => HostRecalcRequested?.Invoke();

        internal void ReportStatus(string message) => HostStatus?.Invoke(message);

        public CrmViewModel(SnapshotWorkspacePresenter workspace)
        {
            Workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
            SelectedSystem = new SystemPropertiesViewModel(this);
            SelectedRoom = new RoomPropertiesViewModel(this);
            workspace.StateChanged += OnStateChanged;
        }

        /// <summary>Хост закрывает окно — отписаться от presenter'а
        /// (modeless-окна в Revit могут открываться многократно).</summary>
        public void Detach()
        {
            Workspace.StateChanged -= OnStateChanged;
        }

        // Последние строки расчёта — источник дерева между статусными состояниями.
        private IReadOnlyList<PlacementRow> _lastRows = Array.Empty<PlacementRow>();

        private void OnStateChanged(WorkspaceState state)
        {
            if (state.Placements.Count > 0)
                _lastRows = state.Placements;

            if (state.Placements.Count > 0 || state.Rooms.Count == 0)
                RebuildTree();
            // статусные состояния (без размещений) дерево не трогают

            UpdatePanels();
        }

        private void UpdatePanels()
        {
            HasSelectedSystem = SelectedNode?.Kind == "System";
            HasSelectedRoom = SelectedNode?.Kind == "Room";
            SelectedSystem.Refresh();
            SelectedRoom.Refresh();
        }

        /// <summary>Хост: принудительно обновить панели (например после
        /// массового применения без пересчёта).</summary>
        public void RefreshPanels() => UpdatePanels();

        /// <summary>Пересобрать дерево из строк последнего расчёта. Узлы
        /// пересоздаются — выбор восстанавливается по ключам.</summary>
        public void RebuildTree()
        {
            var previous = SelectedNode;
            foreach (var n in TreeRoots)
                n.Children.Clear();
            TreeRoots.Clear();

            // ---- Системные узлы из последнего расчёта ----
            var bySystem = _lastRows
                .GroupBy(p => p.SystemName)
                .OrderBy(g => g.Key == "Отопление" ? 1 : 0)
                .ThenByDescending(g => g.Sum(p => p.CalculatedFlow))
                .ToList();

            var builtRoots = new List<CrmNode>();

            foreach (var sys in bySystem)
            {
                var systemNode = new CrmNode
                {
                    Kind = "System",
                    Title = sys.Key,
                    SystemName = sys.Key,
                    DeviceCount = sys.Count()
                };
                foreach (var lvl in sys.GroupBy(p => p.LevelName)
                             .OrderBy(l => l.Key, StringComparer.Ordinal))
                {
                    var levelNode = new CrmNode
                    {
                        Kind = "Level",
                        Title = string.IsNullOrEmpty(lvl.Key) ? "(без уровня)" : lvl.Key!,
                        SystemName = sys.Key,
                        LevelName = lvl.Key,
                        DeviceCount = lvl.Count()
                    };
                    foreach (var roomGroup in lvl.GroupBy(p => p.RoomId)
                                 .OrderBy(g => g.Key, StringComparer.Ordinal))
                    {
                        var firstRow = roomGroup.First();
                        levelNode.Children.Add(new CrmNode
                        {
                            Kind = "Room",
                            Title = firstRow.RoomName,
                            SystemName = sys.Key,
                            LevelName = lvl.Key,
                            RoomId = roomGroup.Key,
                            DeviceCount = roomGroup.Count()
                        });
                    }
                    systemNode.Children.Add(levelNode);
                }
                builtRoots.Add(systemNode);
            }

            // ---- IC5.4: ветка «Без систем (N)» ----
            try
            {
                var withoutRooms = Workspace.Rooms
                    .Where(r => r.Systems == null || r.Systems.Count == 0 || !r.Systems.Any(s => s.IsIncluded))
                    .ToList();
                if (withoutRooms.Count > 0)
                {
                    var noSysNode = new CrmNode
                    {
                        Kind = "NoSystem",
                        Title = $"Без систем ({withoutRooms.Count})",
                        DeviceCount = withoutRooms.Count
                    };
                    foreach (var lvl in withoutRooms.GroupBy(r => r.LevelName).OrderBy(g => g.Key, StringComparer.Ordinal))
                    {
                        var lvlNode = new CrmNode
                        {
                            Kind = "NoSystemLevel",
                            Title = string.IsNullOrEmpty(lvl.Key) ? "(без уровня)" : lvl.Key!,
                            LevelName = lvl.Key,
                            DeviceCount = lvl.Count(),
                            SystemName = null
                        };
                        // Tag NoSystem as parent kind to distinguish
                        foreach (var r in lvl.OrderBy(x => x.Number, StringComparer.Ordinal))
                        {
                            lvlNode.Children.Add(new CrmNode
                            {
                                Kind = "NoSystemRoom",
                                Title = $"{r.Number}. {r.Name}",
                                LevelName = r.LevelName,
                                RoomId = r.RoomId,
                                DeviceCount = 0,
                                SystemName = null
                            });
                        }
                        noSysNode.Children.Add(lvlNode);
                    }
                    builtRoots.Insert(0, noSysNode);
                }
            }
            catch { }

            // ---- Поиск по дереву (IC5.4) ----
            List<CrmNode> toAdd = builtRoots;
            if (!string.IsNullOrWhiteSpace(_treeSearchText))
            {
                string q = _treeSearchText.Trim();
                toAdd = FilterTree(builtRoots, q);
            }

            foreach (var n in toAdd)
                TreeRoots.Add(n);

            OnPropertyChanged(nameof(TreeRoots));

            if (TreeRoots.Count == 0)
            {
                if (SelectedNode != null)
                    SelectedNode = null;
                return;
            }
            if (previous == null)
                return;
            var restored = FindTreeNode(
                TreeRoots, previous.Kind, previous.SystemName, previous.LevelName, previous.RoomId);
            if (restored != null && !ReferenceEquals(restored, previous))
                SelectedNode = restored;
        }

        private static List<CrmNode> FilterTree(IEnumerable<CrmNode> nodes, string query)
        {
            var result = new List<CrmNode>();
            foreach (var n in nodes)
            {
                bool titleMatch = n.Title.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
                var filteredChildren = FilterTree(n.Children, query);
                if (titleMatch || filteredChildren.Count > 0)
                {
                    var copy = new CrmNode
                    {
                        Kind = n.Kind,
                        Title = n.Title,
                        SystemName = n.SystemName,
                        LevelName = n.LevelName,
                        RoomId = n.RoomId,
                        DeviceCount = n.DeviceCount
                    };
                    if (titleMatch)
                    {
                        // keep all children when parent matches
                        foreach (var c in n.Children) copy.Children.Add(c);
                    }
                    else
                    {
                        foreach (var c in filteredChildren) copy.Children.Add(c);
                    }
                    result.Add(copy);
                }
            }
            return result;
        }

        private static CrmNode? FindTreeNode(
            IEnumerable<CrmNode> nodes, string kind,
            string? systemName, string? levelName, string? roomId)
        {
            foreach (var node in nodes)
            {
                if (node.Kind == kind &&
                    node.SystemName == systemName &&
                    node.LevelName == levelName &&
                    node.RoomId == roomId)
                    return node;
                var deep = FindTreeNode(node.Children, kind, systemName, levelName, roomId);
                if (deep != null)
                    return deep;
            }
            return null;
        }

        /// <summary>M2.1: переименовать выбранную систему во всех комнатах.
        /// null — успех, иначе текст ошибки.</summary>
        public string? RenameSelectedSystem(string newName)
        {
            if (SelectedNode?.Kind != "System")
                return "Система не выбрана";
            string oldName = SelectedNode.SystemName ?? "";
            string? error = Workspace.RenameSystem(oldName, newName);
            if (error != null)
                return error;

            // Пересчёт сразу: имена систем в строках/сцене должны совпасть с деревом.
            try
            {
                Workspace.Calculate();
            }
            catch (Exception ex)
            {
                ReportStatus("Пересчёт после переименования: " + ex.Message);
                RebuildTree();
            }
            SelectSystemNode(newName);
            ReportStatus($"Система «{oldName}» переименована в «{newName}»");
            return null;
        }

        private void SelectSystemNode(string name)
        {
            var node = TreeRoots.FirstOrDefault(n =>
                n.Kind == "System" && n.SystemName == name);
            if (node != null)
                SelectedNode = node;
        }

        /// <summary>Совпадает ли строка приборов с выбранным узлом (фильтр таблицы).</summary>
        public bool MatchesSelectedNode(PlacementRow p)
        {
            if (SelectedNode == null || SelectedNode.Kind == "") return true;
            return SelectedNode.Kind switch
            {
                "System" => p.SystemName == SelectedNode.SystemName,
                "Level" => p.LevelName == SelectedNode.LevelName &&
                           (SelectedNode.SystemName == null ||
                            p.SystemName == SelectedNode.SystemName),
                "Room" => p.RoomId == SelectedNode.RoomId,
                "NoSystem" => false,
                "NoSystemLevel" => false,
                "NoSystemRoom" => p.RoomId == SelectedNode.RoomId,
                _ => true
            };
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
