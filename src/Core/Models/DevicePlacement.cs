namespace HVACLoadTerminals.Core.Models
{
    public class DevicePlacement
    {
        public TerminalDevice Device { get; }
        public Point2D Position { get; }
        public double Rotation { get; }
        public string RoomId { get; }
        public string SystemName { get; }

        public DevicePlacement(
            TerminalDevice device,
            Point2D position,
            double rotation,
            string roomId,
            string systemName)
        {
            Device = device;
            Position = position;
            Rotation = rotation;
            RoomId = roomId;
            SystemName = systemName;
        }
    }
}
