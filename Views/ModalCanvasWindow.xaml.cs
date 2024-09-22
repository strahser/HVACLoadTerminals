using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace HVACLoadTerminals.Views
{
    /// <summary>
    /// Логика взаимодействия для ModalCanvasWindow.xaml
    /// </summary>
    public partial class ModalCanvasWindow : Window
    {
        public ModalCanvasWindow(Canvas canvas)
        {
            InitializeComponent();

            InitializeComponent();
            Content = canvas;

            // Customize the modal window (optional)
            Title = "CustomCanvas Details";
            Width = 500;
            Height = 400;
        }
    }
}
