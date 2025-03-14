using System.Windows;

namespace HVACLoadTerminals.HeatLoss.HeatLossResult
{
    /// <summary>
    /// Логика взаимодействия для HeatLossWindow.xaml
    /// </summary>
    public partial class HeatLossWindow : Window
    {
        public HeatLossWindow()
        {
            InitializeComponent();
            DataContext = new HeatLossTableViewModel(); 
        }
    }
}
