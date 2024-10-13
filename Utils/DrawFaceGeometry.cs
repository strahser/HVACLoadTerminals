using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using HVACLoadTerminals.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web.UI.WebControls;
using System.Windows;
using System.Windows.Forms;

namespace HVACLoadTerminals.Utils
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
            XYZ FaceOrientation = face.ComputeNormal(new UV(.5, .5));

            // Получаем контуры Face
            IList<CurveLoop> loops = face.GetEdgesAsCurveLoops();

            // Создаем список для смещенных контуров
            List<CurveLoop> Offsetloopssss = new List<CurveLoop>();

            // Вычисляем смещение для контура
            XYZ HH = FaceOrientation.Multiply(Thickness);

            // Создаем смещенные контуры
            foreach (CurveLoop L in loops)
            {
                // Создаем смещение для контура
                CurveLoop Offsetloop = CurveLoop.CreateViaTransform(L, Transform.CreateTranslation(HH));

                // Добавляем исходный контур и смещенный контур
                Offsetloopssss.Add(L);
                Offsetloopssss.Add(Offsetloop);
            }

            // Создаем Solid с помощью lofting
            SolidOptions options = new SolidOptions(ElementId.InvalidElementId, ElementId.InvalidElementId);
            Solid FaceSolid = GeometryCreationUtilities.CreateLoftGeometry(Offsetloopssss, options);

            // Возвращаем Solid
            return FaceSolid;
        }


    }

}

