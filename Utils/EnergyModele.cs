using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Analysis;
using HVACLoadTerminals.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using HVACLoadTerminals.DrawNewSpaceFaces;
using HVACLoadTerminals.HeatLoss;
using HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces;

namespace HVACLoadTerminals.Utils
{
    public class EnergyModele

    {
        private Document doc { get; set; }
        private List<EnergyAnalysisSpace> _analiticalSpaces { get; set; }
        public List<ConstructionSurfaceModel> FaceDataList { get; set; }
        //конструктор
        public EnergyModele(Document _doc)
        {
            doc = _doc;
            FaceDataList = new List<ConstructionSurfaceModel>();
            _analiticalSpaces = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_AnalyticSpaces)
                .WhereElementIsNotElementType()
                .ToElements()
                .Cast<EnergyAnalysisSpace>()
                .ToList();
        }
        public  void CalculateAnalyticalSurfaceAreas(){
            var modelExist = IfAnaliticalModelExist();
                // Получаем аналитические поверхности для каждого пространства
                var surfaceAreas = new List<List<double>>();
                foreach (var space in _analiticalSpaces)
                {
                GetSurfaceData(space);
            }
                MessageBox.Show($"Успешно получены{FaceDataList.Count}");

        }
        private string _getRoomName(EnergyAnalysisSpace energyAnalysisSpace)
        {
            try { 
                // Получаем имя комнаты из EnergyAnalysisSpace
                var roomNameParam = energyAnalysisSpace.get_Parameter(BuiltInParameter.SPACE_ASSOC_ROOM_NAME).AsValueString();
                return roomNameParam;
            }
            catch {
                return "";
            }
        }

        private void GetSurfaceData(EnergyAnalysisSpace space)
        {
            var tempSurfaces = space.GetAnalyticalSurfaces().ToList();

            foreach (var surface in tempSurfaces)
            {
                var orientation = GetOrientation(surface);
                try
                {
                    // Получаем площадь поверхности в квадратных метрах
                    var Surfacearea = surface.get_Parameter(BuiltInParameter.RBS_GBXML_SURFACE_AREA).AsDouble() * 0.09290304;
                    var opens = surface.GetAnalyticalOpenings().ToList();


                    var surfaceData = new ConstructionSurfaceModel()
                    {
                        SpaceId = space.Id.ToString(),
                        SpaceNumber = space.ComposedName,
                        FaceId = surface.Id.ToString(),
                        ConstructionArea = Surfacearea,
                        Orientation = orientation,
                        EnclosureType = surface.SurfaceType.ToString(),
                        ConstructionType = surface.OriginatingElementDescription
                    };
                    FaceDataList.Add(surfaceData);
                    foreach (var open in opens)
                    {
                        var Openarea = open.get_Parameter(BuiltInParameter.RBS_GBXML_SURFACE_AREA).AsDouble() * 0.09290304;
                        var OpeningData = new ConstructionSurfaceModel()
                        {
                            SpaceId = space.Id.ToString(),
                            FaceId = surface.Id.ToString(),
                            ConstructionArea = Openarea,
                            Orientation = orientation,
                            EnclosureType = open.OpeningType.ToString(),
                            ConstructionType = open.OriginatingElementDescription
                        };
                        FaceDataList.Add(OpeningData);
                    }
                }
                catch (Exception ex)
                {
                    Debug.Write($"Ошибка {ex}");
                }
            }
        }
        private string GetOrientation(EnergyAnalysisSurface surface)
        {
            var azimuth = surface.get_Parameter(BuiltInParameter.AZIMUTH).AsDouble();

            // Преобразуем азимут в градусы
            var azimuthDegrees = azimuth * 180 / Math.PI;
            // Определяем сторону света
            string direction;
            if (azimuthDegrees >= 315 || azimuthDegrees < 45)
            {
                direction = "С"; // Север
            }
            else if (azimuthDegrees >= 45 && azimuthDegrees < 135)
            {
                direction = "В"; // Восток
            }
            else if (azimuthDegrees >= 135 && azimuthDegrees < 225)
            {
                direction = "Ю"; // Юг
            }
            else
            {
                direction = "З"; // Запад
            }
            return direction;
        }
        private bool IfAnaliticalModelExist()
        {
            // Настройки аналитической модели
            var options = new EnergyAnalysisDetailModelOptions();
            options.Tier = EnergyAnalysisDetailModelTier.Final;
            // Проверяем, существует ли аналитическая модель
            var eadm = new FilteredElementCollector(doc)
                .OfClass(typeof(EnergyAnalysisDetailModel))
                .FirstOrDefault() as EnergyAnalysisDetailModel;

            // Создаем аналитическую модель, если она не существует
            if (eadm == null)
            {
                try
                {

                    using (var transaction = new Transaction(doc, "Создание аналитической модели"))
                    {
                        transaction.Start();
                        eadm = EnergyAnalysisDetailModel.Create(doc, options);
                        transaction.Commit();
                    }

                }
                catch(Exception ex)
                {
                    Debug.Write(ex);
                }
            }
            return eadm == null;
        }
    }
}
