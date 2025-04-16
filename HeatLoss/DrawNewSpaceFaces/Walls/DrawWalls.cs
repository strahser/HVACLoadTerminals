using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using HVACLoadTerminals.ClimateData;
using HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls.Calculators;
using HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls.ParametersHandlersStrategy;
using HVACLoadTerminals.ModelsStatic;
using HVACLoadTerminals.Utils;

namespace HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls;

public class DrawWalls(Document hvacDocument, Document roomDocument)
{
    public List<Wall> WallList = [];
    private List<Element> Spaces => CollectorQuery.GetAllSpaces(hvacDocument);
    private List<Element> Rooms => CollectorQuery.GetAllRooms(roomDocument);

    public void DrawWallsForSelectedSpaces(string northDirection, Level groundLevel)
    {

        foreach (var space in Spaces.Cast<Space>())
        {
            var selectedRoom =
                RoomAndSpaceCollectorQuery.GetRoomByNumber(space.Number, Rooms); // TODO: не безопасный выбор
            var faceDataList = VerticalWallFacesCalculator.GetRoomExternalVerticalFaces(roomDocument, selectedRoom);
            foreach (var faceData in faceDataList)
            {
                try
                {
                    var newWall = DrawWallBySpaceAndFace(space, faceData, northDirection, groundLevel);
                    Debug.Write($"стена  в пространстве {space.Number} создана");
                    WallList.Add(newWall);
                }
                catch (Exception ex)
                {
                    Debug.Write($"ошибка при создании стены в пространстве {space.Number} {ex}");
                }
            }
        }
    }

    private Wall DrawWallBySpaceAndFace(Space space, 
        ConstructionSurfaceModel faceModel, 
        string northDirection,
        Level groundLevel)
    {
        // Проверка на null space, faceModel или face
        if (space == null || faceModel == null || faceModel._Face == null)
        {
            Debug.WriteLine($"Предупреждение: Пропущен вызов DrawWallBySpaceAndFace из-за null аргументов.");
            return null;
        }

        // Создаем транзакцию
        using var transaction = new Transaction(hvacDocument, $"Создать стену {space.Name}-{space.Number}");
        transaction.Start();
        // Получаем CurveLoops из Face
        var curveLoops = faceModel._Face.GetEdgesAsCurveLoops();
        if (curveLoops == null || curveLoops.Count == 0)
        {
            Debug.WriteLine($"Предупреждение: Не найдены кривые для грани пространства {space.Name}.");
            transaction.RollBack();
            return null; // Если нет CurveLoops, завершаем
        }

        // Создаем CurveArray для определения стены
        var curveArray = new CurveArray();
        foreach (var loop in curveLoops)
        {
            if (loop == null) continue;
            foreach (var curve in loop)
            {
                if (curve != null) curveArray.Append(curve);
            }
        }

        if (curveArray.IsEmpty)
        {
            Debug.WriteLine(
                $"Предупреждение: Не удалось сформировать CurveArray для стены в пространстве {space.Name}.");
            transaction.RollBack();
            return null;
        }

        var wallCurve = curveArray.get_Item(0);
        if (wallCurve == null)
        {
            Debug.WriteLine(
                $"Предупреждение: Не удалось получить кривую для создания стены в пространстве {space.Name}.");
            transaction.RollBack();
            return null;
        }

        var wall = Wall.Create(hvacDocument, wallCurve, space.Level.Id, structural: false);
        SetWallParameters(space, faceModel, northDirection, wallCurve, wall, groundLevel);
        transaction.Commit();
        return wall;
    }

    private void SetWallParameters(
        Space space,
        ConstructionSurfaceModel faceModel,
        string northDirection,
        Curve wallCurve,
        Wall wall,
        Level groundLevel)
    {
        try
        {
            var factory = new WallParametersStrategyFactory(hvacDocument,northDirection);
            var strategy = factory.CreateStrategy(space, groundLevel);

            strategy.ApplyParameters(wall, space, faceModel, wallCurve, groundLevel);

            // Общие параметры
            ParametersUtility.SetParameterByValueAndName(
                wall,
                nameof(faceModel.TemperatureInSpace),
                ParametersHandler.GetSpaceSetHeatPoint(hvacDocument, space)
            );

            ParametersUtility.SetParameterByValueAndName(
                wall,
                nameof(faceModel.TemperatureOut),
                ParametersHandler.GetProjectInformation(
                    hvacDocument,
                    nameof(ClimateDataModel.TWinterOut092)
                )
            );
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Critical error in SetWallParameters: {ex}");
        }
    }
}
    
