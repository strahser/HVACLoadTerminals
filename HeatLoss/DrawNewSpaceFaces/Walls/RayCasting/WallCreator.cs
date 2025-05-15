using Autodesk.Revit.DB;

namespace HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls.RayCasting
{
    public class WallCreator
    {
        public Document _doc;

        public WallCreator(Document doc)
        {
            _doc = doc;
        }

        public void CreateWall(Curve curve, ElementId levelId)
        {
            const double wallHeight = 9.19;
            try
            {
                WallType wallType = new FilteredElementCollector(_doc)
                    .OfClass(typeof(WallType))
                    .FirstElement() as WallType;

                if (wallType == null) return;
                Wall.Create(_doc, curve, wallType.Id, levelId, wallHeight, 0, false, false);
            }
            catch { }
        }
    }
}