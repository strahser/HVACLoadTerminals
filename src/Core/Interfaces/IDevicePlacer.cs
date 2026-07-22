using System.Collections.Generic;
using HVACLoadTerminals.Core.Models;

namespace HVACLoadTerminals.Core.Interfaces
{
    public interface IDevicePlacer
    {
        void PlaceDevices(IReadOnlyList<DevicePlacement> placements);
        void RemovePlacements(string roomId);
        void ShowPreview(IReadOnlyList<DevicePlacement> placements);
    }
}
