using System;

namespace HVACLoadTerminals.Core.Exceptions
{
    public class PlacementException : Exception
    {
        public string RoomId { get; }

        public PlacementException(string roomId, string message)
            : base($"Room {roomId}: {message}")
        {
            RoomId = roomId;
        }

        public PlacementException(string roomId, string message, Exception inner)
            : base($"Room {roomId}: {message}", inner)
        {
            RoomId = roomId;
        }
    }
}
