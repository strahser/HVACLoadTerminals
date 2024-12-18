using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Mechanical;
using HVACLoadTerminals.Models;
using HVACLoadTerminals.StaticData;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;


namespace HVACLoadTerminals.Utils
{


    public class RoomBounding
    {
        private Document RoomDoc;
        private Document HvacDocument;
        //конструктор
        public RoomBounding(Document doc)
        {
            HvacDocument = doc;
            CreateLinkDoc(doc);
        }
        private void CreateLinkDoc(Document doc)
        {
            // Получить список связанных документов с помощью FilteredElementCollector
            IList<RevitLinkInstance> links = CollectorQuery.GetLinkedDocument(doc);
            if (links.Count > 0)
            {
                // Получить связанный документ
                Document linkedDocument = links[0].GetLinkDocument();
                // Получить имя связанного документа
                string linkedDocumentName = linkedDocument.Title;
                RoomDoc = linkedDocument;
                // Вывести имя в консоль
                Debug.WriteLine("Имя связанного документа: " + linkedDocumentName);
            }
            else
            {
                Debug.WriteLine("Связанные документы не найдены.");
                RoomDoc = doc;
            }
        }
        public void DrawWalls(string northDirection)
        {
            DrawWallAndOpens drawWallAndOpens = new DrawWallAndOpens(HvacDocument);
            List<Wall> wallList = new List<Wall>();
            List<Element> _spaces = CollectorQuery.GetAllSpaces(HvacDocument);
            List<Element> _rooms = CollectorQuery.GetAllRooms(RoomDoc);
            foreach (Space space in _spaces.Cast<Space>())
            {
                Room selectedRoom = RoomAndSpaceCollectorQuery.GetRoomByNumber(space.Number, _rooms);
                List<ConstructionSurfaceData> FaceDataList = VerticalWallFaces.GetRoomExternalVerticalFaces(RoomDoc, selectedRoom);                
                foreach ( ConstructionSurfaceData faceData in FaceDataList)
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
        public void DrawOpenings(List<Element> Walls, List<Element> openings, FamilySymbol familySymbol, string openingType)
        {
            if (openings == null || openings.Count == 0 || familySymbol == null) return;

            DrawWallAndOpens drawWallAndOpens = new DrawWallAndOpens(HvacDocument);
            int count = 0;

            foreach (Wall wall in Walls)
            {
                try
                {
                    List<FamilyInstance> createdOpenings = drawWallAndOpens.DrawOpens(wall, openings, familySymbol, openingType);
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
        public void DrawWindows(List<Element> Walls)
        {
           List<Element> WindowsSymbols = CollectorQuery.GetAllWindowsFamilySymbols(HvacDocument);
           List<Element> _roomWidowsList = CollectorQuery.GetAllWindows(RoomDoc);
           FamilySymbol windowSymbol = WindowsSymbols.FirstOrDefault() as FamilySymbol;
           DrawOpenings(Walls,_roomWidowsList, windowSymbol, RoomBoundingOptions.Window);
        }
        public void DrawDoors(List<Element> Walls)
        {
            List<Element> _roomDoorsList = CollectorQuery.GetAllDoors(RoomDoc);
            List<Element> DoorsSymbols = CollectorQuery.GetAllDoorsFamilySymbols(HvacDocument);
            FamilySymbol doorSymbol = DoorsSymbols.FirstOrDefault() as FamilySymbol;
            DrawOpenings(Walls,_roomDoorsList, doorSymbol, RoomBoundingOptions.Door);
        }
    }
}
