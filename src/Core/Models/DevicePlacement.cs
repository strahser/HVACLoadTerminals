namespace HVACLoadTerminals.Core.Models
{
    public class DevicePlacement
    {
        public TerminalDevice Device { get; }
        public Point2D Position { get; }
        public double Rotation { get; }
        public string RoomId { get; }
        public string SystemName { get; }

        /// <summary>Index of the wall edge this device attaches to. -1 = not set.</summary>
        public int EdgeIndex { get; }

        /// <summary>Which side (Bottom/Right/Top/Left) the device is on. Auto = not set.</summary>
        public CoordinateSystem WallSide { get; }

        public DevicePlacement(
            TerminalDevice device,
            Point2D position,
            double rotation,
            string roomId,
            string systemName,
            int edgeIndex = -1,
            CoordinateSystem wallSide = CoordinateSystem.Auto)
        {
            Device = device;
            Position = position;
            Rotation = rotation;
            RoomId = roomId;
            SystemName = systemName;
            EdgeIndex = edgeIndex;
            WallSide = wallSide;
        }
    }
}
