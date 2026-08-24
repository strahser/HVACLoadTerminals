using System.Collections.Generic;
using System.ComponentModel;
using HVACLoadTerminals.Core.Models;

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

        /// <summary>U1.2: комната участвует в расчёте/расстановке.</summary>
        private bool _isIncluded = true;
        public bool IsIncluded
        {
            get => _isIncluded;
            set { _isIncluded = value; OnPropertyChanged(nameof(IsIncluded)); }
        }

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

        public List<SystemRow> Systems { get; set; } = new List<SystemRow>();

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>One named system of a room (S1.1): a room may carry several
    /// supply and several exhaust systems, each placed independently.</summary>
    public class SystemRow : INotifyPropertyChanged
    {
        private string _name = "";
        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(nameof(Name)); }
        }

        private HVACSystemType _type = HVACSystemType.Supply;
        public HVACSystemType Type
        {
            get => _type;
            set { _type = value; OnPropertyChanged(nameof(Type)); }
        }

        private double _flowM3h;
        public double FlowM3h
        {
            get => _flowM3h;
            set { _flowM3h = value; OnPropertyChanged(nameof(FlowM3h)); }
        }

        private bool _isIncluded = true;
        public bool IsIncluded
        {
            get => _isIncluded;
            set { _isIncluded = value; OnPropertyChanged(nameof(IsIncluded)); }
        }

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

        /// <summary>
        /// U3.1: цветовая группа k_ef для таблиц и плана: «low» (&lt;0.6 недогруз),
        /// «ok» (0.6–0.9 норма), «high» (&gt;0.9 перегруз); пусто — неприменимо.
        /// </summary>
        public string KefStatus =>
            KEf <= 0 ? "" : KEf < 0.6 ? "low" : KEf > 0.9 ? "high" : "ok";

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
