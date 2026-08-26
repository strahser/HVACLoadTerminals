using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using HVACLoadTerminals.Core.Models;
using HVACLoadTerminals.Infrastructure.Presentation;

namespace HVACLoadTerminals.App.ViewModels
{
    /// <summary>
    /// IC1: VM для одного помещения на Canvas-плане. Содержит RoomRow + Polygon2D (мм)
    /// + PointCollection для Polygon + Brush + IsSelected/IsHovered. Лёгкая, без
    /// зависимостей от WPF resources (Brush создаётся VM).
    /// </summary>
    public class PlanItemViewModel : INotifyPropertyChanged
    {
        private bool _isSelected;
        private bool _isHovered;

        public RoomRow Row { get; }
        public Polygon2D Poly { get; }
        public PointCollection Points { get; }

        public Brush Fill { get; }
        public Brush Stroke { get; }

        public bool IsSelected
        {
            get => _isSelected;
            set { if (_isSelected == value) return; _isSelected = value; OnPropertyChanged(nameof(IsSelected)); OnPropertyChanged(nameof(StrokeThickness)); }
        }

        public bool IsHovered
        {
            get => _isHovered;
            set { if (_isHovered == value) return; _isHovered = value; OnPropertyChanged(nameof(IsHovered)); OnPropertyChanged(nameof(StrokeThickness)); }
        }

        public double StrokeThickness => IsHovered ? 3 : (IsSelected ? 2.5 : 1.2);

        public ICommand SelectCommand { get; }
        public ICommand OpenDetailCommand { get; }

        public PlanItemViewModel(
            RoomRow row,
            Polygon2D polyMm,
            PointCollection points,
            Brush fill,
            Brush stroke,
            ICommand selectCommand,
            ICommand openDetailCommand)
        {
            Row = row;
            Poly = polyMm;
            Points = points;
            Fill = fill;
            Stroke = stroke;
            SelectCommand = selectCommand;
            OpenDetailCommand = openDetailCommand;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
