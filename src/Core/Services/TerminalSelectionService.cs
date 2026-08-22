using System;
using System.Collections.Generic;
using System.Linq;
using HVACLoadTerminals.Core.Models;

namespace HVACLoadTerminals.Core.Services
{
    public class TerminalSelectionService
    {
        public IReadOnlyList<TerminalDevice> SelectOptimalDevices(
            double requiredFlowRate,
            IReadOnlyList<TerminalDevice> catalog,
            out int deviceCount)
        {
            var compatible = catalog
                .Where(d => d.MaxFlowRate > 0)
                .OrderBy(d => d.MaxFlowRate)
                .ToList();

            if (compatible.Count == 0)
            {
                deviceCount = 0;
                return Array.Empty<TerminalDevice>();
            }

            var bestDevice = compatible[0];
            deviceCount = (int)Math.Ceiling(requiredFlowRate / bestDevice.MaxFlowRate);

            for (int i = 1; i < compatible.Count; i++)
            {
                var device = compatible[i];
                int count = (int)Math.Ceiling(requiredFlowRate / device.MaxFlowRate);
                if (count < deviceCount)
                {
                    deviceCount = count;
                    bestDevice = device;
                }
            }

            return Enumerable.Repeat(bestDevice, deviceCount).ToList();
        }

        public (IReadOnlyList<TerminalDevice> Devices, int Count) SelectWithConstraints(
            double requiredFlowRate,
            IReadOnlyList<TerminalDevice> catalog,
            int maxDevices)
        {
            var compatible = catalog
                .Where(d => d.MaxFlowRate > 0)
                .OrderByDescending(d => d.MaxFlowRate)
                .ToList();

            if (compatible.Count == 0)
                return (Array.Empty<TerminalDevice>(), 0);

            int bestCount = int.MaxValue;
            TerminalDevice? bestDevice = null;

            foreach (var device in compatible)
            {
                int count = (int)Math.Ceiling(requiredFlowRate / device.MaxFlowRate);
                if (count <= maxDevices && count < bestCount)
                {
                    bestCount = count;
                    bestDevice = device;
                }
            }

            if (bestDevice == null)
            {
                bestDevice = compatible[0];
                bestCount = Math.Min(maxDevices, (int)Math.Ceiling(requiredFlowRate / bestDevice.MaxFlowRate));
            }

            var devices = new List<TerminalDevice>();
            for (int i = 0; i < bestCount; i++)
                devices.Add(bestDevice);

            return (devices, bestCount);
        }

        /// <summary>
        /// Selects <paramref name="count"/> devices of a single family for the given
        /// system type according to the placement mode:
        /// <list type="bullet">
        /// <item><see cref="PlacementMode.ByCalculation"/> / <see cref="PlacementMode.ByStep"/> —
        /// the best device (fewest units needed, i.e. highest flow) is repeated
        /// <paramref name="count"/> times.</item>
        /// <item><see cref="PlacementMode.ByCount"/> — the largest-flow device of the matching
        /// system type is repeated exactly <paramref name="count"/> times.</item>
        /// </list>
        /// The count itself is expected to come from <see cref="QuantityCalculator"/>.
        /// Returns an empty list when the catalog has no compatible device or count &lt; 1.
        /// </summary>
        public IReadOnlyList<TerminalDevice> SelectDevicesForQuantity(
            IReadOnlyList<TerminalDevice> catalog,
            HVACSystemType systemType,
            int count,
            PlacementOptions options)
        {
            if (catalog == null || count < 1)
                return Array.Empty<TerminalDevice>();

            var compatible = catalog
                .Where(d => d.SystemType == systemType && d.MaxFlowRate > 0)
                .ToList();

            if (compatible.Count == 0)
                return Array.Empty<TerminalDevice>();

            var best = PickBestDevice(compatible)!;
            if (options.Mode == PlacementMode.ByCount)
            {
                best = compatible.OrderByDescending(d => d.MaxFlowRate).First();
            }

            return Enumerable.Repeat(best, count).ToList();
        }

        /// <summary>
        /// Returns the device from <paramref name="compatible"/> that needs the fewest
        /// units for a given load — for a fixed required flow that is the device with the
        /// highest <see cref="TerminalDevice.MaxFlowRate"/> (minimal ceil ratio). Returns
        /// null when the list is empty or contains only zero-flow devices.
        /// </summary>
        public TerminalDevice? PickBestDevice(IReadOnlyList<TerminalDevice> compatible)
        {
            if (compatible == null || compatible.Count == 0)
                return null;

            return compatible
                .Where(d => d.MaxFlowRate > 0)
                .OrderByDescending(d => d.MaxFlowRate)
                .FirstOrDefault();
        }

        /// <summary>
        /// Analog-style size selection (plan card C1.4, mirrors
        /// <c>ChooseTerminalsInstanceFromDB</c>): fewest units first, then — within
        /// that count — the SMALLEST capacity still covering load/n, i.e. minimal
        /// reserve / highest loading factor k_ef. Returns device and count; null when
        /// nothing fits.
        /// </summary>
        public (TerminalDevice? Device, int Count) SelectBestForLoad(
            IReadOnlyList<TerminalDevice> compatible,
            double requiredLoad)
        {
            var valid = (compatible ?? Array.Empty<TerminalDevice>())
                .Where(d => d.MaxFlowRate > 0)
                .ToList();

            if (valid.Count == 0 || requiredLoad <= 0)
                return (null, 0);

            int minCount = valid.Min(d =>
                (int)Math.Ceiling(requiredLoad / d.MaxFlowRate));

            double flowPerDevice = requiredLoad / minCount;
            var best = valid
                .Where(d => (int)Math.Ceiling(requiredLoad / d.MaxFlowRate) == minCount)
                .OrderBy(d => d.MaxFlowRate)                 // smallest capacity = min reserve
                .First(d => d.MaxFlowRate >= flowPerDevice - 1e-9);

            return (best, minCount);
        }
    }
}
