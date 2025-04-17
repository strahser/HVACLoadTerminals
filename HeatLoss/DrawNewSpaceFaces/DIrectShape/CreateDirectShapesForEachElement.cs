using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
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

        int createdElementCount = 0; // Счетчик созданных элементов

        using Transaction tx = new Transaction(doc, "Create Thermal Model");
        tx.Start();

        // Конвертация стен с автоматическим учетом проемов
        foreach (var element in structureElementsList)
        {
            if (CreateThermalElementFromExisting(element, "Тепловая Модель", doc))
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
                var enclosureType =AddAreaValueToElement(elem, ds);
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
            HandleError(elem, ex);
            return false; // Произошла ошибка - элемент не создан
        }
    }

    private static void OverrideGraphicDirectShape(Document doc, Autodesk.Revit.DB.DirectShape ds, string enclosureType)
    {
        Color  enclosureColor = EnclosureColorManager.GetColor(enclosureType, ds);
        OverrideGraphicSettings settings = new OverrideGraphicSettings();

        // Получаем солид
        FillPatternElement solidPattern = GetSolidFillPattern(doc);

        if (solidPattern == null) return;

        // Базовые настройки для всех элементов
        settings.SetSurfaceForegroundPatternId(solidPattern.Id);
        settings.SetSurfaceForegroundPatternColor(enclosureColor);
        settings.SetProjectionLineColor(enclosureColor);

        // Индивидуальные настройки
        ApplyEnclosureSpecificSettings(enclosureType, settings);
        // Применяем настройки
        doc.ActiveView.SetElementOverrides(ds.Id, settings);

    }
    
    private static void ApplyEnclosureSpecificSettings(string enclosureType, OverrideGraphicSettings settings)
    {        switch (enclosureType)
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
    }
    
    private static FillPatternElement GetSolidFillPattern(Document doc)
    {
        return  new FilteredElementCollector(doc)
            .OfClass(typeof(FillPatternElement))
            .Cast<FillPatternElement>()
            .FirstOrDefault(f => f.GetFillPattern().IsSolidFill);
    }
    
    private static string AddAreaValueToElement(Element elem, Autodesk.Revit.DB.DirectShape ds)
    {
        var enclosureType = elem.LookupParameter(nameof(ConstructionSurfaceModel.EnclosureType)).AsValueString();
        var calculateArea = CalculateAreaFactory(elem,enclosureType );
                
        ParametersUtility.SetParameterByValueAndName(ds, nameof(ConstructionSurfaceModel.ConstructionArea), ParameterDisplayConvertor.SquareMeters(calculateArea));
        return enclosureType;
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
                    // Получаем тип свойства через рефлексию
                    PropertyInfo propertyInfo = typeof(ConstructionSurfaceModel).GetProperty(parameterName);
                    if (propertyInfo != null)
                    {
                        Type targetType = propertyInfo.PropertyType;

                        // Получаем значение параметра с учетом типа свойства
                        object parameterValue = ParametersUtility.GetParamValueFromPropertyType(sourceParameter, targetType);

                        // Передаем значение параметра
                        if (parameterValue != null && !string.IsNullOrEmpty(parameterValue.ToString()))
                        {
                            ParametersUtility.SetParameterByValueAndName(ds, parameterName, parameterValue);
                        }
                        else
                        {
                            // Если значение параметра пустое, выводим сообщение об этом
                            Debug.Write("Warning", $"Parameter '{parameterName}' is empty on element {elem.Id.IntegerValue}");
                        }
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
    
    private static void HandleError(Element elem, Exception ex)
    {
        Debug.WriteLine($"Error processing {elem.Id}: {ex}");
        TaskDialog.Show("Ошибка", $"Элемент {elem.Id}: {ex.Message}");
    }
}


