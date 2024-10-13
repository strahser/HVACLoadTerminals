using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using System.Linq;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Mechanical;
using System.Diagnostics;
using HVACLoadTerminals.Utils;
using System.Windows;
using HVACLoadTerminals.Views;

namespace HVACLoadTerminals.Commands
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class RoomsBounding : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            // Получение текущего документа
            Document doc = commandData.Application.ActiveUIDocument.Document;
            RoomBounding roomBounding = new RoomBounding(doc);
            Window View = new FaceDataWindow(roomBounding.FaceDataList);
            View.ShowDialog();
            return Result.Succeeded;
        }
    }
    
}
