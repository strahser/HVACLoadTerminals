using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using System.IO;

namespace HVACLoadTerminals
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class ExcternalCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            Document doc = uiApp.ActiveUIDocument.Document;
            // wpf viewer form
            //Viewer viewer = new Viewer(doc);
            //viewer.ShowDialog();
            SpatialElementGeometryCalculator calculator = new SpatialElementGeometryCalculator(doc);
            var el= CollectorQuery.GetAllSpaces(doc);


            SpatialElementGeometryResults results = calculator.CalculateSpatialElementGeometry((SpatialElement)el[0]);

            Solid roomSolid = results.GetGeometry();

            foreach (Face face in roomSolid.Faces)
            {
                var curves = face.GetEdgesAsCurveLoops()[0];
                foreach (Curve curve in curves)
                {
                    Debug.Write(curve.Length.ToString());
                }
                

            }
            return Result.Succeeded;
        }


        }


}
