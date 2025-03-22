using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace HVACLoadTerminals.PipeSewageHandler;

    [Transaction(TransactionMode.Manual)]
public class InsertTeeAtFloorCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        const int offsetValue = 200;
        UIApplication uiapp = commandData.Application;
        UIDocument uidoc = uiapp.ActiveUIDocument;
        Document doc = uidoc.Document;
        double offset = UnitUtils.ConvertToInternalUnits(offsetValue, UnitTypeId.Millimeters);;
        try
        {
            Debug.WriteLine("Начало выполнения скрипта");
            
            // Выбор трубопроводов
            Debug.WriteLine("Запрос выбора трубопроводов...");
            IList<Reference> pipeRefs = uidoc.Selection.PickObjects(
                ObjectType.Element,
                new PipeSelectionFilter(),
                "Выберите вертикальные трубопроводы");

            List<Pipe> pipes = pipeRefs.Select(r => doc.GetElement(r) as Pipe).ToList();
            Debug.WriteLine($"Выбрано трубопроводов: {pipes.Count}");

            // Показать диалог с перечнем труб
            if (pipes.Count > 0)
            {
                string pipeList = string.Join("\n", pipes.Select(p => 
                    $"ID {p.Id}: {p.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM)?.AsValueString()} мм"));
                
                TaskDialog.Show("Выбранные трубопроводы", $"Выбрано вертикальных труб:\n{pipeList}");
            }

            // Поиск пересечений с перекрытиями
            Debug.WriteLine("Начало поиска пересечений с перекрытиями");
            var intersections = new Dictionary<Pipe, List<XYZ>>();
            ElementCategoryFilter floorFilter = new ElementCategoryFilter(BuiltInCategory.OST_Floors);
            
            foreach (Pipe pipe in pipes)
            {
                Debug.WriteLine($"Обработка трубы ID {pipe.Id}");
                var pipeIntersections = FindFloorIntersections(pipe, doc, floorFilter);
                intersections.Add(pipe, pipeIntersections);
                Debug.WriteLine($"Найдено пересечений: {pipeIntersections.Count}");
            }

            // Выбор семейства тройника
            Debug.WriteLine("Выбор семейства тройника...");
            FamilySymbol teeSymbol = SelectTeeFamily(doc);
            if (teeSymbol == null)
            {
                Debug.WriteLine("Тройник не выбран - отмена выполнения");
                return Result.Cancelled;
            }

            // Вставка тройников
            
            Debug.WriteLine($"Смещение для вставки: {offset} внутренних единиц");

            using (Transaction t = new Transaction(doc, "Вставка тройников"))
            {
                t.Start();
                Debug.WriteLine("Старт транзакции");
                
                foreach (var kvp in intersections)
                {
                    Pipe pipe = kvp.Key;
                    Debug.WriteLine($"Вставка тройников для трубы ID {pipe.Id}");
                    
                    foreach (XYZ intersection in kvp.Value)
                    {
                        Debug.WriteLine($"Точка пересечения: {intersection}");
                        XYZ insertionPoint = CalculateInsertionPoint(pipe, intersection, offset);
                        Debug.WriteLine($"Рассчитанная точка вставки: {insertionPoint}");
                        
                        InsertTee(pipe, insertionPoint, teeSymbol, doc);
                        Debug.WriteLine("Тройник успешно вставлен");
                    }
                }
                
                t.Commit();
                Debug.WriteLine("Коммит транзакции");
            }

            Debug.WriteLine("Скрипт выполнен успешно");
            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ОШИБКА: {ex.Message}\n{ex.StackTrace}");
            message = ex.Message;
            return Result.Failed;
        }
    }

    private List<XYZ> FindFloorIntersections(Pipe pipe, Document doc, ElementCategoryFilter floorFilter)
    {
    List<XYZ> intersections = new List<XYZ>();
    Debug.WriteLine($"Поиск пересечений для трубы ID {pipe?.Id}");

    // Проверка pipe на null
    if (pipe == null)
    {
        Debug.WriteLine("ОШИБКА: Переданный объект pipe равен null");
        return intersections;
    }

    LocationCurve lc = pipe.Location as LocationCurve;
    if (lc == null)
    {
        Debug.WriteLine("Труба не имеет LocationCurve");
        return intersections;
    }

    Curve curve = lc.Curve;
    if (curve == null)
    {
        Debug.WriteLine("ОШИБКА: Не удалось получить кривую из LocationCurve");
        return intersections;
    }
    Debug.WriteLine($"Тип кривой трубы: {curve.GetType().Name}");

    FilteredElementCollector floors = new FilteredElementCollector(doc).WherePasses(floorFilter);
    Debug.WriteLine($"Найдено перекрытий: {floors.Count()}");

    foreach (Element floor in floors)
    {
        if (floor == null)
        {
            Debug.WriteLine("Пропуск null-перекрытия");
            continue;
        }

        Debug.WriteLine($"Обработка перекрытия ID {floor.Id}");
        Options geomOptions = new Options { ComputeReferences = true };
        GeometryElement geomElement = floor.get_Geometry(geomOptions);

        // Проверка GeometryElement
        if (geomElement == null)
        {
            Debug.WriteLine("ОШИБКА: Не удалось получить GeometryElement для перекрытия");
            continue;
        }

        foreach (GeometryObject geomObj in geomElement)
        {
            if (geomObj == null)
            {
                Debug.WriteLine("Пропуск null-геометрии");
                continue;
            }

            Solid solid = geomObj as Solid;
            if (solid == null)
            {
                Debug.WriteLine("Пропуск не-Solid объекта: " + geomObj.GetType().Name);
                continue;
            }

            if (solid.Faces.Size == 0)
            {
                Debug.WriteLine("Пропуск пустого Solid");
                continue;
            }

            Debug.WriteLine($"Обработка Solid с {solid.Faces.Size} гранями");
            try
            {
                SolidCurveIntersectionOptions options = new SolidCurveIntersectionOptions();
                SolidCurveIntersection intersection = solid.IntersectWithCurve(curve, options);

                // Критическая проверка на null
                if (intersection == null)
                {
                    Debug.WriteLine("Результат пересечения равен null");
                    continue;
                }

                if (intersection.SegmentCount > 0)
                {
                    Debug.WriteLine($"Найдено сегментов пересечения: {intersection.SegmentCount}");
                    for (int i = 0; i < intersection.SegmentCount; i++)
                    {
                        Curve segment = intersection.GetCurveSegment(i);
                        if (segment == null)
                        {
                            Debug.WriteLine($"ОШИБКА: Сегмент {i} равен null");
                            continue;
                        }

                        XYZ start = segment.GetEndPoint(0);
                        XYZ end = segment.GetEndPoint(1);
                        if (start != null) intersections.Add(start);
                        if (end != null) intersections.Add(end);
                        Debug.WriteLine($"Добавлены точки: {start} - {end}");
                    }
                }
                else
                {
                    Debug.WriteLine("Пересечений не найдено");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ОШИБКА при обработке Solid: {ex.Message}");
            }
        }
    }

    return intersections;
}

    private XYZ CalculateInsertionPoint(Pipe pipe, XYZ intersection, double offset)
        {
            Line line = (pipe.Location as LocationCurve).Curve as Line;
            XYZ direction = line.Direction;
            return intersection + direction * offset;
        }

    private void InsertTee(Pipe pipe, XYZ insertionPoint, FamilySymbol teeSymbol, Document doc)
        {
            if (!teeSymbol.IsActive) teeSymbol.Activate();

            FamilyInstance tee = doc.Create.NewFamilyInstance(
                insertionPoint,
                teeSymbol,
                pipe,
                Autodesk.Revit.DB.Structure.StructuralType.NonStructural);

            // Логика подключения соединителей
            ConnectorSet connectors = tee.MEPModel.ConnectorManager.Connectors;
            Connector pipeConnector = GetConnectorClosestToPoint(pipe, insertionPoint);
            
            foreach (Connector conn in connectors)
            {
                if (conn.CoordinateSystem.BasisZ.IsAlmostEqualTo(pipeConnector.CoordinateSystem.BasisZ))
                {
                    conn.ConnectTo(pipeConnector);
                    break;
                }
            }
        }

    private Connector GetConnectorClosestToPoint(Pipe pipe, XYZ point)
        {
            ConnectorSet connectors = pipe.ConnectorManager.Connectors;
            Connector closest = null;
            double minDist = double.MaxValue;

            foreach (Connector c in connectors)
            {
                double dist = c.Origin.DistanceTo(point);
                if (dist < minDist)
                {
                    minDist = dist;
                    closest = c;
                }
            }
            return closest;
        }

    private FamilySymbol SelectTeeFamily(Document doc)
        {
            FilteredElementCollector collector = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_PipeFitting)
                .WhereElementIsElementType();

            var teeSymbols = collector.Cast<FamilySymbol>()
                //.Where(fs => fs.FamilyName.IndexOf("Tee", StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();

            TeeSelectorWindow window = new TeeSelectorWindow(teeSymbols);
            window.ShowDialog();

            return window.SelectedSymbol;
        }
    
    }

    public class PipeSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            if (!(elem is Pipe pipe)) return false;
            
            LocationCurve lc = pipe.Location as LocationCurve;
            if (lc == null) return false;

            Line line = lc.Curve as Line;
            return line != null && IsVertical(line.Direction);
        }

        private bool IsVertical(XYZ direction)
        {
            return Math.Abs(direction.Z) > 0.9; // Учитываем возможные отклонения
        }

        public bool AllowReference(Reference reference, XYZ position) => false;
    }



 