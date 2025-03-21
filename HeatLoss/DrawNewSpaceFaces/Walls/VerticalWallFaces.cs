using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using HVACLoadTerminals.HeatLoss;
using HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces;
using HVACLoadTerminals.ModelsStatic;
using HVACLoadTerminals.Utils;

namespace HVACLoadTerminals.DrawNewSpaceFaces.Walls
{
    public static class VerticalWallFaces
    {
            /// <summary>
            /// Определяем наружные грани каждого помещения 
            /// </summary>
            /// <param name="doc"></param>
            /// <param name="room"></param>
            /// <returns></returns>
        public static List<ConstructionSurfaceModel> GetRoomExternalVerticalFaces(Document doc, Room room)
        {
            var faceDataList = new List<ConstructionSurfaceModel>(); 
            var boundaryOptions = new SpatialElementBoundaryOptions();
            var calculator = new SpatialElementGeometryCalculator(doc);
            if (room == null || !(room.Area > 0)) return faceDataList;
            var geometryResults = calculator.CalculateSpatialElementGeometry(room);
            var roomSolid = geometryResults.GetGeometry();
            var roomFaces = roomSolid.Faces;
            foreach (Face face in roomFaces)
            {
                var boundaryFaceInfo = geometryResults.GetBoundaryFaceInfo(face);
                foreach (var boundarySubFace in boundaryFaceInfo)
                {
                    if (boundarySubFace.SubfaceType == SubfaceType.Side)
                    {
                        //ненужные сейчас данные, но могут потребоваться при работе с ссылками
                        var sbeId = boundarySubFace.SpatialBoundaryElement;
                        long hostId = sbeId.HostElementId.IntegerValue;
                        var linkId = sbeId.LinkedElementId;
                        var linkInstanceId = sbeId.LinkInstanceId;
                        //преобразуем часть грани в стену
                        var verticalFace = doc.GetElement(boundarySubFace.SpatialBoundaryElement.HostElementId) as Wall;
                        //получаем наружные стены
                        if (verticalFace != null && verticalFace.WallType.get_Parameter(BuiltInParameter.FUNCTION_PARAM).AsInteger() == 1)
                        {
                            // Создаем ConstructionSurfaceModel для стены
                            var wallFaceData = new ConstructionSurfaceModel
                            {
                                _Face = face,
                                FaceId = verticalFace.Id.ToString(),
                                _Room = room,
                                SpaceNumber = room.Number,
                                RevitElementId = verticalFace.WallType.Id.ToString(),
                                FullWallArea = ParameterDisplayConvertor.SquareMeters(face.Area),
                                ConstructionName = verticalFace.WallType.Name,
                                EnclosureType = verticalFace.WallType.Kind == WallKind.Curtain ? EnclosureTypeOptions.Curtain  : EnclosureTypeOptions.Wall,
                                Orientation = OrientationNames.GetSideFromOrientationAzimuth(verticalFace.Orientation),
                                TransferCoefficient = CheckTransferCoefficient(verticalFace),
                            };
                            faceDataList.Add(wallFaceData); 
                        }                            
                    }
                }
            }
            return faceDataList;
        }

        private static double CheckTransferCoefficient(Wall verticalFace)
        {
            var transferCoefficientParam = verticalFace.WallType.get_Parameter(BuiltInParameter.ANALYTICAL_HEAT_TRANSFER_COEFFICIENT);
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
            return uValue;

        }
    }
}
