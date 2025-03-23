// InsertTeeAtFloorCommand.cs
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Autodesk.Revit.DB.Structure;

namespace HVACLoadTerminals.PipeSewageHandler
{
    [Transaction(TransactionMode.Manual)]
    public class InsertTeeAtFloorCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;

            try
            {
                // Выбор труб
                var pipes = new TeeProcessor.PipeSelectionFilter().SelectVerticalPipes(uidoc);
                if (pipes.Count == 0) return Result.Cancelled;

                // Настройка параметров
                var configWindow = new TeeConfigurationWindow(doc);
                if (configWindow.ShowDialog() != true) return Result.Cancelled;

                // Обработка вставки
                var processor = new TeeProcessor(doc, configWindow);
                var insertedTees = processor.Process(pipes);

                // Применение параметров
                if (insertedTees.Any())
                {
                    using (Transaction t = new Transaction(doc, "Update Parameters"))
                    {
                        t.Start();
                        processor.ApplyParameters(insertedTees);
                        t.Commit();
                    }
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.ToString();
                return Result.Failed;
            }
        }
    }

    // Класс для обработки вставки тройников
    internal class TeeProcessor(Document doc, TeeConfigurationWindow config)
    {
        private readonly FamilySymbol _symbol = config.SelectedSymbol;
        private readonly double _offset = config.Offset;

        public List<FamilyInstance> Process(List<Pipe> pipes)
        {
            var insertedTees = new List<FamilyInstance>();
            var intersections = new IntersectionFinder(doc).Find(pipes);

            using var t = new Transaction(doc, "Insert Tees");
            t.Start();
            foreach (var pipe in pipes)
            {
                foreach (var point in intersections[pipe])
                {
                    var tee = InsertTee(pipe, point);
                    ConfigureTeeLogs(tee, pipe, point);
                    insertedTees.Add(tee);
                }
            }

            t.Commit();

            return insertedTees;
        }

        private FamilyInstance InsertTee(Pipe pipe, XYZ point)
        {
            if (!_symbol.IsActive) _symbol.Activate();
            
            // Получаем направление трубы и корректируем точку вставки
            Line pipeLine = (pipe.Location as LocationCurve).Curve as Line;
            XYZ pipeDirection = pipeLine.Direction;
            
            // Смещение вдоль направления трубы, а не вертикали
            XYZ offsetPoint = point + (pipeDirection * _offset);
            var tee =doc.Create.NewFamilyInstance(
                offsetPoint,
                _symbol,
                pipe, // Хост-труба
                StructuralType.NonStructural);
            return tee;
        }

        private void ConfigureTeeLogs(FamilyInstance tee, Pipe pipe, XYZ point)
        {
            try
            {
                Logger.Log($"\n=== Начало обработки тройника {tee.Id} ===");
        
                // Логирование информации о трубе
                Logger.Log($"\nИнформация о трубе {pipe.Id}:");
                Logger.LogPipeGeometry(pipe);
                Logger.LogPipeConnectors(pipe, point);

                // Повороты тройника
                RotateTee(tee, pipe);
        
                Logger.Log($"\nСостояние тройника после поворотов:");
                Logger.LogConnectorInfo(tee, "После поворотов");

                // Подключение коннекторов
                Logger.Log($"\nПопытка подключения...");
                ConnectorAnalytics.Connect(tee, pipe, point);
        
                // Проверка результатов (исправленный вызов)
                Logger.Log($"\nИтоговое состояние подключений:");
                Logger.LogConnectorInfo(tee, "После подключения");
            }
            catch (Exception ex)
            {
                Logger.Log($"Критическая ошибка: {ex.Message}");
            }
        }

        private void RotateTee(FamilyInstance tee, Pipe pipe)
        {
            if (tee.Location is not LocationPoint locationPoint)
            {
                Logger.Log("Ошибка: тройник не имеет LocationPoint");
                return;
            }

            XYZ pipeDirection = ((pipe.Location as LocationCurve)!.Curve as Line)!.Direction;
            XYZ teeDirectionZ = XYZ.BasisZ; // Направление коннектора тройника по умолчанию
            XYZ teeDirectionY = XYZ.BasisY; // Направление коннектора тройника по умолчанию

            // Вычисляем угол между направлением трубы и тройником
            double angleZ = pipeDirection.AngleTo(teeDirectionZ);
            double angleY = pipeDirection.AngleTo(teeDirectionY);

            // Поворот вокруг оси Z
            ElementTransformUtils.RotateElement(
                doc,
                tee.Id,
                Line.CreateBound(locationPoint.Point, locationPoint.Point + XYZ.BasisZ),
                angleZ
            );
            ElementTransformUtils.RotateElement(
                doc,
                tee.Id,
                Line.CreateBound(locationPoint.Point, locationPoint.Point + XYZ.BasisY),
                angleY
            );

            doc.Regenerate();
        }
        
        public void ApplyParameters(List<FamilyInstance> tees)
        {
            foreach (var tee in tees)
            {
                foreach (var paramConfig in config.SelectedParameters)
                {
                    Parameter param = tee.LookupParameter(paramConfig.Key);
                    if (param == null || param.IsReadOnly) continue;

                    try
                    {
                        switch (param.StorageType)
                        {
                            case StorageType.Double:
                                if (double.TryParse(paramConfig.Value, out double dVal))
                                    param.Set(dVal);
                                break;
                            case StorageType.Integer:
                                if (int.TryParse(paramConfig.Value, out int iVal))
                                    param.Set(iVal);
                                break;
                            case StorageType.String:
                                param.Set(paramConfig.Value);
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"Ошибка установки параметра {param.Definition.Name}: {ex.Message}");
                    }
                }
            }
        }
        

        // Класс поиска пересечений
        internal class IntersectionFinder(Document doc)
        {
            public Dictionary<Pipe, List<XYZ>> Find(List<Pipe> pipes)
            {
                var results = new Dictionary<Pipe, List<XYZ>>();
                var filter = new ElementCategoryFilter(BuiltInCategory.OST_Floors);

                foreach (var pipe in pipes)
                {
                    var locationCurve = pipe.Location as LocationCurve;
                    if (locationCurve?.Curve is not Line curve) continue;

                    var intersections = new List<XYZ>();
                    var floors = new FilteredElementCollector(doc)
                        .WherePasses(filter)
                        .WhereElementIsNotElementType();

                    foreach (Element floor in floors)
                    {
                        var geomOptions = new Options { ComputeReferences = true };
                        using (var geomElement = floor.get_Geometry(geomOptions))
                        {
                            if (geomElement == null) continue;

                            foreach (GeometryObject geomObj in geomElement)
                            {
                                if (geomObj is not Solid solid || solid.Faces.Size == 0) continue;

                                var intersection = solid.IntersectWithCurve(
                                    curve,
                                    new SolidCurveIntersectionOptions());

                                if (intersection?.SegmentCount > 0)
                                {
                                    for (int i = 0; i < intersection.SegmentCount; i++)
                                    {
                                        var segment = intersection.GetCurveSegment(i);
                                        if (segment == null) continue;

                                        XYZ upperPoint = segment.GetEndPoint(0).Z > segment.GetEndPoint(1).Z
                                            ? segment.GetEndPoint(0)
                                            : segment.GetEndPoint(1);
                                        intersections.Add(upperPoint);
                                    }
                                }
                            }
                        }
                    }

                    results[pipe] = intersections;
                }

                return results;
            }
        }

        // Фильтр выбора труб
        public class PipeSelectionFilter : ISelectionFilter
        {
            public List<Pipe> SelectVerticalPipes(UIDocument uidoc)
            {
                try
                {
                    var refs = uidoc.Selection.PickObjects(
                        ObjectType.Element,
                        this,
                        "Выберите вертикальные трубы");

                    return refs.Select(r => uidoc.Document.GetElement(r) as Pipe).ToList();
                }
                catch
                {
                    return new List<Pipe>();
                }
            }

            public bool AllowElement(Element elem) =>
                elem is Pipe pipe && pipe.IsVertical();

            public bool AllowReference(Reference reference, XYZ position) => false;
        }

        // Класс для работы с коннекторами
        private static class ConnectorAnalytics
        {
            public static void Connect(FamilyInstance tee, Pipe pipe, XYZ point)
                {
                var teeConnectors = tee.MEPModel?.ConnectorManager?.Connectors?
                    .Cast<Connector>()
                    .ToList();

                var pipeConnectors = pipe.ConnectorManager.Connectors
                    .Cast<Connector>()
                    .OrderBy(c => c.Origin.DistanceTo(point))
                    .ToList();

                if (teeConnectors == null || !pipeConnectors.Any()) 
                {
                    Logger.Log("Ошибка: отсутствуют коннекторы");
                    return;
                }

                foreach (Connector teeConn in teeConnectors)
                {
                    // Увеличение допустимого расстояния до 0.5 единиц
                    Connector nearestPipeConn = pipeConnectors
                        .FirstOrDefault(pc => pc.Origin.DistanceTo(teeConn.Origin) < 0.5);

                    if (nearestPipeConn != null)
                    {
                        // Проверка направления с допуском
                        if (IsMatchingDirection(teeConn, nearestPipeConn, 0.1))
                        {
                            try
                            {
                                teeConn.ConnectTo(nearestPipeConn);
                                Logger.Log($"Коннектор {teeConn.Id} подключен к {nearestPipeConn.Id}");
                            }
                            catch (Exception ex)
                            {
                                Logger.Log($"Ошибка подключения: {ex.Message}");
                            }
                            break;
                        }
                    }
                }
                }

            // Проверка направления с допуском
            private static bool IsMatchingDirection(Connector a, Connector b, double tolerance = 0.1)
            {
                return a.CoordinateSystem.BasisZ.IsAlmostEqualTo(b.CoordinateSystem.BasisZ.Negate(), tolerance);
            }
        
         }

        // Класс для работы с семействами
        public static class FamilySelector
        {
            public static void LoadSymbols(Document doc, System.Windows.Controls.ComboBox comboBox)
            {
                comboBox.ItemsSource = new FilteredElementCollector(doc)
                    .OfClass(typeof(FamilySymbol))
                    .OfCategory(BuiltInCategory.OST_PipeFitting)
                    .Cast<FamilySymbol>()
                    .ToList();
            }

            public static IEnumerable<ParameterWrapper> GetEditableParameters( FamilySymbol symbol)
            {
                return symbol.Parameters
                    .Cast<Parameter>()
                    .Where(p => !p.IsReadOnly)
                    .Select(p => new ParameterWrapper(p));
            }
        }

        // Модель для отображения параметров
        public class ParameterWrapper : INotifyPropertyChanged
        {
            private bool _isSelected;
            private string _value;

            public string Name { get; }
            public StorageType Type { get; }
    
            public bool IsSelected
            {
                get => _isSelected;
                set
                {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                }
            }

            public string Value
            {
                get => _value;
                set
                {
                    _value = value;
                    OnPropertyChanged(nameof(Value));
                }
            }

            public ParameterWrapper(Parameter parameter)
            {
                Name = parameter.Definition.Name;
                Type = parameter.StorageType;
                Value = parameter.AsValueString() ?? parameter.AsString() ?? "";
            }

            public event PropertyChangedEventHandler PropertyChanged;
    
            protected virtual void OnPropertyChanged(string propertyName)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public static class FamilyParameterHelper
        {
            public static List<ParameterWrapper> GetInstanceParameters(Document doc, FamilySymbol symbol)
            {
                using (Transaction tempTx = new Transaction(doc, "Temp Insert"))
                {
                    tempTx.Start();

                    try
                    {
                        // Создаем временный экземпляр в скрытом виде
                        var tempTee = doc.Create.NewFamilyInstance(
                            XYZ.Zero,
                            symbol,
                            StructuralType.NonStructural);

                        // Собираем параметры
                        var parameters = tempTee.Parameters
                            .Cast<Parameter>()
                            .Where(p => !p.IsReadOnly)
                            .Select(p => new ParameterWrapper(p))
                            .ToList();

                        // Удаляем временный объект
                        doc.Delete(tempTee.Id);

                       tempTx.RollBack(); // Откатываем транзакцию
                        return parameters;
                    }
                    catch
                    {
                        tempTx.RollBack();
                        return new List<ParameterWrapper>();
                    }
                }
            }
        }
    }
}
