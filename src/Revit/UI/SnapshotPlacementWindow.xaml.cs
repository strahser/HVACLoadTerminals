using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using Autodesk.Revit.UI;
using HVACLoadTerminals.Core.Services;
using HVACLoadTerminals.Infrastructure.Presentation;

namespace HVACLoadTerminals.Revit.UI
{
    /// <summary>
    /// Modeless snapshot placement window over the shared presenter (plan C3.3).
    /// Shown with Show() — Revit stays responsive; model writes go through the
    /// ExternalEvent handler only.
    /// </summary>
    public partial class SnapshotPlacementWindow : Window
    {
        private readonly SnapshotWorkspacePresenter _presenter;
        private readonly PlaceDevicesExternalEventHandler _handler;
        private readonly ExternalEvent _externalEvent;

        public SnapshotPlacementWindow(
            SnapshotWorkspacePresenter presenter,
            PlaceDevicesExternalEventHandler handler)
        {
            InitializeComponent();
            _presenter = presenter;
            _handler = handler;
            _externalEvent = ExternalEvent.Create(handler); // valid: called in API context

            DataContext = _presenter;
            _presenter.StateChanged += OnStateChanged;
            _handler.Completed += status => StatusText.Text = status;

            Closed += (_, _) => _presenter.StateChanged -= OnStateChanged;
        }

        private void OnStateChanged(WorkspaceState state)
        {
            RoomsGrid.ItemsSource = state.Rooms;
            PlacementsGrid.ItemsSource = state.IsCalculation
                ? state.Placements
                : PlacementsGrid.ItemsSource;
            StatusText.Text = state.Status;
        }

        private void OpenSnapshot_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Выберите снимок помещений HeatLossRevit2",
                Filter = "Снимки (*.json)|*.json|Все файлы|*.*"
            };
            if (dlg.ShowDialog(this) != true)
                return;

            try
            {
                _presenter.LoadSnapshot(dlg.FileName);
            }
            catch (Exception ex)
            {
                StatusText.Text = "Ошибка чтения снимка: " + ex.Message;
            }
        }

        private void RegenLoads_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _presenter.RegenerateLoads();
            }
            catch (Exception ex)
            {
                StatusText.Text = "Ошибка: " + ex.Message;
            }
        }

        private void Calculate_Click(object sender, RoutedEventArgs e) =>
            _presenter.Calculate();

        private void Place_Click(object sender, RoutedEventArgs e)
        {
            var raw = _presenter.LastRawPlacements;
            if (_presenter.CurrentSnapshot == null || raw.Count == 0)
            {
                StatusText.Text = "Откройте снимок и рассчитайте размещение";
                return;
            }

            _handler.SetPending(new PlaceRequest
            {
                Placements = raw.ToList(),
                RoomLevels = _presenter.CurrentSnapshot.Rooms
                    .GroupBy(r => r.Id)
                    .ToDictionary(g => g.Key, g => g.First().LevelName ?? ""),
                DocumentTitle = !string.IsNullOrEmpty(_presenter.CurrentSnapshot.Metadata?.DocumentTitle)
                    ? Path.GetFileNameWithoutExtension(_presenter.CurrentSnapshot.Metadata.DocumentTitle)
                    : "Snapshot"
            });
            _externalEvent.Raise();
        }
    }
}
