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
            Viewer viewer = new Viewer(doc);
            viewer.ShowDialog();
            //Dictionary<string, List<FamilySymbol>> winFamilyTypes = CollectorQuery.FindFamilyTypes(doc, BuiltInCategory.OST_DuctTerminal);
            //foreach (KeyValuePair<string, List<FamilySymbol>> entry in winFamilyTypes)
            //{
            //    Debug.Write(entry.Key);
            //    if (entry.Key == "ADSK_Диффузор_Круглый_Приточный")
            //    {
            //        foreach(Element fs in entry.Value)
            //        {
            //        Debug.Write("family instance",fs.Name);
            //            List<string> param = CollectorQuery.GetParameters(fs);
            //            foreach (string e in param)
            //            {
            //                if (e == "Макс. расход") {
            //                    try
            //                    {
            //                        double gpv = fs.LookupParameter("Макс. расход").AsDouble();
            //                        Debug.Write(e, gpv.ToString());
            //                    }
            //                    catch { }
            //                }
            //            }
            //        }
            //    }
        //}
  

            return Result.Succeeded;
        }


        }


}
