// WallCreationContext.cs

using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls.Core;

public class WallCreationContext
{
    public Document HvacDocument { get; set; } // Добавляем документ
    public string NorthDirection { get; set; }
    public Level GroundLevel { get; set; }
    public HashSet<ElementId> Filter { get; set; }
}