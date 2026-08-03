using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using HVACLoadTerminals.Core.Models;
using HVACLoadTerminals.Revit.Logging;

namespace HVACLoadTerminals.Revit.Visualization
{
    /// <summary>
    /// Row model for the placement result summary (per room).
    /// </summary>
    public sealed class PlacementSummaryRow
    {
        public string RoomName { get; set; } = "";
        public int PlacementCount { get; set; }
        public string SystemsSummary { get; set; } = "";
    }

    /// <summary>
    /// Simple WPF summary window over the placement results. Shows one row per
    /// room (terminal count + per-system breakdown) with Place/Cancel buttons.
    /// No HTML, no OxyPlot — kept deliberately simple so the Revit command
    /// stays responsive inside the Revit process.
    /// </summary>
    public partial class PlacementResultWindow : Window
    {
        private readonly IReadOnlyList<DevicePlacement> _placements;
        private bool _confirmed;

        /// <summary>True when the user clicked "Place in Revit".</summary>
        public bool IsConfirmed => _confirmed;

        /// <summary>All placements the user confirmed (null when cancelled).</summary>
        public IReadOnlyList<DevicePlacement>? ConfirmedPlacements { get; private set; }

        public PlacementResultWindow(string title, IReadOnlyList<PlacementResult> results)
        {
            InitializeComponent();

            Title = title;
            TitleText.Text = title;

            _placements = results
                .SelectMany(r => r.Placements)
                .ToList();

            var allPlacements = _placements;

            SummaryText.Text = $"{results.Count} room(s), {allPlacements.Count} terminal placement(s) computed. " +
                               "Review the summary below, then Place in Revit or Cancel.";

            RoomsList.ItemsSource = results
                .Select(BuildRow)
                .ToList();
        }

        private static PlacementSummaryRow BuildRow(PlacementResult result)
        {
            var bySystem = result.Placements
                .GroupBy(p => p.SystemName ?? "?")
                .Select(g => $"{g.Key}: {g.Count()}")
                .ToList();

            return new PlacementSummaryRow
            {
                RoomName = result.Room.RoomName ?? result.Room.RoomId ?? "Room",
                PlacementCount = result.Placements.Count,
                SystemsSummary = bySystem.Count == 0
                    ? (result.WarningMessage ?? "no placements")
                    : string.Join("  ·  ", bySystem)
            };
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ConfirmedPlacements = _placements;
                _confirmed = true;
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                HvacLogger.LogException("PlacementResultWindow apply", ex);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _confirmed = false;
                DialogResult = false;
                Close();
            }
            catch (Exception ex)
            {
                HvacLogger.LogException("PlacementResultWindow cancel", ex);
            }
        }
    }
}
