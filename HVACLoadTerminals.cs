using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
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
            RibbonPanel ribbonPanel = application.CreateRibbonPanel("NewRibbonPanel");
            // Create a push button to trigger a command add it to the ribbon panel.
            string thisAssemblyPath = Assembly.GetExecutingAssembly().Location;
            PushButtonData buttonData = new PushButtonData("cmdHelloWorld", "Тест1", thisAssemblyPath, "HVACLoadTerminals.Commands.RoomsBounding");
            PushButtonData buttonData2 = new PushButtonData("cmdHelloWorld2", "Тест2", thisAssemblyPath, "HVACLoadTerminals.Commands.RoomsBounding");
            PushButton pushButton = ribbonPanel.AddItem(buttonData) as PushButton;
            PushButton pushButton2 = ribbonPanel.AddItem(buttonData2) as PushButton;
            // Optionally, other properties may be assigned to the button
            // a) tool-tip
            pushButton.ToolTip = "Say hello to the entire world.";
            // b) large bitmap
            Uri uriImage = new Uri(@"/HVACLoadTerminals;component/Resources/39-Globe_32x32.png", UriKind.RelativeOrAbsolute);
            BitmapImage largeImage = new BitmapImage(uriImage);
            pushButton.LargeImage = largeImage;
            pushButton2.LargeImage = largeImage;
            return Result.Succeeded;
        }
    }

}
