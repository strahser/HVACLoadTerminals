using System.Collections.Generic;

namespace HVACLoadTerminals.Core.Services
{
    // Минимальный DTO для десериализации heatgain.v1 sidecar (HG-JSON-3).
    // Совпадает с HeatGainCalculator.Core.Models.HeatGain.HeatGainSnapshotDto (heatgain.v1).
    public class HeatGainSnapshotDto
    {
        public string SchemaVersion { get; set; } = "heatgain.v1";
        public string DataVersion { get; set; } = "";
        public string Method { get; set; } = "SP";
        public string City { get; set; } = "";
        public int Hour { get; set; }
        public int PeakHour { get; set; }
        public List<HeatGainRoomDto> Rooms { get; set; } = new();
    }

    public class HeatGainRoomDto
    {
        public string RoomId { get; set; } = "";
        public string RoomNumber { get; set; } = "";
        public double CoolingLoadW { get; set; }
        public double SensibleW { get; set; }
        public double LatentW { get; set; }
        public Dictionary<string, double> BySource { get; set; } = new();
    }
}
