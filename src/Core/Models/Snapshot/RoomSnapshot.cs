using System.Collections.Generic;

namespace HVACLoadTerminals.Core.Models.Snapshot
{
    /// <summary>
    /// Raw room snapshot produced by HeatLossRevit2 (schemaVersion 1.x).
    /// Coordinates and sizes are in Revit internal units (feet);
    /// area is m2, height/volume are meters/m3.
    /// </summary>
    public class RoomSnapshot
    {
        public SnapshotMetadata Metadata { get; set; } = new SnapshotMetadata();
        public List<SnapshotLevel> Levels { get; set; } = new List<SnapshotLevel>();
        public List<SnapshotRoom> Rooms { get; set; } = new List<SnapshotRoom>();
        public List<SnapshotOpening> Openings { get; set; } = new List<SnapshotOpening>();
        public List<SnapshotWall> Walls { get; set; } = new List<SnapshotWall>();
        public List<SnapshotValve> Valves { get; set; } = new List<SnapshotValve>();
    }

    public class SnapshotMetadata
    {
        public string SchemaVersion { get; set; } = "";
        public string DocumentTitle { get; set; } = "";
        public string DocumentPath { get; set; } = "";
        public string SnapshotType { get; set; } = "";
        public string CreatedAtLocal { get; set; } = "";
    }

    public class SnapshotLevel
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public double Elevation { get; set; }
    }

    public class SnapshotRoom
    {
        public string Id { get; set; } = "";

        /// <summary>Room number as stored in the model (may be non-numeric).</summary>
        public string Number { get; set; } = "";
        public string Name { get; set; } = "";
        public string LevelId { get; set; } = "";
        public string LevelName { get; set; } = "";

        /// <summary>Level floor elevation, feet.</summary>
        public double LevelElevation { get; set; }

        /// <summary>Room area, m2.</summary>
        public double Area { get; set; }

        /// <summary>Closed boundary polygon, feet: list of [x, y].</summary>
        public List<double[]> Polygon { get; set; } = new List<double[]>();

        /// <summary>Calculated temperature, C.</summary>
        public double Temperature { get; set; }
        public bool IsCorner { get; set; }

        /// <summary>Offset of the room upper limit above the level, feet.</summary>
        public double UpperLimitOffset { get; set; }

        /// <summary>Often 0 in raw snapshots — derive from walls instead.</summary>
        public double Volume { get; set; }
    }

    public class SnapshotBoundingBox
    {
        public double MinX { get; set; }
        public double MinY { get; set; }
        public double MinZ { get; set; }
        public double MaxX { get; set; }
        public double MaxY { get; set; }
        public double MaxZ { get; set; }

        /// <summary>Extent along X, feet.</summary>
        public double Width { get; set; }

        /// <summary>Extent along Y, feet.</summary>
        public double Height { get; set; }

        /// <summary>Depth (thickness), feet.</summary>
        public double Depth { get; set; }
        public double CenterX { get; set; }
        public double CenterY { get; set; }
        public double CenterZ { get; set; }
    }

    /// <summary>Window / door / stained-glass opening hosted by a wall.</summary>
    public class SnapshotOpening
    {
        public string Id { get; set; } = "";
        public string CoreElementId { get; set; } = "";

        /// <summary>Id of the space the opening belongs to.</summary>
        public string SpaceId { get; set; } = "";

        /// <summary>Id of the hosting wall element.</summary>
        public string HostWallId { get; set; } = "";
        public bool IsExternal { get; set; }

        /// <summary>Russian enclosure type: "Окно", "Дверь", "Витраж", "Стена".</summary>
        public string EnclosureType { get; set; } = "";
        public string FamilyName { get; set; } = "";
        public string FamilySymbolName { get; set; } = "";

        /// <summary>Width along the wall, feet.</summary>
        public double Width { get; set; }

        /// <summary>Height, feet.</summary>
        public double Height { get; set; }

        /// <summary>Glazing area, m2.</summary>
        public double Area { get; set; }

        /// <summary>Center height above the level, feet.</summary>
        public double CenterHeight { get; set; }

        /// <summary>Wall azimuth, degrees from north.</summary>
        public double Azimuth { get; set; }
        public SnapshotBoundingBox BoundingBox { get; set; } = new SnapshotBoundingBox();
        public bool IsFromLinkedFile { get; set; }
    }

    public class SnapshotLocationCurve
    {
        public string Type { get; set; } = "Line";
        public double StartX { get; set; }
        public double StartY { get; set; }
        public double StartZ { get; set; }
        public double EndX { get; set; }
        public double EndY { get; set; }
        public double EndZ { get; set; }
    }

    public class SnapshotWall
    {
        public string Id { get; set; } = "";
        public string CoreElementId { get; set; } = "";

        /// <summary>Id of the space the wall segment belongs to.</summary>
        public string SpaceId { get; set; } = "";
        public string LevelId { get; set; } = "";
        public string LevelName { get; set; } = "";

        /// <summary>Unconnected height, meters.</summary>
        public double Height { get; set; }

        /// <summary>Length, meters (often 0 — use LocationCurve).</summary>
        public double Length { get; set; }
        public string EnclosureType { get; set; } = "";
        public bool IsExternal { get; set; }
        public bool ArIsExternal { get; set; }
        public bool BoundaryIsExternal { get; set; }

        /// <summary>Resolved external flag (3-flag principle of HeatLossRevit2).</summary>
        public bool ResolvedExternal { get; set; }
        public SnapshotLocationCurve LocationCurve { get; set; } = new SnapshotLocationCurve();

        /// <summary>Azimuth, degrees from north.</summary>
        public double Azimuth { get; set; }
        public bool IsFromLinkedFile { get; set; }
        public List<string> OpeningIds { get; set; } = new List<string>();
    }

    /// <summary>Existing device found in the model (valve etc.) — occupied spot.</summary>
    public class SnapshotValve
    {
        public string Id { get; set; } = "";
        public string LevelId { get; set; } = "";
        public string LevelName { get; set; } = "";
        public SnapshotBoundingBox BoundingBox { get; set; } = new SnapshotBoundingBox();
        public SnapshotPoint LocationPoint { get; set; } = new SnapshotPoint();

        /// <summary>Air flow, l/s.</summary>
        public double AirFlow { get; set; }
        public string FamilyName { get; set; } = "";
        public bool IsFromLinkedFile { get; set; }
    }

    public class SnapshotPoint
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
    }
}
