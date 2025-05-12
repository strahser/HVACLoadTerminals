using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using HVACLoadTerminals.Utils;

namespace HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls;

public class FailedFacesManager
{
    private ILogger logger = new LoggingService("FailedFacesManager.txt");
    private readonly Dictionary<string, FailedFaceData> _failedFaces = new();

    private readonly Dictionary<ElementId, (int Count, XYZ Point)> _failedSpaces = new();
    public Dictionary<ElementId, (int Count, XYZ Point)> FailedSpaces => _failedSpaces;
   

    public List<string> FailedFaceKeys => _failedFaces.Keys.ToList();
    

    public void RegisterFailure(string faceKey, Space space, ConstructionSurfaceModel face, Curve curve, string error)
    {
        if (_failedFaces.ContainsKey(faceKey)) return;
        
        // Добавляем информацию о пространстве
        if (space.Location is LocationPoint locPoint)
        {
            var point = locPoint.Point;
            if (_failedSpaces.TryGetValue(space.Id, out var data))
                _failedSpaces[space.Id] = (data.Count + 1, point);
            else
                _failedSpaces[space.Id] = (1, point);
        }

        _failedFaces[faceKey] = new FailedFaceData(faceKey, space, face, curve, error);
        logger.Log($"Failed face: {faceKey} | Error: {error}", LogLevel.Error);
    }
    
    
    public void RetryFailedFaces(Action<FailedFaceData> retryAction)
    {
        foreach (var entry in _failedFaces.ToList())
        {
            try
            {
                retryAction?.Invoke(entry.Value);
                _failedFaces.Remove(entry.Key);
            }
            catch (Exception ex)
            {
                entry.Value.ErrorMessage += $" | Retry failed: {ex.Message}";
            }
        }
    }
    

    // Обновляем при удалении ошибок
    public void RemoveFace(string faceKey)
    {
        if (!_failedFaces.TryGetValue(faceKey, out var data)) return;
        if (_failedSpaces.TryGetValue(data.Space.Id, out var spaceData))
        {
            var newCount = spaceData.Count - 1;
            _failedSpaces[data.Space.Id] = newCount > 0 
                ? (newCount, spaceData.Point) 
                : (0, spaceData.Point);
        }
        _failedFaces.Remove(faceKey);
    }
    
    
    public void UpdateError(string faceKey, string error) => _failedFaces[faceKey].ErrorMessage = error;
    public void LogFailedOperations() => logger.Log($"Failed operations: {_failedFaces.Count}");
}

public class FailedFaceData(string faceKey, Space space, ConstructionSurfaceModel face, Curve curve, string error)
{
    public string FaceKey { get; set; } = faceKey;
    public Space Space { get; } = space;
    public ConstructionSurfaceModel Face { get; } = face;
    public Curve Curve { get; } = curve;
    public string ErrorMessage { get; set; } = error;
}