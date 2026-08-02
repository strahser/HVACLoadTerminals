using System;
using System.Reflection;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using HVACLoadTerminals.Revit.Logging;
using HVACLoadTerminals.Revit.Testing;

namespace HVACLoadTerminals.Revit.Commands
{
    /// <summary>
    /// Runs every [RevitTest] method in the Revit assembly, writes the JSON
    /// report to %LocalAppData%\HVACLoadTerminals\TestResults and shows a
    /// summary dialog. Result.Succeeded when all tests pass.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class RevitTestRunnerCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            const string cmd = "RevitTestRunner";
            HvacLogger.Info($"{cmd} started");
            try
            {
                // Provide the active Document to integration test fixtures via static holder
                var activeDoc = commandData.Application.ActiveUIDocument?.Document;
                TestDocumentContext.Document = activeDoc;
                HvacLogger.Info($"  Active doc: {(activeDoc != null ? activeDoc.Title : "<null>")}");

                var results = RevitTestRunner.RunAll(Assembly.GetExecutingAssembly());
                string reportPath = RevitTestRunner.WriteReport(results, "Revit 2024");

                int passed = 0;
                int failed = 0;
                foreach (var r in results)
                {
                    if (r.Passed) passed++;
                    else
                    {
                        failed++;
                        HvacLogger.Warn($"  Test failed: {r.Fixture}.{r.Method} — {r.Error}");
                    }
                }

                HvacLogger.Info($"{cmd} finished: {passed}/{results.Count} passed, {failed} failed. Report: {reportPath}");

                TaskDialog.Show("HVAC Load Terminals — Tests",
                    $"Passed: {passed}/{results.Count}\n" +
                    $"Failed: {failed}\n" +
                    $"Report: {reportPath}");

                return failed == 0 ? Result.Succeeded : Result.Failed;
            }
            catch (Exception ex)
            {
                HvacLogger.LogException($"{cmd} failed", ex);
                TaskDialog.Show("HVAC Load Terminals — Tests",
                    $"Error: {ex.Message}\n\nLog:\n{HvacLogger.LogFilePath}");
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
