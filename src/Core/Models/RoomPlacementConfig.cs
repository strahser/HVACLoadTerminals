using System;
using System.Collections.Generic;

namespace HVACLoadTerminals.Core.Models
{
    public class RoomPlacementConfig
    {
        public string RoomId { get; }

        /// <summary>Family names allowed for this room. Empty = any family allowed.</summary>
        public IReadOnlyList<string> AllowedFamilyNames { get; }

        public PlacementOptions Options { get; }

        public RoomPlacementConfig(
            string roomId,
            IReadOnlyList<string>? allowedFamilyNames = null,
            PlacementOptions? options = null)
        {
            RoomId = roomId ?? throw new ArgumentNullException(nameof(roomId));
            AllowedFamilyNames = allowedFamilyNames ?? new List<string>();
            Options = options ?? PlacementOptions.Default;
        }
    }
}
