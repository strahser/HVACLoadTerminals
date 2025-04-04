using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using HVACLoadTerminals.ModelsStatic;
using HVACLoadTerminals.Utils;

namespace HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.DirectShape
{

    public static class CreateDirectShapesForEachElement 
    {

    public static void ConvertArchToThermalModel(Document doc)
    {
        // Получаем все элементы модели.
        var listWalls = CollectorQuery.GetAllWalls(doc);
        var listWindows = CollectorQuery.GetAllWindows(doc);
        var listDoors = CollectorQuery.GetAllDoors(doc);
        var listFloors = CollectorQuery.GetAllFloors(doc);

        // Общий список всех элементов
        var structureElementsList = listWalls.Concat(listWindows).Concat(listDoors).Concat(listFloors).ToList();

        int createdElementCount = 0; // Счетчик созданных элементов

        using (Transaction tx = new Transaction(doc, "Create Thermal Model"))
        {
            tx.Start();

            // Конвертация стен с автоматическим учетом проемов
            foreach (var wall in structureElementsList)
            {
                if (CreateThermalElementFromExisting(wall, "Тепловая Модель", doc))
                {
                    createdElementCount++; // Увеличиваем счетчик, если элемент был успешно создан
                }

            }

            // удаляем элементы модели
            foreach (var element in listWalls.Concat(listFloors)) // Объединяем для более эффективного удаления
            {
                try
                {
                    doc.Delete(element.Id);
                }
                catch (Exception e)
                {
                    Debug.Write("Error", $"Failed to delete element with ID: {element.Id.IntegerValue}. Error: {e.Message}");
                }
            }
            tx.Commit();

            // Показываем сообщение с количеством созданных элементов
            TaskDialog.Show("Создана энергетическая модель здания", $" Создана энергетическая модель здания. Создано {createdElementCount} элементов поверхности.");
        }
    }
 
 

    private static bool CreateThermalElementFromExisting(Element elem, string familyName, Document doc)
    {
        try
        {
            Options geomOptions = new Options
            {
                ComputeReferences = true,
                DetailLevel = ViewDetailLevel.Fine
            };

            GeometryElement geomElem = elem.get_Geometry(geomOptions);

            List<GeometryObject> validGeometries = new List<GeometryObject>();

            foreach (GeometryObject geomObj in geomElem)
            {
                if (geomObj is Solid solid)
                {
                    if (solid.Volume > 0)
                    {
                        validGeometries.Add(solid);
                    }
                }
                else if (geomObj is GeometryInstance geomInst)
                {
                    foreach (GeometryObject instObj in geomInst.GetInstanceGeometry())
                    {
                        if (instObj is Solid instSolid && instSolid.Volume > 0)
                        {
                            validGeometries.Add(instSolid);
                        }
                    }
                }
            }

            if (validGeometries.Count > 0)
            {
                Autodesk.Revit.DB.DirectShape ds = Autodesk.Revit.DB.DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
                ds.SetShape(validGeometries);
                //устанавливаем параметры
                TransferParameters(elem, ds);
                var enclosureType = elem.LookupParameter(nameof(ConstructionSurfaceModel.EnclosureType)).AsValueString();
                var calculateArea = CalculateAreaFactory(elem,enclosureType );
                ParametersUtility.SetParameterByValueAndName(ds, nameof(ConstructionSurfaceModel.ConstructionArea), ParameterDisplayConvertor.SquareMeters(calculateArea));
                OverrideGraphicDirectShape(doc, ds, enclosureType);
                return true; // Элемент успешно создан
            }
            else
            {
                Debug.WriteLine($"No valid geometry found for element with ID: {elem.Id.IntegerValue}");
                return false; // Нет геометрии - элемент не создан
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error creating thermal element from element {elem.Id.IntegerValue}: {ex.Message}");
            TaskDialog.Show("Error", $"Error creating thermal element from element {elem.Id.IntegerValue}: {ex.Message}");
            return false; // Произошла ошибка - элемент не создан
        }
    }

    static void OverrideGraphicDirectShape(Document doc, Autodesk.Revit.DB.DirectShape ds, string enclosureType)
    {
        Color enclosureColor = GetEnclosureColor(enclosureType);
        OverrideGraphicSettings settings = new OverrideGraphicSettings();

        // Получаем сплошной паттерн
        FillPatternElement solidPattern = new FilteredElementCollector(doc)
            .OfClass(typeof(FillPatternElement))
            .Cast<FillPatternElement>()
            .FirstOrDefault(f => f.GetFillPattern().IsSolidFill);

        if (solidPattern == null) return;

        // Базовые настройки для всех элементов
        settings.SetSurfaceForegroundPatternId(solidPattern.Id);
        settings.SetSurfaceForegroundPatternColor(enclosureColor);
        settings.SetProjectionLineColor(enclosureColor);

        // Индивидуальные настройки
        switch (enclosureType)
        {
            case var _ when enclosureType == EnclosureTypeOptions.Window:
                settings.SetSurfaceTransparency(0); // Полная непрозрачность
                settings.SetProjectionLineWeight(4);
                break;

            case var _ when enclosureType == EnclosureTypeOptions.Curtain:
                settings.SetSurfaceTransparency(40); // Частичная прозрачность
                break;

            default:
                settings.SetSurfaceTransparency(0); // По умолчанию непрозрачные
                break;
        }
        // Применяем настройки
        doc.ActiveView.SetElementOverrides(ds.Id, settings);

    }

    private static void Create3DView(Document doc)
    {
        View newView = View3D.CreateIsometric(doc, new FilteredElementCollector(doc)
            .OfClass(typeof(ViewFamilyType))
            .Cast<ViewFamilyType>()
            .First(x => x.ViewFamily == ViewFamily.ThreeDimensional).Id);
        using Transaction t = new Transaction(doc, "Test View");
        t.Start();
        newView.DisplayStyle = DisplayStyle.ShadingWithEdges;
        t.Commit();
    }
    static Color GetEnclosureColor(string enclosureType)
    {
        var colorMap = new Dictionary<string, Color>
        {
            { EnclosureTypeOptions.Wall, new Color(255, 0, 0) },     // Красный
            { EnclosureTypeOptions.Roof, new Color(0, 0, 255) },     // Синий
            { EnclosureTypeOptions.Floor, new Color(0, 255, 0) },     // Зеленый
            { EnclosureTypeOptions.Window, new Color(30, 30, 150) },  // Темно-синий
            { EnclosureTypeOptions.Skylight, new Color(80, 0, 80) },  // Темно-пурпурный
            { EnclosureTypeOptions.Curtain, new Color(0, 150, 150) },// Темно-голубой
            { EnclosureTypeOptions.Door, new Color(80, 0, 40) }      // Темно-бордовый
        };
        return colorMap.TryGetValue(enclosureType, out Color color) ? color : new Color(60, 60, 60);
    }

        //расчет площади в зависимости от типа конструкции.
        private static double CalculateAreaFactory(Element element, string enclosureType)
        {
            //для стен и витражей   
            if (enclosureType == EnclosureTypeOptions.Wall || enclosureType == EnclosureTypeOptions.Curtain)
            {
                return element.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED).AsDouble();
            }

            //для пола и стен   
            if (enclosureType == EnclosureTypeOptions.Floor || enclosureType == EnclosureTypeOptions.Roof)
            {
                return element.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED).AsDouble();
            }

            if (enclosureType == EnclosureTypeOptions.Window || enclosureType == EnclosureTypeOptions.Door)
            {
                var height = element.get_Parameter(BuiltInParameter.CASEWORK_HEIGHT).AsDouble();
                var width = element.get_Parameter(BuiltInParameter.GENERIC_WIDTH).AsDouble();
                if (height > 0 && width > 0)
                {
                    return height * width;
                }

                else
                {
                    var (bbHeight, bbWidth) = GetWindowDimensionsFromBoundingBox(element);
                    Debug.Write($" Не определено значение высоты  и ширины через параметры, принято {element.Name}" +
                                $"-bbHeight{bbHeight * 0.304}-bbWidth{bbWidth * 0.304}");
                    var area = bbHeight * bbWidth;
                    return area;
                }
            }
            else return 0;
        }

        private static (double height, double width) GetWindowDimensionsFromBoundingBox(Element window)
        {
            var bb = window.get_BoundingBox(RevitConfig.Document.ActiveView);
            if (bb == null)
            {
                return (0, 0);
            }

            var min = bb.Min;
            var max = bb.Max;

            var height = Math.Abs(max.Z - min.Z);

            // Проектируем BoundingBox на плоскость XY
            var corners = new XYZ[]
            {
                new XYZ(min.X, min.Y, 0), // Минимальная точка на XY
                new XYZ(max.X, min.Y, 0), // Максимальная X, минимальная Y
                new XYZ(max.X, max.Y, 0), // Максимальная X, максимальная Y
                new XYZ(min.X, max.Y, 0) // Минимальная X, максимальная Y
            };

            // Получаем максимальную ширину через проекции на плоскость XY
            double width = 0;
            for (var i = 0; i < corners.Length; i++)
            {
                for (var j = i + 1; j < corners.Length; j++)
                {
                    width = Math.Max(width, corners[i].DistanceTo(corners[j]));
                }
            }

            return (height, width);
        }

        private static void TransferParameters(Element elem, Element ds)
        {
            // Получаем список всех параметров из класса Surfaces
            List<string> transferParameters = ConstructionSurfaceModel.GetAllSurfaceParameters();

            foreach (string parameterName in transferParameters)
            {
                try
                {
                    Parameter sourceParameter = elem.LookupParameter(parameterName);

                    if (sourceParameter != null)
                    {
                        // Читаем значение параметра в зависимости от типа
                        string parameterValue = ParametersUtility.GetParameterValueAsString(sourceParameter);


                        // Передаем значение параметра
                        if (!string.IsNullOrEmpty(parameterValue)) //проверка, что значение параметра не пустое,
                        {
                            ParametersUtility.SetParameterByValueAndName(ds, parameterName, parameterValue);
                        }
                        else
                        {
                            // Если значение параметра пустое, выводим сообщение об этом,  или можно пропустить итерацию.
                            Debug.Write("Warning",$"Parameter '{parameterName}' is empty on element {elem.Id.IntegerValue}");
                        }
                    }
                    else
                    {
                        // Если параметр не найден, выводим сообщение, но продолжаем перебор.
                        Debug.Write("Warning",
                            $"Parameter '{parameterName}' not found on element {elem.Id.IntegerValue}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.Write("Error", $"Error transferring parameter '{parameterName}': {ex.Message}");
                }
            }
        }
    }
}