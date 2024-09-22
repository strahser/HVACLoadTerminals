
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using HVACLoadTerminals.Views;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI.Selection;
using Autodesk.Revit.Attributes;
using System.IO;
using System.Linq;
using System;
using System.Data.Common;
using System.Data.SQLite;


namespace HVACLoadTerminals.Commands
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class CalculateSpaceDevice : IExternalCommand
    {

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            RevitConfig.Initialize(commandData);
            Element selectedElement;
            try
            {
                Reference pickedRef = RevitConfig.UiDocument.Selection.PickObject(ObjectType.Element, "Выберите пространство");
                selectedElement = RevitConfig.Document.GetElement(pickedRef);
            }
            catch
            {
                selectedElement = null;
            };


            // Проверяем, является ли выбранный элемент пространством.
            if (selectedElement is Space space)
            {
                SpaceBoundary spaceBoundary = new SpaceBoundary(space);
                // Открываем диалоговое окно для выбора кривой и расстояния смещения
                var cleanCurves = spaceBoundary.cleanCurves;
                SQLiteConnection connection = RevitConfig.connection;
                connection.Open();
                OffsetDialog dialog = new OffsetDialog(connection, spaceBoundary);
                dialog.ShowDialog();
                return Result.Succeeded;
            }
            if (selectedElement == null)
            {
                return Result.Failed;
            }
            else
            {
                TaskDialog.Show("Ошибка", "Выбранный элемент не является пространством.");
                return Result.Failed;
            }


        }
    }
}
