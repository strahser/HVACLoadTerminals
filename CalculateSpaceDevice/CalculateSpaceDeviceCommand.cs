
using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using HVACLoadTerminals.Views;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI.Selection;
using HVACLoadTerminals.Utils;


namespace HVACLoadTerminals.CalculateSpaceDevice
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
                var pickedRef = RevitConfig.UiDocument.Selection.PickObject(ObjectType.Element, "Выберите пространство");
                selectedElement = RevitConfig.Document.GetElement(pickedRef);
            }
            catch (Exception)
            {
                selectedElement = null;
            };
            // Проверяем, является ли выбранный элемент пространством.
            if (selectedElement is Space space)
            {
                var spaceBoundary = new SpaceBoundaryCurve(space);
                // Открываем диалоговое окно для выбора кривой и расстояния смещения
                var connection = RevitConfig.Connection;
                connection.Open();
                var dialog = new OffsetDialog(connection, spaceBoundary);
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
