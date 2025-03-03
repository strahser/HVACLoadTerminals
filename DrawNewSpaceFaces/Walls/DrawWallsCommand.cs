using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using HVACLoadTerminals.Utils;

namespace HVACLoadTerminals.DrawNewSpaceFaces.Walls
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class DrawWallsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            RevitConfig.Initialize(commandData);
            var hvacDocument = RevitConfig.Document;

            // Создаем экземпляр окна выбора направления и документа
            var selectionWindow = new WallOrientationWindow(hvacDocument);

            // Отображаем окно как модальное.
            bool? result = selectionWindow.ShowDialog();

            // Проверяем, было ли окно закрыто кнопкой "Подтвердить".
            if (result == true)
            {
                // Получаем выбранное направление и документ из окна
                string selectedDirection = selectionWindow.SelectedDirection;
                Document selectedRoomDocument = selectionWindow.SelectedRoomDocument;


                // Создаем экземпляр DrawWalls и вызываем метод
                var walls = new DrawWalls(hvacDocument, selectedRoomDocument);
                walls.DrawWallsForSelectedSpaces(selectedDirection);

                return Result.Succeeded;
            }
            else
            {
                // Пользователь отменил выбор или закрыл окно.
                return Result.Cancelled;
            }
        }
    }
}
