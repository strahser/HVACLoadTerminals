using System.Collections.Generic;
using HVACLoadTerminals.Core.Models;

namespace HVACLoadTerminals.Core.Interfaces
{
    public interface IRoomSystemProvider
    {
        IReadOnlyList<HVACSystem> GetSystemsForRoom(string roomId);
        void AssignSystemToRoom(string roomId, HVACSystem system);
    }
}
