using System.ComponentModel;

namespace HVACLoadTerminals.App.ViewModels
{
    /// <summary>Editable row of the snapshot rooms table (plan card C2.1).</summary>
    public class RoomRowViewModel : INotifyPropertyChanged
    {
        public string RoomId { get; set; } = "";
        public string Number { get; set; } = "";
        public string Name { get; set; } = "";
        public string LevelName { get; set; } = "";
        public double Area { get; set; }
        public bool IsCorner { get; set; }

        private string _purpose = "";
        public string Purpose
        {
            get => _purpose;
            set { _purpose = value; OnPropertyChanged(nameof(Purpose)); }
        }

        private double _heatingW;
        public double HeatingW
        {
            get => _heatingW;
            set { _heatingW = value; OnPropertyChanged(nameof(HeatingW)); }
        }

        private double _supply;
        public double Supply
        {
            get => _supply;
            set { _supply = value; OnPropertyChanged(nameof(Supply)); }
        }

        private double _exhaust;
        public double Exhaust
        {
            get => _exhaust;
            set { _exhaust = value; OnPropertyChanged(nameof(Exhaust)); }
        }

        public string Warning { get; set; } = "";

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>One computed device position (result table).</summary>
    public class PlacementRowViewModel
    {
        public string RoomName { get; set; } = "";
        public string LevelName { get; set; } = "";
        public string Family { get; set; } = "";
        public string TypeName { get; set; } = "";
        public string SystemName { get; set; } = "";
        public double X { get; set; }
        public double Y { get; set; }
        public double RotationDeg { get; set; }
    }
}
