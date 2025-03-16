using System.Windows;
using System.Data.SQLite;
using HVACLoadTerminals.CalculateSpaceDevice;


namespace HVACLoadTerminals.Views
{
    public partial class OffsetDialog : Window
    {

        public OffsetDialog(SQLiteConnection connection, SpaceBoundaryCurve spaceBoundaryCurve)
        {
            InitializeComponent();
            DataContext = new OffsetDialogViewModel(connection, spaceBoundaryCurve);
            
        }

    }

}


    
