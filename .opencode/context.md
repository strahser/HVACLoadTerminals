# Project Context — HVACLoadTerminals

## Environment
- Language: C# (.NET Framework 4.8, LangVersion latest, Nullable enable)
- Revit: Autodesk Revit 2024 (C:\Program Files\Autodesk\Revit 2024\RevitAPI.dll)
- Build: MSBuild 17 (VS 2022 Community) — "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe"
- Test: xUnit 2.5.3 (in NuGet cache), NUnit available in reference
- NuGet cache: %USERPROFILE%\.nuget\packages — has Clipper2 (1.3.0/1.5.4/2.0.0, netstandard2.0), xunit 2.5.3, Newtonsoft.Json 13.0.3
- Solution: HVACLoadTerminals.sln (src/Core, src/Infrastructure, src/App, src/Revit)

## Project Structure (existing)
- src/Core (HVACLoadTerminals.Core.csproj, SDK-style net48): Models (Point2D, Polygon2D, RoomPolygon, HVACSystem, HVACSystemType, TerminalDevice, DevicePlacement, PlacementResult), Services (PolygonOffsetService naive, TerminalSelectionService, TerminalPlacementService), Interfaces (IRoomGeometryProvider, IRoomSystemProvider, ITerminalCatalogRepository, ITerminalPlacementService, IPolygonVisualizer, IDevicePlacer), Exceptions
- src/Infrastructure: Data (JsonRoomDataStore, SQLiteTerminalCatalogRepository), Visualization (OxyPlotVisualizer), Services (DemoRoomDataService)
- src/App: WPF app (MainWindow, MainViewModel, OxyPlot-based)
- src/Revit: Application.cs (ribbon: Place/Review/Export), Commands (PlaceTerminals, ReviewPlacement, ExportRoomData), Services (RevitRoomGeometryProvider, RevitRoomSystemProvider, RevitDevicePlacer)
- Root legacy: old-style HVACLoadTerminals.csproj (Dynamo-based, legacy Views/ViewModels/Utils at root — DO NOT MODIFY unless needed)

## Reference (READ-ONLY COPY SOURCE) d:\Projects\HeatLossRevit2\
- Core/ZoneServices/Utils/ClipperUtils.cs — Clipper2 wrapper (Scale=10000, Union/Difference/Intersection/InflatePaths/OffsetPolygonInward)
- Core/ZoneServices/Utils/GeometryUtils.cs — PolyArea, CleanPolygon, EnsureClosedPolygon, IsPointInPolygon etc.
- Core/ZoneServices/Services/InwardDirectionCalculator.cs, BufferZoneCalculator.cs, ZonePolygonBuilder.cs, BuildingContourBuilder.cs
- Core/ZoneServices/Models/Point2D.cs (with operators +,-,*, Length, Normalize, Dot), ZoneExportModel.cs
- Core.Tests (SDK net48, xunit 2.9.3) — ZoneServicesTests.cs pattern
- Revit tests: NUnit fixtures referencing Revit API (Tests.HeatLossTests) + Base.csproj helpers

## Analog (UX reference only) d:\Projects\PloteNetworksAndSpaces-master\
- Python/Streamlit + Plotly (Polygons/PolygonPlot/*) — visualization UX reference (rooms, terminals, networks)

## Test Models d:\Projects\ТестыОВ\newBuilding\
- TestBuildingHvac_2024.rvt, TestBuildingHvac_2024_native_zero.rvt, HvackFinal.rvt, TestBuildingAR_2024.rvt
- configs/ (AirFlowFamilyModel, CornerRoom, CurtainWall, FacesDirectShapes, OpeningSettings, UnifiedElements, ValveMappings, WallProcessing), reports/

## Mission Requirements (user)
1. HVAC loads per room (ventilation flow + cooling) — exists, extend
2. Equipment families list per room — per-room config + auto-collect families from Revit model
3. Quantity modes: ByCalculation (ceil load/capacity), ByCount (exact), ByStep (from min with step)
4. Placement: wall polygon offset (Clipper2) + optimal conditional coordinate system (Bottom/Right/Top/Left/Auto)
5. Placement side: LongSide / ShortSide preference
6. HTML UI (C# + HTML): Three.js on plane (or Canvas2D); live preview; also Revit preview with transaction cancel
7. Tests directly in Revit (autotest runner command against test model)
8. Clean architecture — Core must stay Revit-free
9. Mass + individual placement by rooms

## Conventions
- Namespaces: HVACLoadTerminals.Core.{Models,Services,Interfaces,Exceptions}, .Infrastructure.{Data,Services,Visualization}, .Revit.{Commands,Services}, .App.{ViewModels,Views}
- Core = pure C# (no Revit/WPF deps), Infrastructure = implementations, Revit = adapters
- Coordinate system: XY feet (Revit internal units), Point2D double
