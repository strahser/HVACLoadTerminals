using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace HVACLoadTerminals.Revit.Services
{
    /// <summary>
    /// Гасит файлюры при Commit массовых операций размещения: предупреждения
    /// удаляются, к ошибкам применяется резолюция «удалить созданные элементы».
    /// Без этого одна проблемная позиция из ~1200 размещённых приборов
    /// откатывает ВЕСЬ транзакционный прогон (наблюдение S4.1, 2026-08-24:
    /// Commit=RolledBack при уже успешных назначениях систем).
    /// </summary>
    public sealed class MassPlacementFailurePreprocessor : IFailuresPreprocessor
    {
        public FailureProcessingResult PreprocessFailures(FailuresAccessor failuresAccessor)
        {
            foreach (var message in failuresAccessor.GetFailureMessages())
            {
                switch (message.GetSeverity())
                {
                    case FailureSeverity.Warning:
                        failuresAccessor.DeleteWarning(message);
                        break;

                    case FailureSeverity.Error:
                        try
                        {
                            message.SetCurrentResolutionType(
                                FailureResolutionType.DeleteElements);
                            failuresAccessor.ResolveFailures(
                                new List<FailureMessageAccessor> { message });
                        }
                        catch
                        {
                            // не удалось зарезолвить — пусть обрабатывает штатный механизм
                        }
                        break;
                }
            }

            return FailureProcessingResult.Continue;
        }
    }
}
