using System;
using System.Windows;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using HVACLoadTerminals.HeatLoss.HeatLossResult.View;
using HVACLoadTerminals.Utils;

namespace HVACLoadTerminals.HeatLoss.HeatLossResult
{
    //https://thebuildingcoder.typepad.com/blog/2017/10/disjunct-outer-loops-from-planar-face-with-separate-parts.html
    //https://thebuildingcoder.typepad.com/blog/2013/07/football-and-space-adjacency-for-heat-load-calculation.html
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class HeatLossTableCommand : IExternalCommand
    {
        private Element _selectedElement;
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            RevitConfig.Initialize(commandData);

            Window view = new HeatLossWindow();
            if (view == null) throw new ArgumentNullException(nameof(view));
            view.ShowDialog();

            return Result.Succeeded;
        }
        private void SelectSpace()
        {
            try
            {
                var pickedRef =
                    RevitConfig.UiDocument.Selection.PickObject(ObjectType.Element, "Выберите пространство");
                _selectedElement = RevitConfig.Document.GetElement(pickedRef);
            }

            catch
            {
                _selectedElement = null;
            }

            

            // Проверяем, является ли выбранный элемент пространством.
            if (_selectedElement is Space space)
            {
                var typeId = space.SpaceTypeId;
                var spaceType = RevitConfig.Document.GetElement(typeId);
                var tin = Math.Round(spaceType.get_Parameter(BuiltInParameter.SPACE_HEATING_SET_POINT).AsDouble() - 273.15);

                MessageBox.Show($"Space temperature: {tin}");
                
            }
        }
    }
}
