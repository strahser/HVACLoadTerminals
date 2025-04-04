
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HVACLoadTerminals.PipeSewageHandler;

 [Transaction(TransactionMode.Manual)]
    public class TestSplitPipeCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;

            try
            {
                // 1. Выбор трубы
                Reference pipeReference = uidoc.Selection.PickObject(ObjectType.Element, new PipeSelectionFilter(), "Выберите трубу");
                Pipe pipe = doc.GetElement(pipeReference) as Pipe;

                if (pipe == null)
                {
                    message = "Выбранный элемент не является трубой.";
                    return Result.Failed;
                }

                // 2. Получение кривой трубы
                Curve curve = (pipe.Location as LocationCurve).Curve;
                XYZ startPoint = curve.GetEndPoint(0);
                XYZ endPoint = curve.GetEndPoint(1);
                XYZ midPoint = (startPoint + endPoint) / 2;

                // 3. Разделение трубы
                using (Transaction transaction = new Transaction(doc, "Разделить трубу по середине"))
                {
                    transaction.Start();
                    PlumbingUtils.BreakCurve(doc, pipe.Id, midPoint);
                    transaction.Commit();
                }

                TaskDialog.Show("Успех", "Труба успешно разделена по середине.");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }

    //Фильтр для выбора трубы.
    public class PipeSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            return elem is Pipe;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }
    }