using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Autodesk.Revit.DB;
using HVACLoadTerminals.Utils;

namespace HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Utils;

// Класс для управления обработкой ошибок

// Класс для управления обработкой ошибок
public class FailureProcessor : IFailuresPreprocessor
{
    private int _processingAttempts = 0;
    private const int MaxProcessingAttempts = 1;

    public FailureProcessingResult PreprocessFailures(FailuresAccessor failuresAccessor)
    {
        var _logger = new LoggingService();
        IList<FailureMessageAccessor> failureMessages = failuresAccessor.GetFailureMessages();

        if (failureMessages.Count == 0 || _processingAttempts++ > MaxProcessingAttempts)
        {
            return FailureProcessingResult.Continue;
        }

        bool hasCriticalErrors = false;
        var handledFailures = new List<FailureMessageAccessor>();

        foreach (FailureMessageAccessor failure in failureMessages)
        {
            try
            {
                // Проверяем тип ошибки
                if (failure.GetSeverity() == FailureSeverity.Warning)
                {
                    // Автоматическое разрешение для предупреждений
                    failuresAccessor.ResolveFailure(failure);
                    handledFailures.Add(failure);
                }
                else
                {
                    // Обработка критических ошибок
                    _logger.Log($"Critical error: {failure.GetDescriptionText()}", LogLevel.Error);
                    hasCriticalErrors = true;
                }
            }
            catch (Exception ex)
            {
                _logger.Log($"Resolution error: {ex.Message} | {failure.GetDescriptionText()}", LogLevel.Error);
                handledFailures.Add(failure);
                hasCriticalErrors = true;
            }
        }

        // Удаляем обработанные предупреждения
        foreach (var failure in handledFailures)
        {
            failuresAccessor.DeleteWarning(failure);
        }

        return hasCriticalErrors 
            ? FailureProcessingResult.ProceedWithRollBack 
            : FailureProcessingResult.ProceedWithCommit;
    }
}