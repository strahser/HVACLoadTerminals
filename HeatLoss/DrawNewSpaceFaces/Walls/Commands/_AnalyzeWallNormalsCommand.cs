using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace HVACLoadTerminals.HeatLoss.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class _AnalyzeWallNormalsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;

            // Получаем все стены в модели
            FilteredElementCollector wallsCollector = new FilteredElementCollector(doc)
                .OfClass(typeof(Wall));

            // Выбираем внутреннюю точку здания
            XYZ interiorPoint = GetInteriorPoint(uidoc);

            using (Transaction tx = new Transaction(doc, "Анализ нормалей стен"))
            {
                tx.Start();

                foreach (Wall wall in wallsCollector)
                {
                    if (IsExternalWallByNormal(wall, interiorPoint))
                    {
                        TaskDialog.Show("Результат", $"Наружная стена найдена: {wall.Id}");
                    }
                }

                tx.Commit();
            }

            return Result.Succeeded;
        }

        private bool IsExternalWallByNormal(Wall wall, XYZ interiorPoint)
        {
            Options options = new Options();
            GeometryElement geometry = wall.get_Geometry(options);

            foreach (GeometryObject obj in geometry)
            {
                if (obj is Solid solid)
                {
                    foreach (Face face in solid.Faces)
                    {
                        XYZ normal = (face.ComputeNormal(UV.Zero)).Normalize();
                        LocationCurve locationCurve = wall.Location as LocationCurve;
                        if (locationCurve == null) continue;

                        Curve curve = locationCurve.Curve;
                        XYZ wallPoint = curve.GetEndPoint(0); // Берем первую точку локации стены

                        if (normal.DotProduct(wallPoint) < 0)
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        private XYZ GetInteriorPoint(UIDocument uidoc)
        {
            // Пользователь выбирает внутреннюю точку здания
            XYZ selectedPoint = uidoc.Selection.PickPoint("Выберите внутреннюю точку здания");
            return selectedPoint;
        }
    }
}