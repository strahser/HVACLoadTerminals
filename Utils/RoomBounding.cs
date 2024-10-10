using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB.Architecture;

namespace HVACLoadTerminals.Utils
{
    using Autodesk.Revit.DB;
    using Autodesk.Revit.DB.Architecture;
    using HVACLoadTerminals.Models;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Windows;

    public class RoomBounding
    {
        private Document _doc;
        private List<Element> _rooms;

        private SpatialElementBoundaryOptions _boundaryOptions;
        private SpatialElementGeometryCalculator _calculator;

        public List<FaceData> _walls;
        public List<FaceData> _curtains;

        public RoomBounding(List<Element> rooms, Document doc)
        {
            _doc = doc;
            _rooms = rooms;
            _walls = new List<FaceData>();
            _curtains = new List<FaceData>();
            _boundaryOptions = new SpatialElementBoundaryOptions();
            _calculator = new SpatialElementGeometryCalculator(_doc);
        }

        public List<FaceData> Walls => _walls;
        public List<FaceData> Curtains => _curtains;

        public void GetRoomFaces()
        {
            foreach (Room room in _rooms)
            {
                if (room.Area > 0)
                {
                var geometryResults = _calculator.CalculateSpatialElementGeometry(room);
                var roomSolid = geometryResults.GetGeometry();
                var roomFaces = roomSolid.Faces;

                    foreach (Face face in roomFaces)
                    {
                        var faceInfo = geometryResults.GetBoundaryFaceInfo(face);
                        foreach (var bface in faceInfo)
                        {
                            if (bface.SubfaceType == SubfaceType.Side)
                            {
                                var sbeId = bface.SpatialBoundaryElement;
                                var hostId = sbeId.HostElementId.IntegerValue;
                                var linkId = sbeId.LinkedElementId;
                                var linkInstanceId = sbeId.LinkInstanceId;

                                var verticalFace = _doc.GetElement(bface.SpatialBoundaryElement.HostElementId) as Wall;
                                if (verticalFace != null)
                                {
                                    if (verticalFace.WallType.get_Parameter(BuiltInParameter.FUNCTION_PARAM).AsInteger() == 1)

                                    {
                                        string orientation = "";
                                        if (face is PlanarFace planarFace)
                                        {
                                            // Получаем нормаль к грани
                                            XYZ faceNormal = planarFace.FaceNormal;

                                            // Определяем ориентацию грани
                                             orientation = GetOrientation(faceNormal);
                                        }

                                        if (verticalFace.WallType.Kind == WallKind.Curtain)
                                        {
                                            _curtains.Add(new FaceData
                                            {
                                                Room = room,
                                                Face = face,
                                            });
                                        }
                                        else
                                        {
                                            Debug.Write(
                                                $"--room Number {room.Number}" +
                                                $"--face Area {ParameterDisplayConvertor.SquareMeters(face.Area)}"+
                                                $"--wall Name {verticalFace.WallType.Name}"+
                                                $"--wall Name {orientation}"
                                                );
                                            _walls.Add(new FaceData
                                            {
                                                Room = room,
                                                Face = face,

                                            });
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        // Метод для определения ориентации грани
        private string GetOrientation(XYZ faceNormal)
        {
            // Используем проекцию нормали на плоскость XY для определения ориентации
            XYZ projectedNormal = new XYZ(faceNormal.X, faceNormal.Y, 0);
            projectedNormal.Normalize(); // Нормализуем вектор для сравнения

            // Сравниваем нормаль с основными направлениями
            if (projectedNormal.IsAlmostEqualTo(XYZ.BasisY))// Верх
            {
                return "С"; 
            }
            else if (projectedNormal.IsAlmostEqualTo(XYZ.BasisY.Negate()))// Низ
            {
                return "Ю"; 
            }
            else if (projectedNormal.IsAlmostEqualTo(XYZ.BasisX))// Право
            {
                return "В"; 
            }
            else if (projectedNormal.IsAlmostEqualTo(XYZ.BasisX.Negate()))// Лево
            {
                return "З"; 
            }
            else
            {
                return "Не определено"; // Ориентация не определена
            }
        }

    }
}
