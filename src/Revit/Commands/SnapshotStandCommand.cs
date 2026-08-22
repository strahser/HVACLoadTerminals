using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using HVACLoadTerminals.Infrastructure.Presentation;
using HVACLoadTerminals.Revit.UI;

namespace HVACLoadTerminals.Revit.Commands
{
    /// <summary>
    /// Opens the modeless snapshot placement stand. Revit stays responsive
    /// (window.Show(), writes via ExternalEvent) — plan card C3.3.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class SnapshotStandCommand : IExternalCommand
    {
        private static SnapshotPlacementWindow? _window;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                if (_window != null && _window.IsLoaded)
                {
                    _window.Activate();
                    return Result.Succeeded;
                }

                var uiDoc = commandData.Application.ActiveUIDocument;
                var handler = new PlaceDevicesExternalEventHandler(uiDoc);
                _window = new SnapshotPlacementWindow(new SnapshotWorkspacePresenter(), handler);
                _window.Show(); // non-blocking: Revit remains available

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
