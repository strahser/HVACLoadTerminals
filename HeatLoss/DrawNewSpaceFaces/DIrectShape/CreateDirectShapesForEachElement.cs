using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using HVACLoadTerminals.ModelsStatic;
using HVACLoadTerminals.Utils;

namespace HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.DirectShape;

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

        var createdElementCount = 0; // Счетчик созданных элементов

        using var tx = new Transaction(doc, "Create Thermal Model");
        tx.Start();

        // Конвертация стен с автоматическим учетом проемов
        foreach (var element in structureElementsList)
            if (CreateThermalElementFromExisting(element, "Тепловая Модель", doc))
                createdElementCount++; // Увеличиваем счетчик, если элемент был успешно создан

        // удаляем элементы модели
        foreach (var element in listWalls.Concat(listFloors)) // Объединяем для более эффективного удаления
            try
            {
                doc.Delete(element.Id);
            }
            catch (Exception e)
            {
                Debug.Write("Error",
                    $"Failed to delete element with ID: {element.Id.IntegerValue}. Error: {e.Message}");
            }

        tx.Commit();

        // Показываем сообщение с количеством созданных элементов
        TaskDialog.Show("Создана энергетическая модель здания",
            $" Создана энергетическая модель здания. Создано {createdElementCount} элементов поверхности.");
    }

    private static bool CreateThermalElementFromExisting(Element elem, string familyName, Document doc)
    {
        try
        {
            var geomOptions = new Options
            {
                ComputeReferences = true,
                DetailLevel = ViewDetailLevel.Fine
            };

            var geomElem = elem.get_Geometry(geomOptions);

            var validGeometries = new List<GeometryObject>();

            foreach (var geomObj in geomElem)
                if (geomObj is Solid solid)
                {
                    if (solid.Volume > 0) validGeometries.Add(solid);
                }
                else if (geomObj is GeometryInstance geomInst)
                {
                    foreach (var instObj in geomInst.GetInstanceGeometry())
                        if (instObj is Solid instSolid && instSolid.Volume > 0)
                            validGeometries.Add(instSolid);
                }

            if (validGeometries.Count > 0)
            {
                var ds = Autodesk.Revit.DB.DirectShape.CreateElement(doc,
                    new ElementId(BuiltInCategory.OST_GenericModel));
                ds.SetShape(validGeometries);
                //устанавливаем параметры
                TransferParameters(elem, ds);
                var enclosureType = AddAreaValueToElement(elem, ds);
                GraphicDirectShapeHandler.OverrideGraphicDirectShape(doc, ds, enclosureType);
                return true; // Элемент успешно создан
            }

            Debug.WriteLine($"No valid geometry found for element with ID: {elem.Id.IntegerValue}");
            return false; // Нет геометрии - элемент не создан
        }
        catch (Exception ex)
        {
            HandleError(elem, ex);
            return false; // Произошла ошибка - элемент не создан
        }
    }


    private static string AddAreaValueToElement(Element elem, Autodesk.Revit.DB.DirectShape ds)
    {
        var enclosureType = elem.LookupParameter(nameof(ConstructionSurfaceModel.EnclosureType)).AsValueString();
        var calculateArea = CalculateAreaFactory(elem, enclosureType);

        ParametersUtility.SetParameterByValueAndName(ds, nameof(ConstructionSurfaceModel.ConstructionArea),
            ParameterDisplayConvertor.SquareMeters(calculateArea));
        return enclosureType;
    }

    private static (double height, double width) GetWindowDimensionsFromBoundingBox(Element window)
    {
        var bb = window.get_BoundingBox(RevitConfig.Document.ActiveView);
        if (bb == null) return (0, 0);

        var min = bb.Min;
        var max = bb.Max;

        var height = Math.Abs(max.Z - min.Z);

        // Проектируем BoundingBox на плоскость XY
        var corners = new[]
        {
            new XYZ(min.X, min.Y, 0), // Минимальная точка на XY
            new XYZ(max.X, min.Y, 0), // Максимальная X, минимальная Y
            new XYZ(max.X, max.Y, 0), // Максимальная X, максимальная Y
            new XYZ(min.X, max.Y, 0) // Минимальная X, максимальная Y
        };

        // Получаем максимальную ширину через проекции на плоскость XY
        double width = 0;
        for (var i = 0; i < corners.Length; i++)
        for (var j = i + 1; j < corners.Length; j++)
            width = Math.Max(width, corners[i].DistanceTo(corners[j]));

        return (height, width);
    }

    private static void TransferParameters(Element elem, Element ds)
    {
        // Получаем список всех параметров из класса Surfaces
        var transferParameters = ConstructionSurfaceModel.GetAllSurfaceParameters();

        foreach (var parameterName in transferParameters)
            try
            {
                var sourceParameter = elem.LookupParameter(parameterName);

                if (sourceParameter != null)
                {
                    // Получаем тип свойства через рефлексию
                    var propertyInfo = typeof(ConstructionSurfaceModel).GetProperty(parameterName);
                    if (propertyInfo != null)
                    {
                        var targetType = propertyInfo.PropertyType;

                        // Получаем значение параметра с учетом типа свойства
                        var parameterValue =
                            ParametersUtility.GetParamValueFromPropertyType(sourceParameter, targetType);

                        // Передаем значение параметра
                        if (parameterValue != null && !string.IsNullOrEmpty(parameterValue.ToString()))
                            ParametersUtility.SetParameterByValueAndName(ds, parameterName, parameterValue);
                        else
                            // Если значение параметра пустое, выводим сообщение об этом
                            Debug.Write("Warning",
                                $"Parameter '{parameterName}' is empty on element {elem.Id.IntegerValue}");
                    }
                    else
                    {
                        Debug.Write("Warning", $"Property '{parameterName}' not found in ConstructionSurfaceModel");
                    }
                }
                else
                {
                    // Если параметр не найден, выводим сообщение, но продолжаем перебор.
                    Debug.Write("Warning", $"Parameter '{parameterName}' not found on element {elem.Id.IntegerValue}");
                }
            }
            catch (Exception ex)
            {
                Debug.Write("Error", $"Error transferring parameter '{parameterName}': {ex.Message}");
            }
    }

    //расчет площади в зависимости от типа конструкции.
    private static double CalculateAreaFactory(Element element, string enclosureType)
    {
        //для стен и витражей   
        if (enclosureType == EnclosureTypeOptions.Wall || enclosureType == EnclosureTypeOptions.Curtain)
            return element.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED).AsDouble();

        //для пола и стен   
        if (enclosureType == EnclosureTypeOptions.Floor || enclosureType == EnclosureTypeOptions.Roof)
            return element.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED).AsDouble();

        if (enclosureType == EnclosureTypeOptions.Window || enclosureType == EnclosureTypeOptions.Door)
        {
            var height = element.get_Parameter(BuiltInParameter.CASEWORK_HEIGHT).AsDouble();
            var width = element.get_Parameter(BuiltInParameter.GENERIC_WIDTH).AsDouble();
            if (height > 0 && width > 0) return height * width;

            var (bbHeight, bbWidth) = GetWindowDimensionsFromBoundingBox(element);
            Debug.Write($" Не определено значение высоты  и ширины через параметры, принято {element.Name}" +
                        $"-bbHeight{bbHeight * 0.304}-bbWidth{bbWidth * 0.304}");
            var area = bbHeight * bbWidth;
            return area;
        }

        return 0;
    }

    private static void HandleError(Element elem, Exception ex)
    {
        Debug.WriteLine("Ошибка", $"Элемент {elem.Id}: {ex.Message}");
    }
}