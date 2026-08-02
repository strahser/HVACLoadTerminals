using System;
using System.Reflection;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
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
            try
            {
                // Provide the active Document to integration test fixtures via static holder
                TestDocumentContext.Document = commandData.Application.ActiveUIDocument.Document;

                var results = RevitTestRunner.RunAll(Assembly.GetExecutingAssembly());
                string reportPath = RevitTestRunner.WriteReport(results, "Revit 2024");

                int passed = 0;
                int failed = 0;
                foreach (var r in results)
                {
                    if (r.Passed) passed++;
                    else failed++;
                }

                TaskDialog.Show("HVAC Load Terminals — Tests",
                    $"Passed: {passed}/{results.Count}\n" +
                    $"Failed: {failed}\n" +
                    $"Report: {reportPath}");

                return failed == 0 ? Result.Succeeded : Result.Failed;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("HVAC Load Terminals — Tests", "Error: " + ex.Message);
                return Result.Failed;
            }
        }
    }
}
