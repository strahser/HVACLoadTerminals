using HVACLoadTerminals.ViewModels;
using System.Windows;
using HVACLoadTerminals.DrawNewSpaceFaces;

namespace HVACLoadTerminals.Views
{
    /// <summary>
    /// Логика взаимодействия для RoomBoundingWindow.xaml
    /// </summary>
    public partial class RoomBoundingWindow : Window
    {
        public RoomBoundingWindow()
        {
 
            InitializeComponent();
            DataContext = new RoomBoundingViewModel(); 
        }


    }
}
