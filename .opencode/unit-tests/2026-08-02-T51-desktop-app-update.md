# Unit Test UT-13: T5.1 Desktop App Update

- Date: 2026-08-02T12:43:00Z
- Target:
  - src/App/ViewModels/MainViewModel.cs (MODIFY, 214→310 lines)
  - src/App/MainWindow.xaml (MODIFY, 102→147 lines)
- Test: MSBuild compilation check (App project + full solution) + dotnet test Core.Tests
- Result: PASS (App build EXITCODE=0, full solution EXITCODE=0, 33/33 Core tests pass)

## Deliverables

### MainViewModel.cs changes:
- **Placement Options properties** (INotifyPropertyChanged):
  - `CurrentMode` (PlacementMode enum, default ByCalculation)
  - `WallOffsetMm` (double, default 500)
  - `FixedCount` (int, default 1)
  - `StepCount` (int, default 1)
  - `MaxCount` (int, default 50)
  - `SidePreference` (PlacementSide enum, default Any)
  - `CoordinateSystem` (CoordinateSystem enum, default Auto)
- **Enum array properties** for XAML ComboBox binding:
  - `PlacementModes`, `PlacementSides`, `CoordinateSystems`
- **`ShowHtmlPreviewCommand`** (ICommand):
  - Uses `OpenHtmlPreviewCommand` with `_lastSceneJson` callback
  - Auto-computes placement if not done yet
- **`_lastSceneJson`** caching:
  - Updated after each `CalculatePlacement()` call via `PlacementSceneSerializer.ToJson()`
- **`BuildCurrentOptions()`** — creates PlacementOptions from UI state
- **`BuildRoomRequests()`** — creates RoomPlacementRequest per room with current options
- **`CalculatePlacement()`** refactored:
  - Uses concrete `TerminalPlacementService.CalculateAllPlacements(requests, devices)` for options support
  - Falls back to interface overload if service is not concrete type
  - Plots all rooms or selected room
- **`BuildPlotModel()`** extracted as reusable helper

### MainWindow.xaml changes:
- Added "Show HTML Preview" button in Actions section
- Added "Placement Options" section with:
  - ComboBox for Mode (items: PlacementModes, selected: CurrentMode)
  - TextBox for WallOffsetMm (UpdateSourceTrigger=PropertyChanged)
  - TextBox for FixedCount
  - TextBox for StepCount
  - TextBox for MaxCount
  - ComboBox for SidePreference
  - ComboBox for CoordinateSystem

## Build Evidence

```
MSBuild src\App\HVACLoadTerminals.App.csproj /t:Build /p:Configuration=Debug
  -> EXITCODE=0 (Core + Infrastructure + App)

MSBuild HVACLoadTerminals.sln /t:Build /p:Configuration=Debug
  -> EXITCODE=0 (Core + Infrastructure + App + Revit, zero regressions)

dotnet test Core.Tests 33/33 pass
```

## Notes

- Used existing `RelayCommand` class (same as other commands in the ViewModel)
- Used existing `PlacementOptions` model — no new model classes needed
- Cast `_placementService` to `TerminalPlacementService` to access request-based overload (interface only exposes legacy overload)
- XAML follows existing SectionHeader/ActionButton styles
- All properties use `OnPropertyChanged(nameof(...))` pattern matching existing code
- Reuses `OpenHtmlPreviewCommand` from `src/App/Commands/` — no duplication
