using Autodesk.Revit.DB;
using System;
using System.Linq;

namespace HVACLoadTerminals.GSOP;

public static class ProjectInfoRevitExtensions
{
    /// <summary>
    /// Метод расширения для получения строкового параметра из OST_ProjectInformation по имени.
    /// </summary>
    /// <param name="document">Текущий документ Revit.</param>
    /// <param name="parameterName">Имя параметра.</param>
    /// <returns>Значение параметра.</returns>
    public static string GetProjectInfoString(this Document document, string parameterName)
    {
        var projectInfo = GetProjectInformationElement(document);
        var parameter = projectInfo.LookupParameter(parameterName);

        if (parameter == null || string.IsNullOrEmpty(parameter.AsString()))
        {
            throw new ArgumentException($"Параметр '{parameterName}' не найден или не имеет значения.");
        }

        return parameter.AsString();
    }

    /// <summary>
    /// Метод расширения для получения числового параметра из OST_ProjectInformation по имени.
    /// </summary>
    /// <param name="document">Текущий документ Revit.</param>
    /// <param name="parameterName">Имя параметра.</param>
    /// <returns>Значение параметра.</returns>
    public static double GetProjectInfoDouble(this Document document, string parameterName)
    {
        var projectInfo = GetProjectInformationElement(document);
        var parameter = projectInfo.LookupParameter(parameterName);

        if (parameter == null || !parameter.HasValue)
        {
            throw new ArgumentException($"Параметр '{parameterName}' не найден или не имеет значения.");
        }

        return parameter.AsDouble();
    }

    /// <summary>
    /// Метод расширения для установки строкового значения параметра в OST_ProjectInformation.
    /// </summary>
    /// <param name="document">Текущий документ Revit.</param>
    /// <param name="parameterName">Имя параметра.</param>
    /// <param name="value">Новое значение параметра.</param>
    public static void SetProjectInfoString(this Document document, string parameterName, string value)
    {
        using (var transaction = new Transaction(document, "Set Project Info String"))
        {
            transaction.Start();

            var projectInfo = GetProjectInformationElement(document);
            var parameter = projectInfo.LookupParameter(parameterName);

            if (parameter == null)
            {
                throw new ArgumentException($"Параметр '{parameterName}' не найден.");
            }

            parameter.Set(value);

            transaction.Commit();
        }
    }

    /// <summary>
    /// Метод расширения для установки числового значения параметра в OST_ProjectInformation.
    /// </summary>
    /// <param name="document">Текущий документ Revit.</param>
    /// <param name="parameterName">Имя параметра.</param>
    /// <param name="value">Новое значение параметра.</param>
    public static void SetProjectInfoDouble(this Document document, string parameterName, double value)
    {
        using (var transaction = new Transaction(document, "Set Project Info Double"))
        {
            transaction.Start();

            var projectInfo = GetProjectInformationElement(document);
            var parameter = projectInfo.LookupParameter(parameterName);

            if (parameter == null)
            {
                throw new ArgumentException($"Параметр '{parameterName}' не найден.");
            }

            parameter.Set(value);

            transaction.Commit();
        }
    }

    /// <summary>
    /// Вспомогательный метод для получения элемента OST_ProjectInformation.
    /// </summary>
    /// <param name="document">Текущий документ Revit.</param>
    /// <returns>Элемент OST_ProjectInformation.</returns>
    private static Element GetProjectInformationElement(Document document)
    {
        var projectInfoFilter = new ElementCategoryFilter(BuiltInCategory.OST_ProjectInformation);
        var collector = new FilteredElementCollector(document).WherePasses(projectInfoFilter);
        var projectInfo = collector.FirstOrDefault();

        if (projectInfo == null)
        {
            throw new InvalidOperationException("Элемент OST_ProjectInformation не найден.");
        }

        return projectInfo;
    }
}