using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using HVACLoadTerminals.App.ViewModels;
using HVACLoadTerminals.Infrastructure.Presentation;

namespace HVACLoadTerminals.App
{
    public partial class LevelsWindow : Window
    {
        private readonly MainViewModel _vm;

        private class LevelRow
        {
            public string LevelName { get; set; } = "";
            public int RoomCount { get; set; }
            public double TotalArea { get; set; }
            public double TotalHeatingKw { get; set; }
            public double TotalSupply { get; set; }
            public double TotalExhaust { get; set; }
            public int WithSystems { get; set; }
            public int WithoutSystems { get; set; }
            public int Included { get; set; }
        }

        public LevelsWindow(MainViewModel vm)
        {
            InitializeComponent();
            _vm = vm ?? throw new ArgumentNullException(nameof(vm));
            BuildGrid();
            if (LevelsGrid.Items.Count > 0)
                LevelsGrid.SelectedIndex = 0;
            // Pre-select current level
            var current = _vm.SelectedLevel;
            if (!string.IsNullOrEmpty(current))
            {
                for (int i = 0; i < LevelsGrid.Items.Count; i++)
                {
                    if ((LevelsGrid.Items[i] as LevelRow)?.LevelName == current)
                    { LevelsGrid.SelectedIndex = i; break; }
                }
            }
        }

        private void BuildGrid()
        {
            var rows = _vm.Workspace.Rooms
                .GroupBy(r => r.LevelName ?? "")
                .OrderBy(g => g.Key, StringComparer.Ordinal)
                .Select(g =>
                {
                    var list = g.ToList();
                    return new LevelRow
                    {
                        LevelName = string.IsNullOrEmpty(g.Key) ? "(без уровня)" : g.Key,
                        RoomCount = list.Count,
                        TotalArea = list.Sum(r => r.Area),
                        TotalHeatingKw = list.Sum(r => r.HeatingW) / 1000.0,
                        TotalSupply = list.Sum(r => r.Supply),
                        TotalExhaust = list.Sum(r => r.Exhaust),
                        WithSystems = list.Count(r => r.Systems != null && r.Systems.Any(s => s.IsIncluded)),
                        WithoutSystems = list.Count(r => r.Systems == null || !r.Systems.Any(s => s.IsIncluded)),
                        Included = list.Count(r => r.IsIncluded)
                    };
                }).ToList();
            LevelsGrid.ItemsSource = rows;
        }

        private LevelRow? SelectedLevelRow => LevelsGrid.SelectedItem as LevelRow;

        private void Show_Click(object sender, RoutedEventArgs e)
        {
            var row = SelectedLevelRow;
            if (row == null) return;
            string name = row.LevelName == "(без уровня)" ? "" : row.LevelName;
            // Find actual level name from Rooms (handle empty)
            var actual = _vm.Workspace.Rooms.FirstOrDefault(r => (r.LevelName ?? "") == (name == "" ? "" : row.LevelName))?.LevelName ?? name;
            _vm.SelectedLevel = actual;
            DialogResult = true;
            Close();
        }

        private void LevelsGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            Show_Click(sender, e);
        }

        private void Include_Click(object sender, RoutedEventArgs e)
        {
            var row = SelectedLevelRow;
            if (row == null) return;
            string lvl = row.LevelName == "(без уровня)" ? "" : row.LevelName;
            string before = _vm.Workspace.CaptureStateJson();
            _vm.PushUndo($"Включить уровень {lvl}");
            _vm.Workspace.IncludeLevel(lvl);
            _vm.PopUndoIfNoChange(before);
            BuildGrid();
            _vm.MarkDirty();
        }

        private void Exclude_Click(object sender, RoutedEventArgs e)
        {
            var row = SelectedLevelRow;
            if (row == null) return;
            string lvl = row.LevelName == "(без уровня)" ? "" : row.LevelName;
            var rooms = _vm.Workspace.Rooms.Where(r => (r.LevelName ?? "") == lvl).ToList();
            if (rooms.Count == 0) return;
            string before = _vm.Workspace.CaptureStateJson();
            _vm.PushUndo($"Исключить уровень {lvl}");
            _vm.Workspace.SetIncluded(r => (r.LevelName ?? "") == lvl, false);
            _vm.PopUndoIfNoChange(before);
            BuildGrid();
            _vm.MarkDirty();
        }

        private void Assign_Click(object sender, RoutedEventArgs e)
        {
            var row = SelectedLevelRow;
            if (row == null) return;
            string lvl = row.LevelName == "(без уровня)" ? "" : row.LevelName;
            var ids = _vm.Workspace.Rooms.Where(r => (r.LevelName ?? "") == lvl).Select(r => r.RoomId).ToHashSet();
            if (ids.Count == 0) return;
            string before = _vm.Workspace.CaptureStateJson();
            _vm.PushUndo($"Назначить систему уровню {lvl}");
            var win = new HVACLoadTerminals.Infrastructure.Presentation.AssignSystemWizardWindow(_vm.Workspace, r => ids.Contains(r.RoomId)) { Owner = this };
            bool? res = win.ShowDialog();
            _vm.PopUndoIfNoChange(before);
            BuildGrid();
            if (res == true) { _vm.MarkDirty(); }
        }

        private void Close_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }
    }
}
