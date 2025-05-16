using Autodesk.Revit.DB;

namespace HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls.RayCasting
{
    public class WallCreator(Document doc)
    {
        public Document _doc = doc;

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