using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI;
using HVACLoadTerminals.ModelsStatic;
using HVACLoadTerminals.Utils;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Document = Autodesk.Revit.DB.Document;

namespace HVACLoadTerminals.HeatLoss.HeatLossResult
{
    public class HeatLossTableViewModel : ReactiveObject
    {
        [Reactive]
        public List<ConstructionSurfaceModel> FaceDataList { get; private set; } = [];

        [Reactive]
        private double? BuildingHeight { get; set; } = CollectorQuery.GetBuildingHeightFromGroundLevel(RevitConfig.Document);

        private Document HvacDocument=>RevitConfig.Document;
        
        private readonly List<string> _calculatedModelsParametersNames = ["SurfaceHeatLoss","OrientationValue", "CornerValue","InfiltrationLoad"];

        #region Commands

        private RelayCommand _createFacesCommand;

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
        public RelayCommand CreateFacesCommand
        {
            get { return _createFacesCommand ??= new RelayCommand(obj => FaceDataList = AggregateEnclosureData()); }
        }
        public RelayCommand SetSurfaceHeatLoadsCommand
        {
            get { return _setSurfaceHeatLoads ??= new RelayCommand(obj => SetSurfaceHeatLoads()); }
        }
        
        #endregion

        private List<ConstructionSurfaceModel> GetAllStructuralData()
        {

            var directShapes = CollectorQuery.GetDirectShapeElements(HvacDocument);

            // Создаем и возвращаем список моделей конструкций
            var constructionSurfaceData = directShapes
                .Select(CreateHeatLossTable)
                .Where(model => model != null) 
                .OrderBy(model => model.SpaceId) 
                .ToList();

            return ConstructionSurfaceModel.SetCornerValue(constructionSurfaceData);
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

        var elementId = new ElementId(elementIdInt);
        var windowHeight = CollectorGeometry.GetWindowHeights(RevitConfig.Document, element);

        return new ConstructionSurfaceModel()
        {
            FaceId = element.Id.ToString(),
            SpaceId = element.LookupParameter(nameof(ConstructionSurfaceModel.SpaceId))?.AsString(),
            SpaceNumber = element.LookupParameter(nameof(ConstructionSurfaceModel.SpaceNumber))?.AsString(),
            Orientation = element.LookupParameter(nameof(ConstructionSurfaceModel.Orientation))?.AsString(),
            ConstructionType = element.LookupParameter(nameof(ConstructionSurfaceModel.ConstructionType))?.AsString(),
            EnclosureType = element.LookupParameter(nameof(ConstructionSurfaceModel.EnclosureType))?.AsString(),
            TransferCoefficient = element.LookupParameter(nameof(ConstructionSurfaceModel.TransferCoefficient))?.AsDouble() ?? 0,
            ConstructionArea = element.LookupParameter(nameof(ConstructionSurfaceModel.ConstructionArea))?.AsDouble() ?? 0,
            TemperatureInSpace = element.LookupParameter(nameof(ConstructionSurfaceModel.TemperatureInSpace))?.AsDouble() ?? 0,
            TemperatureOut = element.LookupParameter(nameof(ConstructionSurfaceModel.TemperatureOut))?.AsDouble() ?? 0,
            BuildingHeight = BuildingHeight != null ? BuildingHeight.Value * 0.3048 : 0,
            InstanceHeight = windowHeight != null ? windowHeight.Value * 0.3048 : 0,
        };
    }
        
        /// <summary>
        /// Получение элементов из Ревит стены, двери, окна
        /// </summary>
        /// <returns></returns>
        ///
        /// 
        private List<Element> GetConstructionQueryElements()
        {
            // Получаем все элементы модели.
            var listWalls = CollectorQuery.GetAllWalls(RevitConfig.Document);
            var listWindows = CollectorQuery.GetAllWindows(RevitConfig.Document);
            var listDoors = CollectorQuery.GetAllDoors(RevitConfig.Document);
            var listFloors = CollectorQuery.GetAllFloors(RevitConfig.Document);
            // Общий список всех элементов
            return listWalls.Concat(listWindows).Concat(listDoors).Concat(listFloors).ToList();
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
        { var data  = GetAllStructuralData();
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
        private void SetSurfaceHeatLoads()
        {
            int totalParametersSet = 0;
            foreach (var surfaceModel in FaceDataList)
            {
                HeatBalanceParametersMappings.SetParametersFromModelToElementByFaceId(HvacDocument, surfaceModel,_calculatedModelsParametersNames, ref totalParametersSet);
            }
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
