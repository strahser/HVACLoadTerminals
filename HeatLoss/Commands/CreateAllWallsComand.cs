using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI;

namespace HVACLoadTerminals.HeatLoss.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class CreateAllWallsComand : IExternalCommand
    {
        private Document _doc;
        private const double _tolerance = 0.01;
        private string _logPath;
        private int _wallsCreated;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            _doc = uidoc.Document;
            _logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "ConvexHullAnalysisLog.txt");

            try
            {
                File.WriteAllText(_logPath, "Начало анализа выпуклой оболочки\n");


                    // Получаем все помещения, сгруппированные по уровням
                    var spacesByLevel = new FilteredElementCollector(_doc)
                        .OfCategory(BuiltInCategory.OST_MEPSpaces)
                        .WhereElementIsNotElementType()
                        .Cast<Space>()
                        .GroupBy(s => s.LevelId);

                    Log($"Найдено уровней: {spacesByLevel.Count()}");
                    foreach (var levelGroup in spacesByLevel)
                    {
                        ProcessLevel(levelGroup.Key, levelGroup.ToList());
                    }

                    Log($"Всего создано стен: {_wallsCreated}");

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                Log($"КРИТИЧЕСКАЯ ОШИБКА: {ex}");
                return Result.Failed;
            }
        }

        private void ProcessLevel(ElementId levelId, List<Space> spaces)
        {
            Level level = _doc.GetElement(levelId) as Level;
            if (level == null)
            {
                Log($"Уровень {levelId} не найден, пропуск");
                return;
            }

            Log($"\nОбработка уровня: {level.Name}");
            foreach (Space space in spaces)
            {
                using Transaction tx = new Transaction(_doc, "Создание стен по границам помещений");
                tx.Start();
                try
                {
                    CreateWallsForSpace(space, level);
                    tx.Commit();
                }
                catch (Exception e)
                {
                    tx.RollBack();
                }
            }
        }

        private void CreateWallsForSpace(Space space, Level level)
        {
            // Получаем границы помещения
            var boundaries = space.GetBoundarySegments(new SpatialElementBoundaryOptions());

            // Получаем высоту помещения через BuiltInParameter
            Parameter heightParam = space.get_Parameter(BuiltInParameter.ROOM_UPPER_OFFSET); // Высота помещения
            double height = heightParam?.AsDouble() ?? 3.0; // Если параметр отсутствует, используем значение по умолчанию (3 метра)

            foreach (var loop in boundaries)
            {
                foreach (var segment in loop)
                {
                    Curve curve = segment.GetCurve();
                    if (curve == null) continue;

                    XYZ start = curve.GetEndPoint(0);
                    XYZ end = curve.GetEndPoint(1);

                    try
                    {
                        // Создаем стену между двумя точками
                        Wall wall = Wall.Create(_doc, Line.CreateBound(start, end), level.Id, false); // false означает, что это не структурная стена

                        // Устанавливаем высоту стены
                        Parameter wallHeightParam = wall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM);
                        if (wallHeightParam != null && !wallHeightParam.IsReadOnly)
                        {
                            wallHeightParam.Set(height);
                        }

                        _wallsCreated++;
                        Log($"Создана стена: {wall.Id}, высота: {height:F2}m");
                    }
                    catch (Exception ex)
                    {
                        Log($"Ошибка создания стены: {ex.Message}");
                    }
                }
            }
        }
        private void Log(string message)
        {
            string logMessage = $"{DateTime.Now:HH:mm:ss.fff} | {message}";
            Debug.WriteLine(logMessage);
            File.AppendAllText(_logPath, logMessage + Environment.NewLine);
        }
    }
}