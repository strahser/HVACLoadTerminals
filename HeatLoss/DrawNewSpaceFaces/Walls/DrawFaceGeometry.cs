using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace HVACLoadTerminals.DrawNewSpaceFaces.Walls
{
    internal class DrawFaceGeometry
    {
        /// <summary>
        /// Вспомогательный класс для отрисовки толщины грани
        /// </summary>
        private Document doc;
        public DrawFaceGeometry(Document _doc)
        {
            doc = _doc;
        }
    
        // Метод преобразования Face в Solid
        private Solid CreateSolidFromFace(Face face)
        {
            // Константа для толщины
            const double thickness = 100; // 100 мм

            // Получаем нормаль поверхности Face
            var faceOrientation = face.ComputeNormal(new UV(.5, .5));

            // Получаем контуры Face
            var loops = face.GetEdgesAsCurveLoops();

            // Создаем список для смещенных контуров
            var offsetloops = new List<CurveLoop>();

            // Вычисляем смещение для контура
            var HH = faceOrientation.Multiply(thickness);

            // Создаем смещенные контуры
            foreach (var L in loops)
            {
                // Создаем смещение для контура
                var offsetloop = CurveLoop.CreateViaTransform(L, Transform.CreateTranslation(HH));

                // Добавляем исходный контур и смещенный контур
                offsetloops.Add(L);
                offsetloops.Add(offsetloop);
            }

            // Создаем Solid с помощью lofting
            var options = new SolidOptions(ElementId.InvalidElementId, ElementId.InvalidElementId);
            var faceSolid = GeometryCreationUtilities.CreateLoftGeometry(offsetloops, options);

            // Возвращаем Solid
            return faceSolid;
        }


    }

}

