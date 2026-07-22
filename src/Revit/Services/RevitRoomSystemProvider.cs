using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using HVACLoadTerminals.Core.Interfaces;
using HVACLoadTerminals.Core.Models;

namespace HVACLoadTerminals.Revit.Services
{
    public class RevitRoomSystemProvider : IRoomSystemProvider
    {
        private readonly Document _doc;

        public RevitRoomSystemProvider(Document doc)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
        }

        public IReadOnlyList<HVACSystem> GetSystemsForRoom(string roomId)
        {
            if (!ElementId.TryParse(roomId, out var id))
                return Array.Empty<HVACSystem>();

            var space = _doc.GetElement(id) as Space;
            if (space == null) return Array.Empty<HVACSystem>();

            var systems = new List<HVACSystem>();

            try
            {
                string[] supplyNames = {
                    "Supply Airflow", "Supply Air Flow", "Приток", "Supply Flow"
                };
                string[] exhaustNames = {
                    "Exhaust Airflow", "Exhaust Air Flow", "Вытяжка", "Exhaust Flow"
                };

                foreach (var name in supplyNames)
                {
                    var p = space.LookupParameter(name);
                    if (p != null && p.HasValue && p.AsDouble() > 0)
                    {
                        double flow = UnitUtils.ConvertFromInternalUnits(
                            p.AsDouble(), UnitTypeId.CubicMetersPerHour);
                        systems.Add(new HVACSystem("Supply", HVACSystemType.Supply,
                            Math.Round(flow, 2)));
                        break;
                    }
                }

                foreach (var name in exhaustNames)
                {
                    var p = space.LookupParameter(name);
                    if (p != null && p.HasValue && p.AsDouble() > 0)
                    {
                        double flow = UnitUtils.ConvertFromInternalUnits(
                            p.AsDouble(), UnitTypeId.CubicMetersPerHour);
                        systems.Add(new HVACSystem("Exhaust", HVACSystemType.Exhaust,
                            Math.Round(flow, 2)));
                        break;
                    }
                }
            }
            catch
            {
            }

            return systems;
        }

        public void AssignSystemToRoom(string roomId, HVACSystem system)
        {
            using var tx = new Transaction(_doc, "Assign System to Room");
            tx.Start();

            if (ElementId.TryParse(roomId, out var id))
            {
                var space = _doc.GetElement(id) as Space;
                if (space != null)
                {
                    var param = space.LookupParameter("Comments");
                    if (param != null && !param.IsReadOnly)
                        param.Set($"{system.Name} [{system.Type}]: {system.FlowRate} m3/h");
                }
            }

            tx.Commit();
        }
    }
}
