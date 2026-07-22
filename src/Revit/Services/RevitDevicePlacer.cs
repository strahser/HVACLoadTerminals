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
            if (placements.Count == 0) return;

            using var tx = new Transaction(_doc, "Place HVAC Terminals");
            tx.Start();

            var symbolMap = LoadSymbols(placements);

            foreach (var placement in placements)
            {
                if (!symbolMap.TryGetValue(placement.Device.FamilyName, out var symbol))
                    continue;

                var level = GetLevelForRoom(placement.RoomId);
                if (level == null) continue;

                var xyz = new XYZ(placement.Position.X, placement.Position.Y, level.Elevation);

                var instance = _doc.Create.NewFamilyInstance(
                    xyz, symbol, level, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);

                if (instance != null && !string.IsNullOrEmpty(placement.SystemName))
                {
                    var comments = instance.LookupParameter("Comments");
                    if (comments != null && !comments.IsReadOnly)
                        comments.Set($"System: {placement.SystemName}");
                }
            }

            tx.Commit();
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

        private Level? GetLevelForRoom(string roomId)
        {
            if (!ElementId.TryParse(roomId, out var id)) return null;
            var space = _doc.GetElement(id) as Space;
            return space?.Level as Level;
        }
    }
}
