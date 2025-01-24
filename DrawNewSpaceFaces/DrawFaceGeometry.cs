using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace HVACLoadTerminals.DrawNewSpaceFaces
{
    internal class DrawFaceGeometry
    {
        private Document doc;
        public DrawFaceGeometry(Document _doc)
        {
            doc = _doc;
        }
    
        // Метод преобразования Face в Solid
        private Solid CreateSolidFromFace(Face face)
        {
            // Константа для толщины
            const double Thickness = 100; // 100 мм

            // Получаем нормаль поверхности Face
            var FaceOrientation = face.ComputeNormal(new UV(.5, .5));

            // Получаем контуры Face
            var loops = face.GetEdgesAsCurveLoops();

            // Создаем список для смещенных контуров
            var Offsetloopssss = new List<CurveLoop>();

            // Вычисляем смещение для контура
            var HH = FaceOrientation.Multiply(Thickness);

            // Создаем смещенные контуры
            foreach (var L in loops)
            {
                // Создаем смещение для контура
                var Offsetloop = CurveLoop.CreateViaTransform(L, Transform.CreateTranslation(HH));

                // Добавляем исходный контур и смещенный контур
                Offsetloopssss.Add(L);
                Offsetloopssss.Add(Offsetloop);
            }

            // Создаем Solid с помощью lofting
            var options = new SolidOptions(ElementId.InvalidElementId, ElementId.InvalidElementId);
            var FaceSolid = GeometryCreationUtilities.CreateLoftGeometry(Offsetloopssss, options);

            // Возвращаем Solid
            return FaceSolid;
        }


    }

}

