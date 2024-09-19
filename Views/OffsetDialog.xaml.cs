using System.Windows;
using System.Data.SQLite;
using HVACLoadTerminals.ViewModels;



namespace HVACLoadTerminals.Views
{
    public partial class OffsetDialog : Window
    {
        public OffsetDialog(SQLiteConnection connection, SpaceBoundary _spaceBoundary)
        {
            InitializeComponent();
            DataContext = new OffsetDialogViewModel(connection, _spaceBoundary);
        }
    }

}


    
