using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI;
using HVACLoadTerminals.ClimateData;
using HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces;
using HVACLoadTerminals.HeatLoss.HeatLossResult.Reports;
using HVACLoadTerminals.ModelsStatic;
using HVACLoadTerminals.Utils;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Document = Autodesk.Revit.DB.Document;
using RelayCommand = HVACLoadTerminals.Utils.RelayCommand;

namespace HVACLoadTerminals.HeatLoss.HeatLossResult
{
    public class HeatLossTableViewModel : ReactiveObject
    {
        [Reactive]
        public List<ConstructionSurfaceModel> FaceDataList { get; private set; } = [];
   
        [Reactive]
        private double? BuildingHeight { get; set; } = CollectorQuery.GetBuildingHeightFromGroundLevel(RevitConfig.Document);

        [Reactive] 
        public bool IsExpanded { get; set; } = true;
        private Document HvacDocument=>RevitConfig.Document;

        #region Commands

        private RelayCommand _recalculateDataCommand;

        private RelayCommand _setRoomHeatLoadsCommand;

        private RelayCommand _exportToDocxCommand;
        
        private RelayCommand _setSurfaceHeatLoads;
        public RelayCommand ExportToDocxCommand
        {
            get { return _exportToDocxCommand ??= new RelayCommand(obj => ExportToDocx()); }
        }
        public RelayCommand SetRoomHeatLoadsCommand
        {
            get { return _setRoomHeatLoadsCommand ??= new RelayCommand(obj => SetRoomHeatLoads()); }
        }
        public RelayCommand RecalculateDataCommand
        {
            get { return _recalculateDataCommand ??= new RelayCommand(obj => FaceDataList = AggregateEnclosureData()); }
        }
        public RelayCommand SetSurfaceHeatLoadsCommand
        {
            get { return _setSurfaceHeatLoads ??= new RelayCommand(obj => UpdateConstructionSurfaceData()); }
        }
        #endregion
        
        //Получаем даные из модели ревита
        private List<ConstructionSurfaceModel> GetAllStructuralData()
        {
            var directShapes = CollectorQuery.GetDirectShapeElements(HvacDocument);
            List<ConstructionSurfaceModel> constructionSurfaceData = new List<ConstructionSurfaceModel>();

            foreach (var directShape in directShapes)
            {
                ConstructionSurfaceModel model = CreateHeatLossTable(directShape);

                if (model != null)
                {
                    // Получаем Space по ID из модели
                    Space space = null;
                    try
                    {
                        space = HvacDocument.GetElement(new ElementId(int.Parse(model.SpaceId))) as Space;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Ошибка при получении Space по ID {model.SpaceId}: {ex.Message}");
                    }


                    // Обновляем TemperatureInSpace
                    if (space != null)
                    {
                        model.TemperatureInSpace = ParametersHandler.GetSpaceSetHeatPoint(HvacDocument, space);
                    }
                    else
                    {
                        Debug.WriteLine($"Не удалось получить Space для SpaceId {model.SpaceId}, TemperatureInSpace не обновлено.");
                        model.TemperatureInSpace = 0.0; // Или какое-то значение по умолчанию
                    }

                    // Обновляем TemperatureOut
                    model.TemperatureOut = ParametersHandler.GetProjectInformation(HvacDocument, nameof(ClimateDataModel.TWinterOut092));
                    constructionSurfaceData.Add(model);
                }
            }
            constructionSurfaceData = constructionSurfaceData.OrderBy(m => m.SpaceId).ToList();
            return ConstructionSurfaceModel.SetCornerValue(constructionSurfaceData);
        }

        
        /// <summary>
        /// Автоматически устанавливаем параметры из Ревит поверхности в модель
        /// </summary>
        /// <param name="element"></param>
        /// <param name="buildingHeight"></param>
        /// <param name="windowHeight"></param>
        /// <returns></returns>
        private ConstructionSurfaceModel CreateFromRevitElement(Element element, double? buildingHeight, double? windowHeight)
        {
            var model = new ConstructionSurfaceModel();

            // Получаем все свойства с атрибутом RevitParameter
            var properties = typeof(ConstructionSurfaceModel)
                .GetProperties()
                .Where(p => p.GetCustomAttribute<RevitParameterAttribute>() != null);

            foreach (var prop in properties)
            {
                // Получаем имя параметра из атрибута Description
                var paramName =prop.Name;
        
                // Получаем значение параметра из элемента Revit
                var param = element.LookupParameter(paramName);
                if (param == null) continue;

                // Устанавлием значение в модель
                try
                {
                    object value = ParametersUtility.GetParamValueFromPropertyType(param, prop.PropertyType);
                    prop.SetValue(model, value);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Метод {nameof(CreateFromRevitElement)}-Ошибка при установке параметра {paramName}: {ex.Message}");
                }
            }

            // Специфичные преобразования
            model.BuildingHeight = buildingHeight != null ? buildingHeight.Value * 0.3048 : 0;
            model.OpenInstanceHeight = windowHeight != null ? windowHeight.Value * 0.3048 : 0;
            model.RevitElementId = element.Id.ToString();
            return model;
        }
        
        private ConstructionSurfaceModel CreateHeatLossTable(Element element)
    {
        if (element == null) return null; //Обработка null элемента
        var spaceIdParamValue = element.LookupParameter( nameof(ConstructionSurfaceModel.SpaceId))?.AsString();
        if (!int.TryParse(spaceIdParamValue, out int elementIdInt))
        {
              // Обработка ошибки: параметр SpaceId не является целым числом, выводим в отладку ошибку
              Debug.WriteLine($"Ошибка: Не удалось преобразовать SpaceId элемента {element.Id} в целое число. Значение: {spaceIdParamValue}.");
              return null; // или выбросить исключение
        }
        var windowHeight = CollectorGeometry.GetWindowHeights(RevitConfig.Document, element);
        //получаем данные из модели ревит (обобщенные элементы)
        return CreateFromRevitElement(element,  BuildingHeight, windowHeight);
    }
      
        private List<ConstructionSurfaceModel> AggregateEnclosureData()
        {
            var data = GetAllStructuralData();

            // Группируем и вычисляем субтоталы
            var spaceSubtotals = data
                .GroupBy(x => x.SpaceId)
                .ToDictionary(
                    g => g.Key, 
                    g => g.Sum(x => SafeConvertToDouble(x.SurfaceHeatLoss))
                );

            // Обновляем исходные данные
            foreach (var item in data)
            {
                if (spaceSubtotals.TryGetValue(item.SpaceId, out var subtotal))
                {
                    item.Subtotal = subtotal; 
                }
            }
            return data;
        }

        private  void SetRoomHeatLoads()
        { 
            var data  = GetAllStructuralData();
            var groupedData = data.GroupBy(x => x.SpaceId)
                                .Select(group => new
                                {
                                    SpaceId = group.Key,
                                    TotalHeatLoad = group.Sum(x => SafeConvertToDouble(x.SurfaceHeatLoss)),
                                })
                                .ToList();
            using (var transaction = new Transaction(RevitConfig.Document,"Set Room Heat Loads"))
            {
                transaction.Start();
                
                foreach (var group in groupedData)
                {
                    try
                    {
                        var elementIdInt = int.Parse(group.SpaceId);
                        var elementId = new ElementId(elementIdInt);
                        if (RevitConfig.Document.GetElement(elementId) is Space space)
                        {
                            space.LookupParameter(nameof(SpaceDataModel.HeatLoss)).Set(group.TotalHeatLoad);
                        }
                        else
                        {
                            Debug.Write("Error", $"Не найдено помещение с SpaceId: {group.SpaceId}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.Write("Error", $"Ошибка при установке параметра для помещения {group.SpaceId}: {ex.Message}");
                    }
                }
                transaction.Commit();
            }
            // устанавливаем параметры в элементы ревит
            
            TaskDialog.Show("Информация",$"Тепловые Потери установлены в параметр {nameof(SpaceDataModel.HeatLoss)}");
        }
        
        private void UpdateConstructionSurfaceData()
        {
            int totalParametersSet = 0;
            
            using Transaction transaction = new Transaction(HvacDocument, "Update Heat Loss Data");
            transaction.Start();
            foreach (var surfaceModel in FaceDataList)
            {
                 HeatBalanceParametersMappings.SetParametersFromModelToElement(HvacDocument, surfaceModel);
                totalParametersSet++;
            }
            transaction.Commit();
            TaskDialog.Show("Обновление параметров завершено", $"Параметры успешно обновлены для {totalParametersSet} элементов.");
        }
        
        private static double SafeConvertToDouble(object value)
        {
            if (value == null)
            {
                return 0;
            }

            if (value is double doubleValue)
            {
                return doubleValue;
            }

            if (double.TryParse(value.ToString(), out var parsedDouble))
            {
                return parsedDouble;
            }

            return 0;
        }
        
        private void ExportToDocx()
        {
            var docExporter = new CreateDocxReport(FaceDataList);
            docExporter.ExportToDocx();
        }
    }
}
