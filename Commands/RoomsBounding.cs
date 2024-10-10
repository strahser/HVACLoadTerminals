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

namespace HVACLoadTerminals.Commands
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class RoomsBounding : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            // Получение текущего документа
            Document doc = commandData.Application.ActiveUIDocument.Document;
            List<Element> rooms = new FilteredElementCollector(doc)
              .OfCategory(BuiltInCategory.OST_Rooms)
              .WhereElementIsNotElementType()
              .ToElements()
            .ToList();

            RoomBounding roomBounding = new RoomBounding(rooms,doc);
            roomBounding.GetRoomFaces();
            return Result.Succeeded;
        }
    }
    
}
