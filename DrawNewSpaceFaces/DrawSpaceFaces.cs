using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI;
using HVACLoadTerminals.CalculateSpaceDevice;
using HVACLoadTerminals.ModelsStatic;
using HVACLoadTerminals.Utils;

namespace HVACLoadTerminals.DrawNewSpaceFaces
{
    public class DrawSpaceFaces
    {
        private Document _roomDoc;
        private Document _hvacDocument;
        //конструктор
        public DrawSpaceFaces(Document doc)
        {
            _hvacDocument = doc;
            CreateLinkDoc(doc);
        }
        private void CreateLinkDoc(Document doc)
        {
            // Получить список связанных документов с помощью FilteredElementCollector
            var links = CollectorQuery.GetLinkedDocument(doc);
            if (links.Count > 0)
            {
                // Получить связанный документ
                var linkedDocument = links[0].GetLinkDocument();
                // Получить имя связанного документа
                var linkedDocumentName = linkedDocument.Title;
                _roomDoc = linkedDocument;
                // Вывести имя в консоль
                Debug.WriteLine("Имя связанного документа: " + linkedDocumentName);
            }
            else
            {
                Debug.WriteLine("Связанные документы не найдены.");
                _roomDoc = doc;
            }
        }
        public void DrawWalls(string northDirection)
        {
            var drawWallAndOpens = new DrawWallAndOpens(_hvacDocument);
            var wallList = new List<Wall>();
            var spaces = CollectorQuery.GetAllSpaces(_hvacDocument);
            var rooms = CollectorQuery.GetAllRooms(_roomDoc);
            foreach (var space in spaces.Cast<Space>())
            {
                var selectedRoom = RoomAndSpaceCollectorQuery.GetRoomByNumber(space.Number, rooms);
                var faceDataList = VerticalWallFaces.GetRoomExternalVerticalFaces(_roomDoc, selectedRoom);                
                foreach ( var faceData in faceDataList)
                {
                    try
                    {
                        var newWall = drawWallAndOpens.DrawWallBySpaceAndFace(space, faceData, northDirection);
                        Debug.Write($"стена  в пространстве {space.Number} создана");
                        wallList.Add(newWall);                    
                    }
                    catch (Exception ex)
                    {
                        Debug.Write($"ошибка при создании стены в пространстве {space.Number} {ex}");
                    }
                }
            }
            MessageBox.Show($"Создано {wallList.Count()} стен");
        }

        public void DrawFloors()
        {
            //Находим тип перекрытия
            FilteredElementCollector collector = new FilteredElementCollector(_hvacDocument);
            FloorType floorType = collector.OfClass(typeof(FloorType)).FirstOrDefault() as FloorType;
            var spaces = GetSpacesOnLowestLevel(_hvacDocument);
            foreach (var space in spaces.Cast<Space>())
            {
                var spaceBoundary = new SpaceBoundaryCurve(space as Space);
                var curves = spaceBoundary.GetCurves();
                var level = _hvacDocument.GetElement(space.LevelId) as Level;

                CurveLoop curveLoop = new CurveLoop();
                foreach (Curve curve in curves)
                {
                    curveLoop.Append(curve);
                }

                IList<CurveLoop> curveLoops = new List<CurveLoop>() { curveLoop };

                if (floorType == null)
                {
                    TaskDialog.Show("Error", "Не найден тип перекрытия");
                }

                using (Transaction trans = new Transaction(_hvacDocument, "Create Floor"))
                {
                    trans.Start();
                    try
                    {
                        Floor floor = Floor.Create(_hvacDocument, curveLoops, floorType.Id, level.Id);
                        ParametersUtilty.SetParameterByValue(floor, "Orientation", OrientationNames.Horizontal);
                        ParametersUtilty.SetParameterByValue(floor, "SpaceId", space.Id.ToString());
                        ParametersUtilty.SetParameterByValue(floor, "SpaceNumber", space.Number.ToString());
                        ParametersUtilty.SetParameterByValue(floor, "TransferCoefficient", 0);
                        ParametersUtilty.SetParameterByValue(floor, "ConstructionType", "no construction");
                        ParametersUtilty.SetParameterByValue(floor, "EnclosureType", EnclosureTypeOptions.Floor);
                        trans.Commit();
                    }
                    catch (Exception ex)
                    {
                        trans.RollBack();
                        TaskDialog.Show("Error", $"Ошибка при создании перекрытия: {ex.Message}");
                    }
                }
            }
        }
        public static List<SpatialElement> GetSpacesOnLowestLevel(Document doc)
        {
            if (doc == null)
            {
                TaskDialog.Show("Error", "Invalid document");
                return null;
            }
            // Получаем все уровни, отсортированные по elevation
            FilteredElementCollector levelCollector = new FilteredElementCollector(doc);
            List<Level> levels = levelCollector.OfClass(typeof(Level)).Cast<Level>().OrderBy(l => l.Elevation).ToList();

            if (levels.Count == 0)
            {
                TaskDialog.Show("Error", "No levels found in the document");
                return null;
            }
            // Берем самый первый уровень (с наименьшей высотой)
            Level lowestLevel = levels.First();

            // Получаем все пространства в документе
            FilteredElementCollector spaceCollector = new FilteredElementCollector(doc);
            List<SpatialElement> allSpaces = spaceCollector.OfClass(typeof(SpatialElement)).Cast<SpatialElement>().ToList();

            if (allSpaces.Count == 0)
            {
                TaskDialog.Show("Error", "No spaces found in the document");
                return null;
            }

            // Фильтруем пространства по уровню.
            List<SpatialElement> spacesOnLowestLevel = allSpaces
                .Where(space => space.LevelId == lowestLevel.Id)
                .ToList();

            return spacesOnLowestLevel;
        }
        private void DrawOpenings(List<Element> walls, List<Element> openings, FamilySymbol familySymbol, string openingType)
        {
            if (openings == null || openings.Count == 0 || familySymbol == null) return;

            var drawWallAndOpens = new DrawWallAndOpens(_hvacDocument);
            var count = 0;

            foreach (var element in walls)
            {
                var wall = (Wall)element;
                try
                {
                    var createdOpenings = drawWallAndOpens.DrawOpens(wall, openings, familySymbol, openingType);
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
        public void DrawWindows(List<Element> walls)
        {
           var windowsSymbols = CollectorQuery.GetAllWindowsFamilySymbols(_hvacDocument);
           var roomWidowsList = CollectorQuery.GetAllWindows(_roomDoc);
           var windowSymbol = windowsSymbols.FirstOrDefault() as FamilySymbol;
           DrawOpenings(walls,roomWidowsList, windowSymbol, EnclosureTypeOptions.Window);
        }
        public void DrawDoors(List<Element> walls)
        {
            var roomDoorsList = CollectorQuery.GetAllDoors(_roomDoc);
            var doorsSymbols = CollectorQuery.GetAllDoorsFamilySymbols(_hvacDocument);
            var doorSymbol = doorsSymbols.FirstOrDefault() as FamilySymbol;
            DrawOpenings(walls,roomDoorsList, doorSymbol, EnclosureTypeOptions.Door);
        }
    }
}
