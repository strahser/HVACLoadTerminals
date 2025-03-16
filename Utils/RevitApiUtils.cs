using System;
using Autodesk.Revit.DB;
using System.IO;
using System.Windows;
using HVACLoadTerminals.Models;


namespace HVACLoadTerminals.Utils
{

    namespace HVACLoadTerminals
    {
        public static class SharedParameterUtils
        {
            /// <summary>
            /// Добавляем общие парметры в ФОП. ФОП должен существовать!Добавляет параметр к выбранной категории.
            /// </summary>
            /// <param name="doc"></param>
            /// <param name="sharedParameter"></param>
            public static void CreateParameterBinding(Document doc, ParameterFields sharedParameter)
            {
                
                var groupName = sharedParameter.GroupName;
                var definitionName = sharedParameter.ParameterName;
                var builtInCategory = sharedParameter.BuiltInCategory;
                var builtInParameterGroup = sharedParameter.BuiltInParameterGroup;
                var parameterType = sharedParameter.ParameterType;
                var isInstanceParameter = sharedParameter.IsInstanceParameter;
                using (var transaction = new Transaction(doc, "Создание пользовательских параметров"))
                {
                    transaction.Start();
                    GetSharedParameterFilePath(doc);
                    DefinitionFile definitionFile=null;
                    // Открытие файла общих параметров
                    if (doc.Application.SharedParametersFilename != null)
                    {
                        definitionFile = doc.Application.OpenSharedParameterFile();
                    }
                    else {MessageBox.Show("Проверьте загружен ли ФОП"); }                    
                    if (definitionFile == null)
                    {
                        GetSharedParameterFilePath(doc);
                        MessageBox.Show("Не удалось открыть файл общих параметров.");
                        return;
                    }
                    // Получение группы общих параметров
                    var definitionGroups = definitionFile.Groups;
                    // Получение группы параметров
                    var group = definitionGroups.get_Item(groupName) ?? definitionGroups.Create(groupName);
                    if (group == null)
                    {
                        ThrowNewException("Не удалось создать группу общих параметров!");
                        return;
                    }
                    // Получение определения параметра
                    var definition = group.Definitions.get_Item(definitionName);
                    if (definition == null)
                    {
                        // Передача имени параметра как строки
                        var externalDefinitionCreationOptions = new ExternalDefinitionCreationOptions(definitionName, parameterType);
                        definition = group.Definitions.Create(externalDefinitionCreationOptions);
                    }
                    // Создание привязки (InstanceBinding или TypeBinding)
                    var categorySet = new CategorySet();
                    ElementBinding binding;
                    if (isInstanceParameter)
                    {
                        var category = Category.GetCategory(doc, builtInCategory);
                        categorySet.Insert(category);
                        binding = doc.Application.Create.NewInstanceBinding(categorySet);
                    }
                    else
                    {
                        var category = Category.GetCategory(doc, builtInCategory);
                        categorySet.Insert(category);
                        binding = doc.Application.Create.NewTypeBinding(categorySet);
                    }
                    // Добавление параметров в проект
                    AddSharedParameterToProject(doc, definition, binding, builtInParameterGroup);
                   transaction.Commit();
                }
            }
            /// <summary>
            /// Добавляем общий параметр в проект
            /// </summary>
            /// <param name="doc"></param>
            /// <param name="definition"></param>
            /// <param name="binding"></param>
            /// <param name="builtInParameterGroup"></param>
            private static string AddSharedParameterToProject(Document doc, Definition definition, ElementBinding binding, BuiltInParameterGroup builtInParameterGroup)
            {
                
                try
                {
                    // Добавление параметра в проект
                    doc.ParameterBindings.Insert(definition, binding, builtInParameterGroup);
                    return definition.Name;
                }

                catch (Exception ex)
                { MessageBox.Show($"Привязка общего параметра не выполнена{ex}"); }

                // Удаление текстового файла общих параметров, так как общие параметры после привязки
                // становятся параметрами проекта только для текущего проекта
                //File.Delete(sharedParameterPath);
                return string.Empty;
            }

            private static string GetSharedParameterFilePath(Document doc)
            {
                var sharedParameterPath = Path.Combine(RevitConfig.ProjectDirectory, "ФОП2019.txt");
                // Удаление существующего файла, если он есть
                //if (File.Exists(sharedParameterPath))
                //{
                //    File.Delete(sharedParameterPath);
                //}

                // Создание нового файла, если его нет
                if (!File.Exists(sharedParameterPath))
                {
                    try
                    {
                        File.Create(sharedParameterPath);
                    }
                    catch (Exception)
                    {
                        ThrowNewException("Ошибка создания файла общих параметров!");
                    }
                }
                // Установка файла общих параметров
                if (doc.Application.SharedParametersFilename != null)
                doc.Application.SharedParametersFilename = sharedParameterPath;
                return sharedParameterPath;
            }

            private static void ThrowNewException(string message)
            {
                throw new Exception(message);
            }
        }
    }
}
