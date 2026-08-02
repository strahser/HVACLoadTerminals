using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using HVACLoadTerminals.Core.Models;

namespace HVACLoadTerminals.Revit.Services
{
    /// <summary>
    /// Shows a preview of terminal placements in the model and asks the user to
    /// confirm. Preview markers and (on confirmation) the real devices run inside
    /// a single transaction: "Yes" places the devices and commits, "No" (or any
    /// error) rolls everything back so nothing stays in the model.
    /// </summary>
    public class RevitPlacementPreviewService
    {
        private readonly UIDocument _uiDoc;
        private readonly Document _doc;

        public RevitPlacementPreviewService(UIDocument uiDoc)
        {
            _uiDoc = uiDoc ?? throw new ArgumentNullException(nameof(uiDoc));
            _doc = uiDoc.Document;
        }

        /// <summary>
        /// Creates preview markers for <paramref name="placements"/> and shows a
        /// modal Yes/No dialog. On Yes the devices are created in the same
        /// transaction and committed; on No (or error) the transaction is rolled
        /// back and nothing remains.
        /// </summary>
        /// <param name="placements">Placements to preview (and optionally place).</param>
        /// <param name="caption">Dialog caption.</param>
        /// <returns>True when the user confirmed and the devices were committed.</returns>
        public bool PreviewAndConfirm(
            IReadOnlyList<DevicePlacement> placements,
            string caption = "Terminal Placement Preview")
        {
            if (placements == null || placements.Count == 0) return false;

            using var tx = new Transaction(_doc, "Preview Terminal Placement");
            tx.Start();

            try
            {
                var placer = new RevitDevicePlacer(_uiDoc);
                placer.CreatePreviewMarkers(placements, tx);

                var result = System.Windows.MessageBox.Show(
                    $"Preview shows {placements.Count} terminals.\n\n" +
                    "Yes = place terminals in the model (commit).\n" +
                    "No = cancel (rollback, nothing stays).",
                    caption,
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Question);

                if (result == System.Windows.MessageBoxResult.Yes)
                {
                    placer.PlaceDevicesInTransaction(placements, tx);
                    tx.Commit();
                    return true;
                }

                tx.RollBack();
                return false;
            }
            catch (Exception ex)
            {
                tx.RollBack();
                System.Windows.MessageBox.Show(
                    "Preview failed: " + ex.Message,
                    "Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
                return false;
            }
        }
    }
}
