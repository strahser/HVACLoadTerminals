using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Data.SQLite;
using HVACLoadTerminals.ViewModels;
using System.Windows.Controls.DataVisualization.Charting;
using System.Collections.Generic;
using System.Windows.Data;



namespace HVACLoadTerminals.Views
{
    public partial class OffsetDialog : Window
    {
        public OffsetDialog(SQLiteConnection connection, SpaceBoundary _spaceBoundary, string _jsonPath)
        {
            InitializeComponent();
            DataContext = new OffsetDialogViewModel(connection, _spaceBoundary, _jsonPath);
        }

    }

}


    
