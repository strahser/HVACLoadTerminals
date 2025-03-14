using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI;
using HVACLoadTerminals.DrawNewSpaceFaces;
using HVACLoadTerminals.DrawNewSpaceFaces.Walls;
using HVACLoadTerminals.ModelsStatic;
using HVACLoadTerminals.Utils;

namespace HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls
{
    public class DrawWalls(Document hvacDocument, Document roomDocument)
    {
        private List<Element> Spaces => CollectorQuery.GetAllSpaces(hvacDocument);
        private List<Element> Rooms => CollectorQuery.GetAllRooms(roomDocument);
        public void DrawWallsForSelectedSpaces(string northDirection)
        {
            var wallList = new List<Wall>();
            foreach (var space in Spaces.Cast<Space>())
            {
                var selectedRoom = RoomAndSpaceCollectorQuery.GetRoomByNumber(space.Number, Rooms);// TODO: не безопасный выбор
                var faceDataList = VerticalWallFaces.GetRoomExternalVerticalFaces(roomDocument, selectedRoom);                
                foreach ( var faceData in faceDataList)
                {
                    try
                    {
                        
                        var newWall = DrawWallBySpaceAndFace(space, faceData, northDirection);
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
        private Wall DrawWallBySpaceAndFace(Space space, ConstructionSurfaceModel faceModel, string northDirection)
        { // Проверка на null space, faceModel или face
         if (space == null || faceModel == null || faceModel._Face == null)
         {
             Debug.WriteLine($"Предупреждение: Пропущен вызов DrawWallBySpaceAndFace из-за null аргументов.");
             return null;
         }
         
         // Создаем транзакцию
         using var transaction = new Transaction(hvacDocument, $"Создать стену {space.Name}-{space.Number}");
         transaction.Start();
         // Получаем CurveLoops из Face
         var curveLoops = faceModel._Face.GetEdgesAsCurveLoops();
         if (curveLoops == null || curveLoops.Count == 0)
         {
             Debug.WriteLine($"Предупреждение: Не найдены кривые для грани пространства {space.Name}.");
             transaction.RollBack();
             return null; // Если нет CurveLoops, завершаем
         }
         // Создаем CurveArray для определения стены
         var curveArray = new CurveArray();
         foreach (var loop in curveLoops)
         {
             if (loop == null) continue;
             foreach (var curve in loop)
             {
                 if(curve != null) curveArray.Append(curve);
             }
         }
         if (curveArray.IsEmpty)
         {
             Debug.WriteLine($"Предупреждение: Не удалось сформировать CurveArray для стены в пространстве {space.Name}.");
             transaction.RollBack();
             return null;
         }
         var wallCurve = curveArray.get_Item(0);
         if (wallCurve == null)
         {
             Debug.WriteLine($"Предупреждение: Не удалось получить кривую для создания стены в пространстве {space.Name}.");
             transaction.RollBack();
             return null;
         }

         var wall = Wall.Create(hvacDocument, wallCurve, space.Level.Id, structural: false);
         SetWallParameters(space, faceModel, northDirection, wallCurve, wall);
         transaction.Commit();
         return wall;
        }
        private  void SetWallParameters(Space space, ConstructionSurfaceModel faceModel, string northDirection, Curve wallCurve, Wall wall)
        {
            // Устанавливаем параметры стены (если они есть)
            var orientationValue = GetOrientation(wallCurve, northDirection);
            var roomBoundingParam = wall.get_Parameter(BuiltInParameter.WALL_ATTR_ROOM_BOUNDING);
            var heightParameter = wall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM);
            var spaceHeight = space.get_Parameter(BuiltInParameter.ROOM_UPPER_OFFSET)?.AsDouble() ?? 0;
            var calculateArea = ParameterDisplayConvertor.SquareMeters(wall.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED).AsDouble());
            
            //устанавливаем параметры
            roomBoundingParam?.Set(0); // 0 - означает false
            heightParameter?.Set(spaceHeight); // берем и устанавливаем в футах
            ParametersUtility.SetParameterByValueAndName(wall, nameof(ConstructionSurfaceModel.Orientation), orientationValue);
            ParametersUtility.SetParameterByValueAndName(wall, nameof(ConstructionSurfaceModel.SpaceId), space.Id.ToString());
            ParametersUtility.SetParameterByValueAndName(wall, nameof(ConstructionSurfaceModel.SpaceNumber), space.Number.ToString());
            ParametersUtility.SetParameterByValueAndName(wall, nameof(ConstructionSurfaceModel.TransferCoefficient), faceModel.TransferCoefficient);
            ParametersUtility.SetParameterByValueAndName(wall, nameof(ConstructionSurfaceModel.ConstructionType), faceModel.ConstructionType);
            ParametersUtility.SetParameterByValueAndName(wall, nameof(ConstructionSurfaceModel.EnclosureType), faceModel.EnclosureType);
            ParametersUtility.SetParameterByValueAndName(wall, nameof(ConstructionSurfaceModel.ConstructionArea), calculateArea);
            ParametersUtility.SetParameterByValueAndName(wall, nameof(ConstructionSurfaceModel.TemperatureInSpace), ParametersHandler.GetSpaceSetHeatPoint(hvacDocument,space));
            ParametersUtility.SetParameterByValueAndName(wall, nameof(ConstructionSurfaceModel.TemperatureOut), ParametersHandler.GetProjectInformation(hvacDocument,nameof(ClimateData.TWinterOut092)));
        }
        private static string GetOrientation(Curve curve, string northDirection)
        {
            //northDirection up,down,left,right)
            var mapping = OrientationMapping.OrientationMappings.FirstOrDefault(m =>
                m.MainDirection.ToLower() == northDirection.ToLower());
            if (curve is Arc)
            {
                // Если кривая - дуга, преобразуем ее в линию
                var startPoint = curve.GetEndPoint(0);
                var endPoint = curve.GetEndPoint(1);
                curve = Line.CreateBound(startPoint, endPoint);
            }

            // Получение вектора направления кривой
            var curveDirection = curve.GetEndPoint(1) - curve.GetEndPoint(0);
            curveDirection.Normalize(); // Нормализация вектора
            return CurveNormalizeMappingOrientation(curveDirection, mapping);
        }
        private static string CurveNormalizeMappingOrientation(XYZ curveDirection, OrientationMapping mapping)
        {
            // Определение ориентации
            if (Math.Abs(curveDirection.Y) > 0.9) // Вертикальное направление (С/Ю)
            {
                return curveDirection.Y > 0 ? mapping.N : mapping.S;
            }
            else if (Math.Abs(curveDirection.X) > 0.9) // Горизонтальное направление (В/З)
            {
                return curveDirection.X > 0 ? mapping.E : mapping.W;
            }
            else
            {
                // Промежуточные направления
                if (curveDirection.X > 0 && curveDirection.Y > 0)
                {
                    return mapping.NE;  // Северо-восток
                }
                else if (curveDirection.X < 0 && curveDirection.Y > 0)
                {
                    return mapping.NW; // Северо-запад
                }
                else if (curveDirection.X > 0 && curveDirection.Y < 0)
                {
                    return mapping.SE; // Юго-восток
                }
                else if (curveDirection.X < 0 && curveDirection.Y < 0)
                {
                    return mapping.SW; // Юго-запад
                }
                else
                {
                    return "Не определено"; // Ориентация не определена
                }
            }
        }
    }
}