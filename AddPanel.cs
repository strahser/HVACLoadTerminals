using Autodesk.Revit.UI;
using System;
using System.Reflection;
using System.Windows.Media.Imaging;

namespace HVACLoadTerminals
{
    public class AddPanel : IExternalApplication
    {
        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }

        public Result OnStartup(UIControlledApplication application)
        {
            // Add a new ribbon panel
            var ribbonPanel = application.CreateRibbonPanel("Энергетическая Модель");
            // Create a push button to trigger a command add it to the ribbon panel.
            var thisAssemblyPath = Assembly.GetExecutingAssembly().Location;
            var buttonData = new PushButtonData("cmdRoomsBounding", "Создать Модель", thisAssemblyPath, 
                "HVACLoadTerminals.Commands.HeatLossTableCommand");
            var pushButton = ribbonPanel.AddItem(buttonData) as PushButton;
            pushButton.ToolTip = "Создает Модель (стены окна и двери).";
            var uriImage = new Uri(@"/HVACLoadTerminals;component/Resources/39-Globe_32x32.png", UriKind.RelativeOrAbsolute);
            var largeImage = new BitmapImage(uriImage);
            pushButton.LargeImage = largeImage;
            return Result.Succeeded;
        }
    }
}
