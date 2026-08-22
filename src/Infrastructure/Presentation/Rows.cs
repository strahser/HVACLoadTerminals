using System.ComponentModel;

namespace HVACLoadTerminals.Infrastructure.Presentation
{
    /// <summary>Editable room row of the snapshot workspace (plan card C2.1/C2.3).
    /// Lives in Infrastructure so App and Revit hosts share the same presenter.</summary>
    public class RoomRow : INotifyPropertyChanged
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

    /// <summary>One computed device position with the loading factor.</summary>
    public class PlacementRow : INotifyPropertyChanged
    {
        public string RoomName { get; set; } = "";
        public string LevelName { get; set; } = "";
        public string Family { get; set; } = "";
        public string TypeName { get; set; } = "";
        public string SystemName { get; set; } = "";
        public double X { get; set; }
        public double Y { get; set; }
        public double RotationDeg { get; set; }

        /// <summary>Load per device / device capacity (0 when not applicable).</summary>
        public double KEf { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
