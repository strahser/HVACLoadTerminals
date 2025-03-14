using System.Windows;


// Для ObservableCollection

    // WPF окно для выбора документа
    namespace HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.WindowsDoors
    {
        public partial class WindowsAndDoorsSelectionWindow : Window
        {
            public WindowsAndDoorsSelectionWindow()
            {
                InitializeComponent();
            }

            private void CancelButton_Click(object sender, RoutedEventArgs e)
            {
                DialogResult = false;
                Close();
            }
        }

    }