using HVACLoadTerminals.Core.Models;

namespace HVACLoadTerminals.Infrastructure.Presentation
{
    /// <summary>U2.1: wall edge chosen by a mass placement pattern —
    /// hosts highlight it on the plan (supply/exhaust color per system).</summary>
    public class PatternEdge
    {
        public string LevelName { get; set; } = "";
        public string SystemName { get; set; } = "";
        public Point2D Start { get; set; } = new Point2D(0, 0);
        public Point2D End { get; set; } = new Point2D(0, 0);
    }
}
