using Autodesk.Revit.DB;
using System;

namespace HVACLoadTerminals.Utils
{
    internal static class CollectorGeometry
    {
        public static double GetWindowLocationHeight(Document doc, Element window)
        {
            var locationPoint = window.Location as LocationPoint;
            if (locationPoint != null)
            {
                var point = locationPoint.Point;
                return point.Z; // Z-координата — высота
            }


            //Если нет LocationPoint, можно попробовать получить центр BoundingBox
            var bb = window.get_BoundingBox(doc.ActiveView);

            if (bb != null)
            {
                var centerPoint = (bb.Max + bb.Min) / 2.0;
                return centerPoint.Z; // Возвращаем Z-координату центра BoundingBox
            }

            return 0; // Если не удалось получить координаты
        }

        public static double? GetWindowHeights(Document doc, Element element)
        {
                
            if (element.Category.Id == new ElementId(BuiltInCategory.OST_Windows))
            {
                try
                {
                    var height = GetWindowLocationHeight(doc, element);
                    return height;

                }
                catch (ArgumentException ex)
                {
                    return null;
                }
            }
            else {return null; }            
        }
    }
}
