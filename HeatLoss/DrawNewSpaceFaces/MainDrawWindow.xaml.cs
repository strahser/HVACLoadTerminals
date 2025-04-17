using System.Windows.Controls;

namespace HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces
{
    public partial class MainDrawControl : UserControl
    {
        public MainDrawControl()
        {
            // Создаем ViewModel и UserControl
            var viewModel = new MainDrawViewModel();


            // Устанавливаем DataContext
            this.DataContext = viewModel;
            InitializeComponent();
        }
    }
}