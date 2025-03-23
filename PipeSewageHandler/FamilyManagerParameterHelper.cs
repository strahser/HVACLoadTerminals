using Autodesk.Revit.DB;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB.Structure;

namespace HVACLoadTerminals.PipeSewageHandler;

public class ParameterWrapper
{
    public string Name { get; }
    public StorageType Type { get; }

    public ParameterWrapper(FamilyParameter parameter)
    {
        Name = parameter.Definition.Name;
        Type = parameter.StorageType;
    }
}
public static class FamilyManagerParameterHelper
{
    /// <summary>
    /// Получает параметры экземпляра через FamilyManager
    /// </summary>
    public static List<ParameterWrapper> GetInstanceParameters(FamilySymbol symbol)
    {
        var parameters = new List<ParameterWrapper>();
        Document familyDoc = null;

        try
        {
            // Открываем семейство для редактирования
            familyDoc = symbol.Document.EditFamily(symbol.Family);
            if (familyDoc == null) return parameters;

            // Получаем параметры через FamilyManager
            FamilyManager familyManager = familyDoc.FamilyManager;
            foreach (FamilyParameter fp in familyManager.GetParameters())
            {
                if (fp.IsInstance)
                {
                    parameters.Add(new ParameterWrapper(fp));
                }
            }
        }
        finally
        {
            // Закрываем без сохранения изменений
            familyDoc?.Close(false);
        }

        return parameters;
    }
}