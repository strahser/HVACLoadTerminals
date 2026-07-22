using System.Collections.Generic;
using HVACLoadTerminals.Core.Models;

namespace HVACLoadTerminals.Core.Interfaces
{
    public interface ITerminalCatalogRepository
    {
        IReadOnlyList<TerminalDevice> GetAllDevices();
        IReadOnlyList<TerminalDevice> GetDevicesBySystemType(HVACSystemType systemType);
        TerminalDevice? GetDeviceById(string id);
    }
}
