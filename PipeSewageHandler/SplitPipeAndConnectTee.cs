using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HVACLoadTerminals.PipeSewageHandler;


  [Transaction(TransactionMode.Manual)]
    public class SplitPipeAndConnectTee : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;

            try
            {
                Logger.Log("Starting SplitPipeAndConnectTee command.");

                // 1. Выбор тройника (Tee Fitting)
                Reference teeReference = uidoc.Selection.PickObject(ObjectType.Element, new TeeSelectionFilter(), "Выберите тройник (Tee Fitting)");
                Element teeElement = doc.GetElement(teeReference);

                if (teeElement == null || !(teeElement is FamilyInstance))
                {
                    message = "Выбранный элемент не является тройником (Tee Fitting).";
                    return Result.Failed;
                }

                FamilyInstance tee = teeElement as FamilyInstance;
                Logger.Log("Selected Tee: {0}", tee.Name);

                // 2. Поиск ближайшей трубы
                Pipe nearestPipe = FindNearestPipe(doc, tee);

                if (nearestPipe == null)
                {
                    message = "Рядом с тройником не найдена труба.";
                    return Result.Failed;
                }
                Logger.Log("Nearest Pipe: {0}", nearestPipe.Name);

                // 3. Определение точки разреза на трубе (рядом с коннектором тройника)
                XYZ splitPoint = GetSplitPointNearTeeConnector(doc, tee, nearestPipe);

                if (splitPoint == null)
                {
                    message = "Не удалось определить точку разреза трубы.";
                    Logger.Log("Failed to determine split point.");
                    return Result.Failed;
                }
                Logger.Log("Split Point: {0}", splitPoint.ToString());

                // 4. Разделение трубы
                using (Transaction transaction = new Transaction(doc, "Разделить трубу и подключить тройник"))
                {
                    transaction.Start();

                    // Разделяем трубу в заданной точке.
                    bool splitSuccessful = SplitPipe(doc, nearestPipe.Id, splitPoint);

                    if (!splitSuccessful)
                    {
                        message = "Не удалось разделить трубу.";
                        Logger.Log("Failed to split pipe.");
                        transaction.RollBack();
                        return Result.Failed;
                    }
                    Logger.Log("Pipe split successfully.");

                    // Refresh the pipe object.  Важно: После Split, ID трубы может измениться.
                    // Получаем оба сегмента трубы.  Нам нужно убедиться, что pipeId и второй сегмент действительны.
                    Pipe firstPipe = doc.GetElement(nearestPipe.Id) as Pipe;
                    Pipe secondPipe = FindNearestPipeSegment(doc, splitPoint); //Ищем трубу рядом с точкой разделения.

                    if (firstPipe == null || secondPipe == null)
                    {
                        message = "Не удалось найти сегменты трубы после разделения.";
                        Logger.Log("Failed to find pipe segments after split.");
                        transaction.RollBack();
                        return Result.Failed;
                    }
                    Logger.Log("First Pipe Segment: {0}, Second Pipe Segment: {1}", firstPipe.Name, secondPipe.Name);


                    // 5. Подключение тройника к трубе
                    bool connected = TryConnectTeeToPipe(doc, tee, firstPipe, secondPipe);

                    if (connected)
                    {
                        transaction.Commit();
                        TaskDialog.Show("Успех", "Труба разделена и тройник подключен.");
                        return Result.Succeeded;
                    }
                    else
                    {
                        transaction.RollBack();
                        message = "Не удалось подключить тройник к трубе.";
                        Logger.Log("Failed to connect tee to pipe.");
                        return Result.Failed;
                    }
                }
            }
            catch (Exception ex)
            {
                message = ex.Message;
                Logger.LogException(ex, "Execute");
                return Result.Failed;
            }
            finally
            {
                Logger.Log("Finished SplitPipeAndConnectTee command.");
            }
        }

        // Фильтр для выбора только тройников
        private class TeeSelectionFilter : ISelectionFilter
        {
            public bool AllowElement(Element elem)
            {
                //Проверяем, что это FamilyInstance и Plumbing Fixture
                if (elem is FamilyInstance fi && fi.Category.Id.IntegerValue.Equals((int)BuiltInCategory.OST_PipeFitting))
                {
                    return true; // Это FamilyInstance и Pipe Fitting, значит разрешаем выбор
                }
                return false;
            }

            public bool AllowReference(Reference reference, XYZ position)
            {
                return false; // Не разрешаем выбор по Reference, только по элементу.
            }
        }

        // Находит ближайшую трубу к тройнику
        private Pipe FindNearestPipe(Document doc, FamilyInstance tee)
        {
            try
            {
                Logger.Log("Finding nearest pipe to tee.");
                XYZ teeLocation = ((LocationPoint)tee.Location).Point;
                FilteredElementCollector collector = new FilteredElementCollector(doc)
                    .OfClass(typeof(Pipe));

                Pipe nearestPipe = null;
                double minDistance = double.MaxValue;

                foreach (Element element in collector)
                {
                    Pipe pipe = element as Pipe;
                    Curve pipeCurve = (pipe.Location as LocationCurve).Curve;
                    double distance = pipeCurve.Distance(teeLocation);

                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        nearestPipe = pipe;
                    }
                }
                if(nearestPipe != null) Logger.Log("Found nearest pipe: {0}", nearestPipe.Name);
                else Logger.Log("No nearest pipe found.");
                return nearestPipe;
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "FindNearestPipe");
                return null;
            }
        }

        // Находит ближайший сегмент трубы к точке
        private Pipe FindNearestPipeSegment(Document doc, XYZ point)
        {
             try
            {
                Logger.Log("Finding nearest pipe segment to point: {0}", point.ToString());
                FilteredElementCollector collector = new FilteredElementCollector(doc)
                    .OfClass(typeof(Pipe));

                Pipe nearestPipe = null;
                double minDistance = double.MaxValue;

                foreach (Element element in collector)
                {
                    Pipe pipe = element as Pipe;
                    Curve pipeCurve = (pipe.Location as LocationCurve).Curve;
                    double distance = pipeCurve.Distance(point);

                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        nearestPipe = pipe;
                    }
                }
                if (nearestPipe != null) Logger.Log("Found nearest pipe segment: {0}", nearestPipe.Name);
                else Logger.Log("No nearest pipe segment found.");
                return nearestPipe;
            }
             catch (Exception ex)
            {
                Logger.LogException(ex, "FindNearestPipeSegment");
                return null;
            }

        }


        // Определяет точку разреза на трубе рядом с коннектором тройника
        private XYZ GetSplitPointNearTeeConnector(Document doc, FamilyInstance tee, Pipe pipe)
{
    try
    {
        Logger.Log("Getting split point using projection method.");
        ConnectorSet connectors = GetConnectors(tee);

        if (connectors == null)
        {
            Logger.Log("Connectors is null.");
            return null;
        }

        XYZ nearestPoint = null;
        double minDistance = double.MaxValue;
        const double Tolerance = 0.001;

        foreach (Connector connector in connectors)
        {
            if (connector.ConnectorType == ConnectorType.End)
            {
                XYZ connectorPosition = connector.Origin;
                Curve pipeCurve = (pipe.Location as LocationCurve).Curve;
                double distance = pipeCurve.Distance(connectorPosition);

                Logger.Log("Connector position: {0}, Distance to pipe: {1}", connectorPosition, distance);

                if (distance <= Tolerance && distance < minDistance)
                {
                    minDistance = distance;
                    nearestPoint = connectorPosition;
                }
            }
        }

        if (nearestPoint == null)
        {
            Logger.Log("No suitable connector found within tolerance.");
            return null;
        }

        Logger.Log("Nearest Point: {0}", nearestPoint);

        Curve curve = (pipe.Location as LocationCurve).Curve;
        XYZ pipeDirection = (curve.GetEndPoint(1) - curve.GetEndPoint(0)).Normalize();
        Logger.Log("Pipe Direction: {0}", pipeDirection);

        const double VerticalTolerance = 0.001;
        if (Math.Abs(pipeDirection.X) > VerticalTolerance || Math.Abs(pipeDirection.Y) > VerticalTolerance)
        {
            Logger.Log("Pipe is not vertical.");
            return null;
        }

        // Проекция точки на горизонтальную плоскость
        Plane horizontalPlane = Plane.CreateByNormalAndOrigin(XYZ.BasisZ, nearestPoint);
        XYZ projectedPoint = ProjectPointOntoPlane(nearestPoint, horizontalPlane);

        Logger.Log("Projected Point: {0}", projectedPoint);

        return projectedPoint;
    }
    catch (Exception ex)
    {
        Logger.LogException(ex, "GetSplitPointUsingProjection");
        return null;
    }
}
        
        // Аналог Deep Seek не работает
        private XYZ GetSplitPointNearTeeConnectorDeepSeek(Document doc, FamilyInstance tee, Pipe pipe)
        {
            try
            {
                Logger.Log("Getting split point near tee connector (Vertical Pipe).");
                ConnectorSet connectors = GetConnectors(tee);

                if (connectors == null)
                {
                    Logger.Log("Connectors is null.");
                    return null;
                }

                XYZ nearestPoint = null;
                double minDistance = double.MaxValue;
                const double Tolerance = 0.001;

                foreach (Connector connector in connectors)
                {
                    if (connector.ConnectorType == ConnectorType.End)
                    {
                        XYZ connectorPosition = connector.Origin;
                        Curve pipeCurve = (pipe.Location as LocationCurve).Curve;
                        double distance = pipeCurve.Distance(connectorPosition);
                        Logger.Log("Connector position: {0}, Distance to pipe: {1}", connectorPosition, distance);

                        if (distance <= Tolerance && distance < minDistance)
                        {
                            minDistance = distance;
                            nearestPoint = connectorPosition;
                        }
                    }
                }

                if (nearestPoint == null)
                {
                    Logger.Log("No suitable connector found within tolerance.");
                    return null;
                }
                Logger.Log("Nearest Point: {0}", nearestPoint);

                Curve curve = (pipe.Location as LocationCurve).Curve;
                XYZ pipeDirection = (curve.GetEndPoint(1) - curve.GetEndPoint(0)).Normalize();
                Logger.Log("Pipe Direction: {0}", pipeDirection);

                const double VerticalTolerance = 0.001;
                if (Math.Abs(pipeDirection.X) > VerticalTolerance || Math.Abs(pipeDirection.Y) > VerticalTolerance)
                {
                    Logger.Log("Pipe is not vertical.");
                    return null;
                }

                Plane horizontalPlane = Plane.CreateByNormalAndOrigin(XYZ.BasisZ, nearestPoint);
                XYZ startPoint = curve.GetEndPoint(0);
                XYZ endPoint = curve.GetEndPoint(1);

                XYZ projectedStartPoint = ProjectPointOntoPlane(startPoint, horizontalPlane);
                XYZ projectedEndPoint = ProjectPointOntoPlane(endPoint, horizontalPlane);

                Logger.Log("Projected Start: {0}, End: {1}", projectedStartPoint, projectedEndPoint);

                double projectedLength = projectedStartPoint.DistanceTo(projectedEndPoint);
                double shortCurveTolerance = doc.Application.ShortCurveTolerance;
                Logger.Log("Projected Length: {0}, Tolerance: {1}", projectedLength, shortCurveTolerance);

                XYZ splitPoint = null;

                if (projectedLength <= shortCurveTolerance)
                {
                    Logger.Log("Using solid closest point.");

                    Options geoOptions = new Options { DetailLevel = ViewDetailLevel.Fine };
                    GeometryElement geoElement = pipe.get_Geometry(geoOptions);
                    Solid pipeSolid = null;

                    foreach (GeometryObject geoObj in geoElement)
                    {
                        if (geoObj is GeometryInstance instance)
                        {
                            foreach (GeometryObject instObj in instance.GetInstanceGeometry())
                            {
                                if (instObj is Solid solid && solid.Volume > 0)
                                {
                                    pipeSolid = solid;
                                    break;
                                }
                            }
                        }
                        else if (geoObj is Solid solid && solid.Volume > 0)
                        {
                            pipeSolid = solid;
                            break;
                        }
                        if (pipeSolid != null) break;
                    }

                    if (pipeSolid == null)
                    {
                        Logger.Log("Pipe solid not found.");
                        return null;
                    }

                    // Алгоритм поиска ближайшей точки на Solid (работает во всех версиях Revit)
                    XYZ closestPoint = FindClosestPointOnSolid(nearestPoint, pipeSolid);
                    Logger.Log("Closest Point on Solid: {0}", closestPoint);
                    splitPoint = closestPoint;
                }
                else
                {
                    Line projectedLine = Line.CreateBound(projectedStartPoint, projectedEndPoint);
                    splitPoint = ProjectPointOntoLine(nearestPoint, projectedLine);
                    Logger.Log("Projected Point on Line: {0}", splitPoint);
                }

                return splitPoint;
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "GetSplitPointNearTeeConnector");
                return null;
            }
}

        // Метод для поиска ближайшей точки на Solid (альтернатива ComputeClosestPoint)
        private XYZ FindClosestPointOnSolid(XYZ point, Solid solid)
{
    double minDistance = double.MaxValue;
    XYZ closestPoint = null;

    foreach (Face face in solid.Faces)
    {
        XYZ projection = face.Project(point).XYZPoint;
        double distance = projection.DistanceTo(point);

        if (distance < minDistance)
        {
            minDistance = distance;
            closestPoint = projection;
        }
    }

    return closestPoint ?? point; // На крайний случай возвращаем исходную точку
}

        private XYZ ProjectPointOntoPlane(XYZ point, Plane plane)
        {
            XYZ vec = point - plane.Origin;
            double distance = plane.Normal.DotProduct(vec);
            return point - distance * plane.Normal;
        }

        private XYZ ProjectPointOntoLine(XYZ point, Line line)
        {
            XYZ start = line.GetEndPoint(0);
            XYZ direction = (line.GetEndPoint(1) - start).Normalize();
            double t = (point - start).DotProduct(direction);
            t = Math.Max(0, Math.Min(t, line.Length));
            return start + direction * t;
        }


        // Пытается подключить тройник к трубе
        private bool TryConnectTeeToPipe(Document doc, FamilyInstance tee, Pipe pipe1, Pipe pipe2)
        {

            Connector teeConnector = FindUnconnectedConnector(tee);
            Connector pipe1Connector = FindUnconnectedConnector(pipe1);
            Connector pipe2Connector = FindUnconnectedConnector(pipe2);

            if (teeConnector != null && pipe1Connector != null && pipe2Connector != null)
            {
                try
                {
                    //Пытаемся создать соединение между тройником и первой трубой
                    //doc.Create.NewElbow(pipe1Connector, teeConnector); // NewElbow - не подходит, нужно определить какой фитинг создавать
                    ConnectTwoConnectors(doc, pipe1Connector, teeConnector);
                    //Пытаемся создать соединение между тройником и второй трубой
                    teeConnector = FindUnconnectedConnector(tee); //Получаем опять, т.к. предыдущее соединение могло его занять.
                    if (teeConnector != null)
                    {
                        //doc.Create.NewElbow(pipe2Connector, teeConnector);
                        ConnectTwoConnectors(doc, pipe2Connector, teeConnector);
                    }
                    return true; //Предполагаем успех, если не было исключений
                }
                catch (Exception ex)
                {
                    TaskDialog.Show("Ошибка", "Не удалось подключить тройник: " + ex.Message);
                    return false;
                }
            }
            else
            {
                TaskDialog.Show("Ошибка", "Не удалось найти свободные коннекторы.");
                return false;
            }


        }


        // Находит первый свободный коннектор на элементе
        private Connector FindUnconnectedConnector(Element element)
        {
             try
            {
                ConnectorSet connectors = GetConnectors(element); // Используем общую функцию для получения коннекторов.

                if (connectors != null)
                {
                    foreach (Connector connector in connectors)
                    {
                        if (connector.Domain == Domain.DomainHvac || connector.Domain == Domain.DomainPiping) // или любой другой домен
                        {
                            if (connector.IsConnected == false)
                            {
                                return connector;
                            }
                        }
                    }
                }
                return null;
            }
             catch (Exception ex)
            {
                Logger.LogException(ex, "FindUnconnectedConnector");
                return null;
            }

        }
        
        
        // Соединяет трубу с коннектором
        private void ConnectTwoConnectors(Document doc, Connector c1, Connector c2)
        {
            //Проверяем, можно ли их соединить.
            if (c1.IsConnected || c2.IsConnected) return;

            //Пробуем соединить напрямую.
            try
            {
                c1.ConnectTo(c2);
                return;
            }
            catch { }

 
            //TODO:  Реализовать логику определения типа фитинга и его создания.

            //Пример:
            //FamilySymbol elbowType = FindElbowType(doc, c1, c2); //Функция для поиска подходящего типа отвода
            //if (elbowType != null)
            //{
            //  doc.Create.NewFamilyInstance(c1.Origin, elbowType, StructuralType.NonStructural); //Создаем отвод в месте соединения
            //  //После создания отвода нужно его подключить к коннекторам.  Это отдельная задача.
            //}
        }


        // Helper function to safely get connectors from an element.
        private ConnectorSet GetConnectors(Element element)
        {
           try
            {
                ConnectorManager connectorManager = null;

                if (element is FamilyInstance fi)
                {
                    connectorManager = fi.MEPModel?.ConnectorManager;
                }
                else if (element is Pipe p)
                {
                    //Получаем ConnectorManager для трубы.
                    //Важно: У трубы нет прямого свойства MEPModel. Нужно использовать ConnectorManager.
                    //connectorManager = p.ConnectorManager; //Это работать не будет.
                    //Вместо этого нужно получить коннекторы напрямую:

                    ConnectorSet connectors = p.ConnectorManager?.Connectors;
                    if (connectors != null && connectors.Size > 0)
                    {
                        return connectors; //Возвращаем коннекторы напрямую.
                    }
                    return null; //Если коннекторы не найдены.
                }

                if (connectorManager != null)
                {
                    return connectorManager.Connectors;
                }

                return null;
            }
           catch (Exception ex)
            {
                Logger.LogException(ex, "GetConnectors");
                return null;
            }

        }

        // Splitting pipe using PlumbingUtils.BreakCurve
        private bool SplitPipe(Document document, ElementId pipeId, XYZ point)
        {
            try
            {
                Logger.Log("Splitting pipe with ID: {0} at point: {1}", pipeId.IntegerValue, point.ToString());
                PlumbingUtils.BreakCurve(document, pipeId, point);
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "SplitPipe");
                return false;
            }
        }
    }


