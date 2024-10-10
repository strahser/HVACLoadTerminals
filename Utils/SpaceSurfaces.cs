using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB.Analysis;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Mechanical;
using System.Linq;
using HVACLoadTerminals.Models;

namespace HVACLoadTerminals.Utils
{
    public class SpaceSurfaces
    {
        private Document _doc;

        private SpatialElementBoundaryOptions _boundaryOptions;
        private SpatialElementGeometryCalculator _calculator;

        private List<FaceData> _faceData;

        public SpaceSurfaces(Document doc)
        {
            _doc = doc;

            _faceData = new List<FaceData>();

            _boundaryOptions = new SpatialElementBoundaryOptions();
            _calculator = new SpatialElementGeometryCalculator(_doc);

            GetSpaceFaces();
        }

        public List<FaceData> FaceData => _faceData;

        private void GetSpaceFaces()
        {
            // Получаем аналитические пространства
            List<Space> spaces = new FilteredElementCollector(_doc)
                .OfCategory(BuiltInCategory.OST_AnalyticSpaces)
                .WhereElementIsNotElementType()
                .ToElements()
                .Cast<Space>()
                .ToList();

            // Создаем аналитическую модель
            EnergyAnalysisDetailModelOptions options = new EnergyAnalysisDetailModelOptions();
            options.Tier = EnergyAnalysisDetailModelTier.Final;
            EnergyAnalysisDetailModel eadm = null;

            using (Transaction transaction = new Transaction(_doc, "Create Analytic Model"))
            {
                transaction.Start();
                eadm = EnergyAnalysisDetailModel.Create(_doc, options);
                transaction.Commit();
            }

            // Проходим по аналитическим пространствам
            foreach (Space space in spaces)
            {
                // Получаем поверхности, связанные с пространством
                List<ElementId> spaceSurfaceIds = GetAnalyticalSurfaceIdsForSpace(space, eadm);

                // Проходим по поверхностям
                foreach (ElementId surfaceId in spaceSurfaceIds)
                {
                    Element surfaceElement = _doc.GetElement(surfaceId);

                    // Проверяем, является ли элемент AnalyticalSurface
                    if (surfaceElement is AnalyticalSurface surface)
                    {
                        // Получаем геометрию поверхности
                        GeometryObject geometryObject = surface.GetGeometryObjectFromReference(surface.GetAnalyticalSurfaceReference());

                        // Получаем грани поверхности
                        if (geometryObject is Solid solid)
                        {
                            foreach (Face face in solid.Faces)
                            {
                                if (face is PlanarFace planarFace)
                                {
                                    // Получаем нормаль к грани
                                    XYZ faceNormal = planarFace.FaceNormal;

                                    // Определяем ориентацию грани
                                    string orientation = GetOrientation(faceNormal);

                                    // Проверяем, является ли грань наружной
                                    if (IsOuterFace(planarFace, space, faceNormal))
                                    {
                                        // Получаем HostElementId из SpatialBoundaryElement
                                        ElementId hostId = GetHostElementId(planarFace, space);

                                        Element hostElement = _doc.GetElement(hostId);

                                        // Определяем тип ограждения
                                        string enclosureType = GetEnclosureType(hostElement);

                                        // Определяем тип конструкции
                                        string constructionType = GetConstructionType(hostElement);

                                        // Вычисляем площадь конструкции
                                        double area = planarFace.Area;

                                        _faceData.Add(new FaceData
                                        {
                                            Space = space,
                                            Face = face,
                                            EnclosureType = enclosureType,
                                            Orientation = orientation,
                                            ConstructionType = constructionType,
                                            Area = area
                                        });
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        // Метод для получения HostElementId из SpatialBoundaryElement
        private ElementId GetHostElementId(PlanarFace planarFace, Space space)
        {
            // Используем SpatialElementGeometryCalculator для получения SpatialBoundarySegmentInfo
            var geometryResults = _calculator.CalculateSpatialElementGeometry(space);
            var roomSolid = geometryResults.GetGeometry();
            var roomFaces = roomSolid.Faces;

            // Находим SpatialBoundarySegmentInfo для данной грани
            foreach (Face face in roomFaces)
            {
                if (face == planarFace)
                {
                    // Используем _calculator.GetBoundarySegmentInfo
                    var faceInfo = _calculator.GetBoundarySegmentInfo(face);

                    // Проверяем, есть ли SpatialBoundaryElement 
                    foreach (SpatialBoundarySegmentInfo segmentInfo in faceInfo)
                    {
                        if (segmentInfo.SpatialBoundaryElement != null)
                        {
                            return segmentInfo.SpatialBoundaryElement.HostElementId;
                        }
                    }
                }
            }

            // Если SpatialBoundaryElement не найден, возвращаем неверный ID
            return ElementId.InvalidElementId;
        }

        // Метод для получения ID AnalyticalSurface для пространства
        private List<ElementId> GetAnalyticalSurfaceIdsForSpace(Space space, EnergyAnalysisDetailModel eadm)
        {
            List<ElementId> surfaceIds = new List<ElementId>();

            // Получаем все поверхности в аналитической модели
            foreach (ElementId surfaceId in eadm.GetSurfaceIds())
            {
                Element surfaceElement = _doc.GetElement(surfaceId);

                if (surfaceElement is AnalyticalSurface && surfaceElement.SpaceId == space.Id)
                {
                    surfaceIds.Add(surfaceId);
                }
            }

            return surfaceIds;
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

        // Метод для определения, является ли грань наружной
        private bool IsOuterFace(PlanarFace face, Space space, XYZ faceNormal)
        {
            // Получаем точки на грани
            var points = GetFacePoints(face);

            // Проверяем, находится ли хотя бы одна точка вне комнаты
            foreach (XYZ point in points)
            {
                // Используем метод IsPointInRoom для определения, 
                // находится ли точка внутри пространства
                if (!space.IsPointInRoom(point))
                {
                    return true; // Грань наружная
                }
            }

            // Если все точки внутри комнаты, значит грань внутренняя
            return false;
        }

        // Метод для получения точек на грани
        private List<XYZ> GetFacePoints(Face face)
        {
            var points = new List<XYZ>();
            var curveLoops = face.GetEdgesAsCurveLoops();
            foreach (CurveLoop curveLoop in curveLoops)
            {
                foreach (Curve curve in curveLoop.GetCurves())
                {
                    points.Add(curve.GetEndPoint(0));
                    points.Add(curve.GetEndPoint(1));
                }
            }
            return points;
        }

        // Метод для определения типа ограждения
        private string GetEnclosureType(Element element)
        {
            if (element is Wall wall)
            {
                if (wall.WallType.Kind == WallKind.Curtain)
                {
                    return "CurtainWall";
                }
                else
                {
                    return "Wall";
                }
            }
            else if (element is RoofBase roof)
            {
                return "Roof";
            }
            else if (element is Floor floor)
            {
                return "Floor";
            }
            else
            {
                return "Unknown";
            }
        }

        // Метод для определения типа конструкции
        private string GetConstructionType(Element element)
        {
            if (element is Wall wall)
            {
                return wall.WallType.Name;
            }
            else if (element is RoofBase roof)
            {
                return roof.RoofType.Name;
            }
            else if (element is Floor floor)
            {
                return floor.FloorType.Name;
            }
            else
            {
                return "Unknown";
            }
        }
    }
}
