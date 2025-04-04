using System;
using System.Linq;

namespace HVACLoadTerminals.PipeSewageHandler;

using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;

public static class PipeExtensions
{
    /// <summary>
    /// Возвращает нормализованное направление вертикальной трубы
    /// </summary>
    public static XYZ GetVerticalDirection(this Pipe pipe)
    {
        var locationCurve = pipe?.Location as LocationCurve;
        var curve = locationCurve?.Curve as Line;
        return curve?.Direction.Normalize() ?? XYZ.BasisZ;
    }

    /// <summary>
    /// Проверяет, является ли труба вертикальной (допуск ±10%)
    /// </summary>
    public static bool IsVertical(this Pipe pipe)
    {
        var locationCurve = pipe?.Location as LocationCurve;
        var curve = locationCurve?.Curve as Line;
        return curve != null && Math.Abs(curve.Direction.Z) > 0.9;
    }
    
    private static void InsertTeeIntoPipe(Document doc, ElementId pipeId, FamilyInstance tee)
{
    /*using Transaction trans = new Transaction(doc, "Insert tee to Pipe");
    trans.Start();*/
    try
    {


        // Получаем исходную трубу
        Pipe originalPipe = doc.GetElement(pipeId) as Pipe;
        if (originalPipe == null)Logger.Log("Труба не найдена.");

        // Получаем позицию тройника
        Connector teeConnector = tee.MEPModel.ConnectorManager.Connectors.Cast<Connector>()
            .FirstOrDefault(c => c.Direction == FlowDirectionType.In);
        if (teeConnector == null) Logger.Log("Коннектор тройника не найден.");

        XYZ teePosition = teeConnector.Origin;

        // Получаем геометрию исходной трубы
        LocationCurve originalLocation = originalPipe.Location as LocationCurve;
        Curve originalCurve = originalLocation.Curve;
        XYZ start = originalCurve.GetEndPoint(0);
        XYZ end = originalCurve.GetEndPoint(1);

        // Сохраняем параметры исходной трубы
        ElementId pipeTypeId = originalPipe.PipeType.Id;
        ElementId levelId = originalPipe.LevelId;
        ElementId systemTypeId = originalPipe.get_Parameter(BuiltInParameter.RBS_PIPING_SYSTEM_TYPE_PARAM).AsElementId();

        // Разбиваем трубу на две части
        XYZ newStart1 = start;
        XYZ newEnd1 = teePosition;
        XYZ newStart2 = teePosition;
        XYZ newEnd2 = end;

        // Удаляем исходную трубу
        doc.Delete(pipeId);

        // Создаем новые сегменты
        Pipe pipe1 = Pipe.Create(
            doc,
            pipeTypeId,
            levelId,
            systemTypeId, // Тип системы
            newStart1,
            newEnd1
        );

        Pipe pipe2 = Pipe.Create(
            doc,
            pipeTypeId,
            levelId,
            systemTypeId, // Тип системы
            newStart2,
            newEnd2
        );

        // Подключаем к тройнику
        /*Connector pipe1End = pipe1.ConnectorManager.Connectors.Cast<Connector>()
            .FirstOrDefault(c => c.Origin.IsAlmostEqualTo(teePosition));
        Connector pipe2End = pipe2.ConnectorManager.Connectors.Cast<Connector>()
            .FirstOrDefault(c => c.Origin.IsAlmostEqualTo(teePosition));

        teeConnector.ConnectTo(pipe1End);
        teeConnector.ConnectTo(pipe2End);*/

        // trans.Commit();
    }
    catch (Exception e)
    {
        Logger.Log($"не смог разрезать трубу {e.Message}");
    }
}
    
    public static void MoveTeeAndConnect(Document doc, ElementId teeId, ElementId pipeId, double moveDistance)
        {
            /*using Transaction trans = new Transaction(doc, "Move Tee and Connect");
            trans.Start();*/
            try
            {


                // Получаем элементы
                FamilyInstance tee = doc.GetElement(teeId) as FamilyInstance;
                Pipe pipe = doc.GetElement(pipeId) as Pipe;

                if (tee == null || pipe == null)
                    throw new Exception("Элемент не найден.");

                // Определяем направление перемещения (вдоль оси трубы)
                LocationCurve pipeLocation = pipe.Location as LocationCurve;
                Curve pipeCurve = pipeLocation.Curve;
                XYZ pipeDirection = (pipeCurve.GetEndPoint(1) - pipeCurve.GetEndPoint(0)).Normalize();

                // Перемещаем тройник
                ElementTransformUtils.MoveElement(doc, teeId, pipeDirection * moveDistance);

                /*
                // Находим коннектор тройника
                Connector teeConnector = tee.MEPModel.ConnectorManager.Connectors.Cast<Connector>()
                    .FirstOrDefault(c => c.Direction == FlowDirectionType.Out);

                // Находим ближайший коннектор трубы
                Connector pipeConnector = pipe.ConnectorManager.Connectors.Cast<Connector>()
                    .OrderBy(c => c.Origin.DistanceTo(teeConnector.Origin))
                    .FirstOrDefault();

                // Проверяем расстояние и подключаем
                if (teeConnector.Origin.DistanceTo(pipeConnector.Origin) <= 0.005) // 5 мм
                {
                    pipeConnector.ConnectTo(teeConnector);
                }
                else
                {
                    throw new Exception("Коннекторы слишком далеко друг от друга.");
                }*/

                //trans.Commit();
            }
            catch (Exception e)
            {
                Logger.Log($"Ошибка: не удалось сдвинуть тройник -{e.Message}");;
                throw;
            }
        }
    
    private static void ConnectTeeToPipe(FamilyInstance tee, Pipe pipe)
        {
            /*using Transaction trans = new Transaction(doc, "Connect Tee to Pipe");
            trans.Start();*/
            try
            {
                if (tee == null || pipe == null)
                    throw new Exception("Элемент не найден.");

                // Получаем кривую трубы
                LocationCurve pipeLocation = pipe.Location as LocationCurve;
                if (pipeLocation == null)
                    throw new Exception("Не удалось получить геометрию трубы.");

                Curve pipeCurve = pipeLocation.Curve;

                // Находим коннектор тройника
                ConnectorSet teeConnectors = tee.MEPModel.ConnectorManager.Connectors;
                Connector teeConnector = null;

                foreach (Connector conn in teeConnectors.Cast<Connector>())
                {
                    // Проверяем направление коннектора
                    if (conn.Direction == FlowDirectionType.Out)
                    {
                        // Проверяем совпадение направления с трубой
                        XYZ direction = conn.CoordinateSystem.BasisZ;
                        XYZ pipeDirection = (pipeCurve.GetEndPoint(0) - conn.Origin).Normalize();

                        if (direction.IsAlmostEqualTo(pipeDirection) || direction.IsAlmostEqualTo(-pipeDirection))
                        {
                            teeConnector = conn;
                            break;
                        }
                    }
                }

                if (teeConnector == null)
                    throw new Exception("Подходящий коннектор тройника не найден.");

                // Находим коннектор трубы
                Connector pipeConnector = pipe.ConnectorManager.Connectors.Cast<Connector>()
                    .FirstOrDefault(c =>
                            c.Direction == FlowDirectionType.Out &&
                            !c.IsConnected && // Проверка на незанятость
                            c.Shape == ConnectorProfileType.Round &&
                            c.Domain == Domain.DomainHvac // или Domain.DomainPiping
                    );

                if (pipeConnector == null)
                    throw new Exception("Подходящий коннектор трубы не найден.");

                // Создаем соединение
                pipeConnector.ConnectTo(teeConnector);
            }
            catch{Logger.Log("Ошибка: не удалось подключиться к трубе");}
            //trans.Commit();
        }
           
    private static void ConnectTeeToPipeSystem(FamilyInstance tee, Pipe pipe)
        {
            var teeConnectors = tee.MEPModel?.ConnectorManager?.Connectors?
                .Cast<Connector>().ToList();

            var pipeConnectors = pipe.ConnectorManager.Connectors
                .Cast<Connector>().ToList();

            if (teeConnectors == null || !pipeConnectors.Any()) return;

            // Получаем позицию тройника через LocationPoint
            if (tee.Location is not LocationPoint locationPoint)
            {
                Logger.Log("Ошибка: тройник не имеет LocationPoint");
                return;
            }

            XYZ teePosition = locationPoint.Point;

            // Находим ближайший коннектор трубы
            Connector pipeConn = pipeConnectors
                .OrderBy(c => c.Origin.DistanceTo(teePosition))
                .First();
        }
}