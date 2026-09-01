using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using HVACLoadTerminals.Core.Services;
using HVACLoadTerminals.Infrastructure.Data;
using HVACLoadTerminals.Infrastructure.Presentation;
using HVACLoadTerminals.Revit.UI;

namespace HVACLoadTerminals.Revit.Commands
{
    /// <summary>
    /// Настройки — единое окно (оборудование · расчёт и геометрия с графикой · нагрузки · прочие).
    /// Для стенда используется тот же SnapshotWorkspacePresenter, что и в App, чтобы настройки
    /// оборудования и глобальные правила были общими (%AppData%).
    /// Вне стенда — лёгкий presenter на дефолтах (без снимка) для правки каталога и UI.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class SettingsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var repo = ResolveCatalog();
                string catalogPath = (repo as JsonCatalogRepository)?.FilePath ?? JsonCatalogRepository.ResolveDefaultPath();
                string uiPath = JsonUiSettingsStore.ResolveDefaultPath();

                var detail = $"Каталог приборов:\n{catalogPath}\n\nНастройки UI:\n{uiPath}\n\n" +
                             $"Для полной настройки (оборудование · методы расчёта с графикой · нагрузки · прочие) откройте автономный стенд:\n" +
                             $"d:\\Projects\\HVACLoadTerminals\\production\\HVACLoadTerminals.App\\HVACLoadTerminals.App.exe\n\n" +
                             $"или стенд «Стенд расстановки» → Настройки (доступны после открытия стенда).";

                TaskDialog.Show("Настройки — HVAC Terminals", detail);

                try { System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{catalogPath}\""); } catch { }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }

        private static HVACLoadTerminals.Core.Interfaces.ITerminalCatalogRepository ResolveCatalog()
        {
            try
            {
                var repo = new JsonCatalogRepository(JsonCatalogRepository.ResolveDefaultPath());
                repo.EnsureSeeded();
                repo.GetAllDevices();
                return repo;
            }
            catch
            {
                return new DemoCatalogRepository();
            }
        }
    }
}
