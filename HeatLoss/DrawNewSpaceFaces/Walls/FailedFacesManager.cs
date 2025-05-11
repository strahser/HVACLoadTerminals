using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using HVACLoadTerminals.Utils;

namespace HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls;

public class FailedFacesManager(ILogger logger)
{
    private readonly Dictionary<string, FailedFaceData> _failedFaces = new();
   

    public List<string> FailedFaceKeys => _failedFaces.Keys.ToList();
    

    public void RegisterFailure(string faceKey, Space space, ConstructionSurfaceModel face, Curve curve, string error)
    {
        if (_failedFaces.ContainsKey(faceKey)) return;
        _failedFaces[faceKey] = new FailedFaceData(faceKey,space, face, curve, error);
        logger.Log($"Failed face: {faceKey} | Error: {error}",LogLevel.Error);
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

    public void RemoveFace(string faceKey) => _failedFaces.Remove(faceKey);
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