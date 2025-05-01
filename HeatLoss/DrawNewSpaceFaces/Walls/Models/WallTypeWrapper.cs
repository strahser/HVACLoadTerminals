using Autodesk.Revit.DB;
using HVACLoadTerminals.Utils;

namespace HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls.Models;

public class WallTypeWrapper(WallType type) : ViewModelBase
{
    public WallType Type { get; } = type;
    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => SetField(ref _isSelected, value);
    }
}