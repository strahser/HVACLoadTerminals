using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using HVACLoadTerminals.ModelsStatic;
using HVACLoadTerminals.Utils;

namespace HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.WindowsDoors
{
    internal class OpensHandler(Document hvacDocument, Document roomDocument)
    {
        private static readonly List<string> TransferParameters = ConstructionSurfaceModel.TransferParameters;

        private static bool IsExternalElement(Element element)
        {
            if (element is FamilyInstance instance)
            {
                var fromRoom = instance.FromRoom;
                var toRoom = instance.ToRoom;
                return fromRoom == null || toRoom == null;
            }
            return false;
        }

        // Получение последней фазы проекта
        Phase GetLastPhase(Document doc)
        {
            var phases = doc.Phases;
            return phases.Cast<Phase>().LastOrDefault();
        }

        private List<Element> GetExternalOpens(List<Element> Collection)
        {
            //var externalWindows = CollectorQuery.GetAllWindows(doc)
           // .Where(w => IsExternalElement(w, lastPhase))
             //   .ToList();
             
            var doc = roomDocument; // текущий документ
            var lastPhase = GetLastPhase(doc);
            if (lastPhase == null) return []; // проверка наличия фаз

            return Collection
                .Where(w => IsExternalElement(w))
                .ToList();
        }
        public void DrawWindows(List<Element> walls)
        {
            var roomWidowsList = GetExternalOpens(CollectorQuery.GetAllWindows(roomDocument));
            var windowsSymbols = CollectorQuery.GetAllWindowsFamilySymbols(hvacDocument);
            var windowSymbol = windowsSymbols.FirstOrDefault() as FamilySymbol;
            DrawOpensForSelectedWalls(walls,roomWidowsList, windowSymbol, EnclosureTypeOptions.Window);
        }
        
        public void DrawDoors(List<Element> walls)
        {
            var roomDoorsList = GetExternalOpens(CollectorQuery.GetAllDoors(roomDocument));
            var doorsSymbols = CollectorQuery.GetAllDoorsFamilySymbols(hvacDocument);
            var doorSymbol = doorsSymbols.FirstOrDefault() as FamilySymbol;
            DrawOpensForSelectedWalls(walls,roomDoorsList, doorSymbol, EnclosureTypeOptions.Door);
        }
        
        private void DrawOpensForSelectedWalls(List<Element> walls, List<Element> openings, FamilySymbol familySymbol, string openingType)
        {
            if (openings == null || openings.Count == 0 || familySymbol == null) return;
            var count = 0;
            foreach (var wall in walls.Cast<Wall>())
            {
                try
                {
                    var createdOpenings = DrawBaseOpens(wall, openings, familySymbol, openingType);
                    count += createdOpenings.Count;
                }
                catch (Autodesk.Revit.Exceptions.ArgumentException ex)
                {
                    // Обработка конкретного исключения, например, несоответствие параметров
                    Debug.Write($"Ошибка при создании {openingType}: {ex.Message}");
                }
                catch (Exception ex)
                {
                    // Обработка других исключений
                    Debug.Write($"Непредвиденная ошибка при создании {openingType}: {ex.Message}");
                }
            }

            MessageBox.Show($"Создано {count} {openingType}");
        }
        
        private List<FamilyInstance>  DrawBaseOpens(Wall wall, List<Element> opensList, FamilySymbol opensInstance, string enclosureType)
        {
            if (opensInstance == null)
            {
                TaskDialog.Show("Error", "Не найдено семейство стены/окна");
            }
            var openList = new List<FamilyInstance>();
            // Создание окна, если точка вставки находится внутри ограничивающего прямоугольника стены
            foreach (var element in opensList)
            {
                var open = (FamilyInstance)element;
                // Получение уровня стены
                var level = hvacDocument.GetElement(wall.LevelId) as Level;
                var wallBoundingBox = wall.get_BoundingBox(null);
                var locationWindowPoint = (LocationPoint)open.Location;
                // Получение точки вставки окна
                var windowInsertionPoint = locationWindowPoint.Point;
                // Проверка, находится ли точка вставки внутри ограничивающего прямоугольника стены
                if (CheckIsPointInBoundBox(wallBoundingBox, windowInsertionPoint))
                {
                    // Создание окна.
                    using (var transaction = new Transaction(hvacDocument, $"Создать {enclosureType} {open.Name}"))
                    {
                        transaction.Start();
                        var newOpen = hvacDocument.Create.NewFamilyInstance(windowInsertionPoint, 
                            opensInstance, wall, level, StructuralType.NonStructural);
                        SetOpensParameters(wall, enclosureType, open, newOpen);
                        transaction.Commit();
                        openList.Add(newOpen);
                    }
                }
            }
            return openList;
        }

        private void SetOpensParameters(Wall wall, string enclosureType, FamilyInstance open, FamilyInstance newOpen)
        {
            // Установка параметров для нового окна
            var transferCoefficientParam = open.Symbol.get_Parameter(BuiltInParameter.ANALYTICAL_HEAT_TRANSFER_COEFFICIENT);
            double uValue;

            // Проверка, существует ли параметр
            if (transferCoefficientParam != null)
            {
                // Получение значения параметра
                uValue = transferCoefficientParam.AsDouble();
            }
            else
            {
                uValue = 0;
            }
            var height = GetOpenDimensionParameterValue(open, BuiltInParameter.CASEWORK_HEIGHT);
            var width = GetOpenDimensionParameterValue(open, BuiltInParameter.GENERIC_WIDTH);
            ChangeOpensGeometryDimensionParameter(newOpen, BuiltInParameter.CASEWORK_HEIGHT,height);
            ChangeOpensGeometryDimensionParameter(newOpen, BuiltInParameter.GENERIC_WIDTH,width);

            Debug.Write($"{open.Name}-height {height}-width {width}");
            //забираем параметры из стены
            foreach(var parameter in TransferParameters)
            {
                var parameterValue = wall.LookupParameter(parameter).AsValueString();
                ParametersUtility.SetParameterByValueAndName(newOpen, parameter, parameterValue);
            }
            //забираем параметры окон дверей из связанного документа
            ParametersUtility.SetParameterByValueAndName(newOpen, nameof(ConstructionSurfaceModel.TransferCoefficient), uValue);
            ParametersUtility.SetParameterByValueAndName(newOpen, nameof(ConstructionSurfaceModel.ConstructionName), open.Name);
            ParametersUtility.SetParameterByValueAndName(newOpen, nameof(ConstructionSurfaceModel.EnclosureType), enclosureType);
        }


        private static double GetOpenDimensionParameterValue(FamilyInstance element, BuiltInParameter parameter)
        {
            var elementParameterValue = element.get_Parameter(parameter).AsDouble();
            var symbolParameterValue = element.Symbol.get_Parameter(parameter).AsDouble();
            if(symbolParameterValue != 0)
            {
                return symbolParameterValue;
            }
            if (elementParameterValue != 0)
            { return elementParameterValue; }
            else{
                var aReaFt = element.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED).AsDouble();
                return Math.Sqrt(aReaFt); 
            }
        }

        
   
        private static void ChangeOpensGeometryDimensionParameter(FamilyInstance newOpenInstance, BuiltInParameter parameter, double parameterValue)
       {
            var newOpenParameterValue = newOpenInstance.get_Parameter(parameter);
            if (newOpenParameterValue != null && parameterValue > 0)
            {
                newOpenParameterValue.Set(parameterValue);

            }
       }
      


        private static bool CheckIsPointInBoundBox(BoundingBoxXYZ boundingBox, XYZ locationPoint)
        {
            // Проверка, находится ли точка внутри BoundingBox
            if (boundingBox.Min.X <= locationPoint.X + 1 && boundingBox.Max.X >= locationPoint.X - 1 &&
                boundingBox.Min.Y <= locationPoint.Y + 1 && boundingBox.Max.Y >= locationPoint.Y - 1 &&
                boundingBox.Min.Z <= locationPoint.Z + 1 && boundingBox.Max.Z >= locationPoint.Z + 1)
                return true;
            else
            {
                return false;
            }
        }
        
        private static void GetParameterFromWallAndSetToWindowAsString( Element wall, Element window, string parameterName)
        {
            var parameterValue = wall.LookupParameter(parameterName).AsString();
            ParametersUtility.SetParameterByValueAndName(window, parameterName, parameterValue);
        }
    }
}
