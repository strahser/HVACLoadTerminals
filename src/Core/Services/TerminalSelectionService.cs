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
    }
}
