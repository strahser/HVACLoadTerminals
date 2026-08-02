using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI;
using HVACLoadTerminals.Core.Interfaces;
using HVACLoadTerminals.Core.Models;

namespace HVACLoadTerminals.Revit.Services
{
    public class RevitDevicePlacer : IDevicePlacer
    {
        private readonly UIDocument _uiDoc;
        private readonly Document _doc;

        public RevitDevicePlacer(UIDocument uiDoc)
        {
            _uiDoc = uiDoc ?? throw new ArgumentNullException(nameof(uiDoc));
            _doc = uiDoc.Document;
        }

        public void PlaceDevices(IReadOnlyList<DevicePlacement> placements)
        {
            if (placements == null || placements.Count == 0) return;

            using var tx = new Transaction(_doc, "Place HVAC Terminals");
            tx.Start();

            PlaceDevicesInTransaction(placements, tx);

            tx.Commit();
        }

        /// <summary>
        /// Places the devices (symbol loading + family instance creation only)
        /// inside an already-started transaction owned by the caller. The caller
        /// is responsible for committing or rolling back the transaction.
        /// </summary>
        /// <param name="placements">Placements to create in the model.</param>
        /// <param name="tx">An already-started transaction owned by the caller.</param>
        public void PlaceDevicesInTransaction(IReadOnlyList<DevicePlacement> placements, Transaction tx)
        {
            if (placements == null || placements.Count == 0) return;
            if (tx == null) throw new ArgumentNullException(nameof(tx));
            if (tx.GetStatus() != TransactionStatus.Started)
                throw new InvalidOperationException(
                    "PlaceDevicesInTransaction requires an active (started) transaction.");

            var symbolMap = LoadSymbols(placements);

            foreach (var placement in placements)
            {
                if (!symbolMap.TryGetValue(placement.Device.FamilyName, out var symbol))
                    continue;

                var level = GetLevelForRoom(placement.RoomId) ?? GetFirstLevel();
                if (level == null) continue;

                var xyz = new XYZ(placement.Position.X, placement.Position.Y, level.Elevation);

                FamilyInstance? instance;
                try
                {
                    instance = _doc.Create.NewFamilyInstance(
                        xyz, symbol, level, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                }
                catch
                {
                    continue; // instance creation failed for this symbol -> skip, continue
                }

                if (instance == null) continue;

                ApplyRotation(instance, xyz, placement.Rotation);
                ApplyAirflow(instance, placement.Device);
                ApplyComments(instance, placement);
            }
        }

        public void RemovePlacements(string roomId)
        {
            if (!ElementId.TryParse(roomId, out var id)) return;

            using var tx = new Transaction(_doc, "Remove Terminals");
            tx.Start();

            var instances = new FilteredElementCollector(_doc)
                .OfCategory(BuiltInCategory.OST_DuctTerminal)
                .WhereElementIsNotElementType()
                .Cast<FamilyInstance>();

            foreach (var instance in instances)
            {
                var commentsParam = instance.LookupParameter("Comments");
                if (commentsParam != null)
                {
                    string comments = commentsParam.AsString() ?? "";
                    if (comments.Contains(roomId))
                    {
                        _doc.Delete(instance.Id);
                    }
                }
            }

            tx.Commit();
        }

        public void ShowPreview(IReadOnlyList<DevicePlacement> placements)
        {
            using var tx = new Transaction(_doc, "Show Placement Preview");
            tx.Start();

            int i = 0;
            foreach (var placement in placements)
            {
                var pt = new XYZ(placement.Position.X, placement.Position.Y, 0);

                var line = Line.CreateBound(
                    pt,
                    new XYZ(pt.X + 0.3, pt.Y + 0.3, 0));

                var sketchPlane = SketchPlane.Create(
                    _doc, Plane.CreateByNormalAndOrigin(XYZ.BasisZ, pt));

                _doc.Create.NewModelCurve(line, sketchPlane);
                i++;
                if (i > 100) break;
            }

            TaskDialog.Show("Preview",
                $"Created {i} preview markers.\nUndo to remove them.");

            tx.Commit();
        }

        /// <summary>
        /// Creates circular markers plus a short label line at each placement
        /// position. The caller owns the transaction: it must start the
        /// transaction before calling this method and commit (or roll back)
        /// afterwards, typically after a Place/Cancel confirmation dialog.
        /// </summary>
        /// <param name="placements">Placements to mark in the model.</param>
        /// <param name="tx">An already-started transaction owned by the caller.</param>
        public void CreatePreviewMarkers(IReadOnlyList<DevicePlacement> placements, Transaction tx)
        {
            if (placements == null || placements.Count == 0) return;
            if (tx == null) throw new ArgumentNullException(nameof(tx));
            if (tx.GetStatus() != TransactionStatus.Started)
                throw new InvalidOperationException(
                    "CreatePreviewMarkers requires an active (started) transaction.");

            var sketchPlane = SketchPlane.Create(
                _doc, Plane.CreateByNormalAndOrigin(XYZ.BasisZ, XYZ.Zero));

            foreach (var placement in placements)
            {
                var pt = new XYZ(placement.Position.X, placement.Position.Y, 0);

                var circle = Ellipse.CreateCurve(
                    pt, 0.3, 0.3, XYZ.BasisX, XYZ.BasisY, 0, 2 * Math.PI);
                _doc.Create.NewModelCurve(circle, sketchPlane);

                var labelLine = Line.CreateBound(
                    pt, new XYZ(pt.X + 0.3, pt.Y + 0.3, 0));
                _doc.Create.NewModelCurve(labelLine, sketchPlane);
            }
        }

        /// <summary>
        /// Builds a family-symbol lookup for the placements. A symbol is matched
        /// by <see cref="TerminalDevice.TypeName"/> first (symbol name), falling
        /// back to <see cref="TerminalDevice.FamilyName"/> (family name). Inactive
        /// symbols are activated.
        /// </summary>
        private Dictionary<string, FamilySymbol> LoadSymbols(IReadOnlyList<DevicePlacement> placements)
        {
            var symbols = new FilteredElementCollector(_doc)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .ToList();

            var map = new Dictionary<string, FamilySymbol>();
            foreach (var placement in placements)
            {
                if (map.ContainsKey(placement.Device.FamilyName)) continue;

                var match = symbols.FirstOrDefault(s =>
                        s.Name != null &&
                        s.Name.Equals(placement.Device.TypeName, StringComparison.OrdinalIgnoreCase))
                    ?? symbols.FirstOrDefault(s =>
                        s.Family != null && s.Family.Name != null &&
                        s.Family.Name.Equals(placement.Device.FamilyName,
                            StringComparison.OrdinalIgnoreCase));

                if (match != null)
                {
                    if (!match.IsActive) match.Activate();
                    map[placement.Device.FamilyName] = match;
                }
            }
            return map;
        }

        /// <summary>
        /// Applies the device rotation (radians, CCW, 0 = front faces +X) around
        /// the vertical axis through the placement point.
        /// </summary>
        private static void ApplyRotation(FamilyInstance instance, XYZ xyz, double rotation)
        {
            var loc = instance.Location as LocationPoint;
            if (loc != null && Math.Abs(rotation) > 1e-9)
            {
                var axis = Line.CreateBound(
                    new XYZ(xyz.X, xyz.Y, xyz.Z - 1),
                    new XYZ(xyz.X, xyz.Y, xyz.Z + 1));
                loc.Rotate(axis, rotation);
            }
        }

        /// <summary>
        /// Writes the device max flow rate (m3/h) into the flow parameter
        /// identified by <see cref="TerminalDevice.FlowParameterName"/>.
        /// </summary>
        private static void ApplyAirflow(FamilyInstance instance, TerminalDevice device)
        {
            if (string.IsNullOrEmpty(device.FlowParameterName)) return;

            try
            {
                var param = instance.LookupParameter(device.FlowParameterName);
                if (param != null && !param.IsReadOnly)
                {
                    double internalValue = UnitUtils.ConvertToInternalUnits(
                        device.MaxFlowRate, UnitTypeId.CubicMetersPerHour);
                    param.Set(internalValue);
                }
            }
            catch
            {
                // parameter may be type-only or otherwise non-writable; skip
            }
        }

        /// <summary>
        /// Writes the system name into the instance Comments parameter.
        /// </summary>
        private static void ApplyComments(FamilyInstance instance, DevicePlacement placement)
        {
            if (!string.IsNullOrEmpty(placement.SystemName))
            {
                var comments = instance.LookupParameter("Comments");
                if (comments != null && !comments.IsReadOnly)
                    comments.Set($"System: {placement.SystemName}");
            }
        }

        private Level? GetLevelForRoom(string roomId)
        {
            if (!ElementId.TryParse(roomId, out var id)) return null;
            var space = _doc.GetElement(id) as Space;
            return space?.Level as Level;
        }

        private Level? GetFirstLevel()
        {
            return new FilteredElementCollector(_doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .FirstOrDefault();
        }
    }
}
