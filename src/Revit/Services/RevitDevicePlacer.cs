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

        /// <summary>S4.1: вариант для headless-тестов (TUnit) — документ есть,
        /// UIDocument (ActiveUIDocument) в этом режиме отсутствует.</summary>
        public RevitDevicePlacer(Document doc)
        {
            _uiDoc = null!;
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
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
        /// <param name="commentsFactory">Optional marker builder: the returned text is
        /// written to the instance Comments parameter (idempotency markers, plan C3.2).
        /// When null, legacy "System: &lt;name&gt;" comment is written.</param>
        /// <param name="levelResolver">Optional level lookup by placement room id;
        /// tried first (snapshot room ids may belong to a linked document).</param>
        public void PlaceDevicesInTransaction(
            IReadOnlyList<DevicePlacement> placements,
            Transaction tx,
            Func<DevicePlacement, string>? commentsFactory = null,
            Func<string, Level?>? levelResolver = null,
            Action<DevicePlacement, FamilyInstance>? instanceCreated = null)
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

                var level = levelResolver?.Invoke(placement.RoomId)
                    ?? GetLevelForRoom(placement.RoomId)
                    ?? GetFirstLevel();
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
                ApplyAirflow(instance, placement);

                string? comments = commentsFactory?.Invoke(placement);
                if (!string.IsNullOrEmpty(comments))
                    SetComments(instance, comments!);
                else
                    SetComments(instance, $"System: {placement.SystemName}");

                instanceCreated?.Invoke(placement, instance);
            }
        }

        /// <summary>
        /// Deletes non-type family instances whose Comments start with
        /// <paramref name="markerPrefix"/>. Returns the number deleted. Must be called
        /// inside an active transaction ("Заменить" idempotency mode).
        /// </summary>
        public int DeleteMarkedInstances(string markerPrefix)
        {
            int deleted = 0;
            var instances = new FilteredElementCollector(_doc)
                .WhereElementIsNotElementType()
                .OfClass(typeof(FamilyInstance));

            foreach (var fi in instances.Cast<FamilyInstance>().ToList())
            {
                var p = fi.LookupParameter("Comments");
                string value = p?.AsString() ?? "";
                if (!string.IsNullOrEmpty(value) &&
                    value.StartsWith(markerPrefix, StringComparison.Ordinal))
                {
                    _doc.Delete(fi.Id);
                    deleted++;
                }
            }
            return deleted;
        }

        /// <summary>
        /// Deletes non-type family instances whose Comments are exactly one of
        /// <paramref name="markers"/>. Returns the number deleted. Must be called
        /// inside an active transaction (idempotent "replace" semantics).
        /// </summary>
        public int DeleteMarkedInstancesExact(ICollection<string> markers)
        {
            int deleted = 0;
            var set = new HashSet<string>(markers, StringComparer.Ordinal);
            var instances = new FilteredElementCollector(_doc)
                .WhereElementIsNotElementType()
                .OfClass(typeof(FamilyInstance));

            foreach (var fi in instances.Cast<FamilyInstance>().ToList())
            {
                var p = fi.LookupParameter("Comments");
                string value = p?.AsString() ?? "";
                if (!string.IsNullOrEmpty(value) && set.Contains(value))
                {
                    _doc.Delete(fi.Id);
                    deleted++;
                }
            }
            return deleted;
        }

        /// <summary>
        /// Collects Comments of all family instances starting with "HLT|" —
        /// idempotency markers placed by this plugin.
        /// </summary>
        public List<string> CollectMarkers()
        {
            var result = new List<string>();
            var instances = new FilteredElementCollector(_doc)
                .WhereElementIsNotElementType()
                .OfClass(typeof(FamilyInstance));

            foreach (var fi in instances.Cast<FamilyInstance>())
            {
                var p = fi.LookupParameter("Comments");
                string value = p?.AsString() ?? "";
                if (value.StartsWith("HLT|", StringComparison.Ordinal))
                    result.Add(value);
            }
            return result;
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
        /// S3.1: пишет РАСЧЁТНЫЙ расход на приборе (CalculatedFlowM3h, м³/ч);
        /// 0 → паспортный максимум типоразмера (legacy-путь). Параметр —
        /// <see cref="TerminalDevice.FlowParameterName"/>.
        /// </summary>
        private static void ApplyAirflow(FamilyInstance instance, DevicePlacement placement)
        {
            var device = placement.Device;
            if (string.IsNullOrEmpty(device.FlowParameterName)) return;

            double flowM3h = placement.CalculatedFlowM3h > 0
                ? placement.CalculatedFlowM3h
                : device.MaxFlowRate;
            if (flowM3h <= 0) return;

            try
            {
                var param = instance.LookupParameter(device.FlowParameterName);
                if (param != null && !param.IsReadOnly)
                {
                    double internalValue = UnitUtils.ConvertToInternalUnits(
                        flowM3h, UnitTypeId.CubicMetersPerHour);
                    param.Set(internalValue);
                }
            }
            catch
            {
                // parameter may be type-only or otherwise non-writable; skip
            }
        }

        /// <summary>
        /// Writes text into the instance Comments parameter (idempotency marker
        /// or legacy system name).
        /// </summary>
        private static void SetComments(FamilyInstance instance, string text)
        {
            var comments = instance.LookupParameter("Comments");
            if (comments != null && !comments.IsReadOnly)
                comments.Set(text);
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
