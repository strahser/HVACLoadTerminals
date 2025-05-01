using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Autodesk.Revit.DB;
using HVACLoadTerminals.Utils;

namespace HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Utils;

public class FailureProcessor : IFailuresPreprocessor
{
    private readonly ILogger _logger = new LoggingService();
    private static readonly HashSet<FailureDefinitionId> _allowedWarnings = new()
    {
        // Разрешаем конфликты стен
        BuiltInFailures.OverlapFailures.WallsOverlap,
        
        // Добавьте другие разрешенные предупреждения по необходимости
    };

    public FailureProcessingResult PreprocessFailures(FailuresAccessor failuresAccessor)
    {
        var messages = failuresAccessor.GetFailureMessages();
        if (messages.Count == 0) return FailureProcessingResult.Continue;

        foreach (var message in messages.ToList())
        {
            var id = message.GetFailureDefinitionId();
            
            if (_allowedWarnings.Contains(id))
            {
                try
                {
                    failuresAccessor.DeleteWarning(message);
                }
                catch
                {
                    failuresAccessor.ResolveFailure(message);
                }
            }
            else
            {
                _logger.Log($"Unhandled Revit warning: {message.GetDescriptionText()}");
            }
        }

        return FailureProcessingResult.ProceedWithCommit;
    }
}