using System.Windows;

namespace HVACLoadTerminals.HeatLoss
{
    /// <summary>
    /// Логика взаимодействия для RoomBoundingWindow.xaml
    /// </summary>
    public partial class RoomBoundingWindow : Window
    {
        public RoomBoundingWindow(double tout)
        {
 
            InitializeComponent();
            DataContext = new HeatLossTableViewModel(tout); 
        }
    }
}
