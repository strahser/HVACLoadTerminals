using Autodesk.Revit.DB;
using HVACLoadTerminals.Utils;

namespace HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls.Core
{
    public class WallCreator
    {
        public Document _doc = RevitConfig.Document;

        public Wall CreateWall(Curve curve, ElementId levelId)
        {
            const double wallHeight = 9.19;
            try
            {
                WallType wallType = new FilteredElementCollector(_doc)
                    .OfClass(typeof(WallType))
                    .FirstElement() as WallType;

                if (wallType == null) return null;
                var wall = Wall.Create(_doc, curve, wallType.Id, levelId, wallHeight, 0, false, false);
                return wall;
            }
            catch { }
            return null;
        }
    }
}