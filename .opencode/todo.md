# Mission: HVAC Load Terminals — placement engine + HTML UI + Revit integration + Revit tests

## M1: Core placement engine (pure C#, Revit-free) | status: completed
### T1.1: Clipper2 geometry services | agent:Worker | status: completed
- [x] S1.1.1: Add Clipper2 NuGet (2.0.0) to Core; port ClipperGeometryService (offset inward/outward, union, difference, clean polygon) from HeatLossRevit2 pattern | size:M
- [x] S1.1.2: Reimplement PolygonOffsetService on Clipper2 (OffsetInward with mm, DistributePointsOnOffset preserved) | size:M
### T1.2: Room geometry analysis | agent:Worker | status: completed
- [x] S1.2.1: RoomGeometryAnalyzer: edge classification (length, orientation), long/short side detection, inward normals, edge ordering | size:M | verified 2026-08-02 (GetEdges/SelectPrimaryEdge/ResolveCoordinateSystem, build OK)
- [x] S1.2.2: EdgeSelector: choose placement edges by SidePreference (LongSide/ShortSide) + CoordinateSystem preference (Bottom/Right/Top/Left/Auto) | size:M | verified 2026-08-02 (build OK)
### T1.3: Quantity modes | agent:Worker | status: completed
- [x] S1.3.1: QuantityCalculator: ByCalculation (ceil load/capacity), ByCount (exact n), ByStep (min count + step, max cap) | size:M | verified 2026-08-02 (CalculateCount + 3 modes, tests 29/29)
- [x] S1.3.2: TerminalSelectionService refactor to support cooling capacity + flow-based selection | size:S | verified 2026-08-02 (SelectDevicesForQuantity/PickBestDevice, tests 29/29)
### T1.4: Models extension | agent:Worker | status: completed
- [x] S1.4.1: PlacementOptions (mode, count, step, offset, side, coord system, spacing), PlacementMode enum, PlacementSide enum, CoordinateSystem enum | size:M | verified 2026-08-02 (14 model files present, build OK)
- [x] S1.4.2: RoomPlacementConfig (per-room allowed families), TerminalDevice + CoolingCapacity/W/H, HVACSystemType + Cooling, DevicePlacement + rotation/edge/wall side | size:M | verified 2026-08-02 (build OK)
### T1.5: Placement service orchestration | agent:Worker | depends:T1.1,T1.2,T1.3,T1.4 | status: completed
- [x] S1.5.1: TerminalPlacementService: room config → edge selection → local coordinate system → offset → distribute → world transform with rotation | size:L | verified 2026-08-02 (CalculatePlacement/CalculateAllPlacements, tests 29/29)
- [x] S1.5.2: PlacementResult + warnings (insufficient capacity, count capped, no families) | size:S | verified 2026-08-02 (tests 29/29)
### T1.6: Core.Tests (xUnit net48) | agent:Worker | depends:T1.1..T1.5 | status: completed
- [x] S1.6.1: Geometry tests (offset, area, long/short side, point in polygon) | size:M | verified 2026-08-02 (29/29 pass, dotnet test)
- [x] S1.6.2: Quantity mode tests (calc/count/step) | size:M | verified 2026-08-02 (29/29 pass, dotnet test)
- [x] S1.6.3: Placement integration tests (rect room, L-room, coords Bottom/Right, long/short side, rotation correctness) | size:L | verified 2026-08-02 (dotnet test 33/33 pass, extended tests L-room, Bottom/Right coords, rotation all pass)

## M2: HTML visualization (C# + HTML) | status: completed
### T2.1: Scene serialization | agent:Worker | depends:M1 | status: completed
- [x] S2.1.1: PlacementScene DTOs (rooms, offset polygons, placements with rotation, labels, colors) + Newtonsoft serializer | size:M | verified 2026-08-02 (ToJson/BuildScene/FromJsonScene, build OK)
- [x] S2.1.2: HtmlSceneExporter: JSON → self-contained HTML (Three.js plane via CDN + Canvas2D fallback) | size:M | verified 2026-08-02 (BuildHtml/SaveToFile, smoke test UT-8 PASS)
### T2.2: Preview server bridge | agent:Worker | depends:T2.1 | status: completed
- [x] S2.2.1: HtmlPreviewServer (HttpListener, port auto): GET / (html), GET /api/scene, POST /api/options (recompute) | size:M | verified 2026-08-02 (smoke test UT-9 pass, build EXITCODE=0)
- [x] S2.2.2: IHtmlPreviewHost abstraction (Start/Stop/Recompute/Apply/Cancel) for Revit & App | size:M | verified 2026-08-02 (smoke test UT-9 pass, build EXITCODE=0)
### T2.3: WPF HTML window | agent:Worker | depends:T2.2 | status: completed
- [x] S2.3.1: HtmlPreviewWindow (WPF): WebView2 if available, else system browser + local server | size:M | verified 2026-08-02 (App build EXITCODE=0, WebBrowser used, 3 files created)
- [x] S2.3.2: OpenHtmlPreviewCommand wiring in App MainViewModel | size:S | verified 2026-08-02 (App build EXITCODE=0, solution EXITCODE=0, OpenHtmlPreviewCommand.cs exists with ICommand)

## M3: Revit integration | status: completed
### T3.1: Family catalog auto-collection | agent:Worker | depends:M1
- [x] S3.1.1: RevitFamilyCatalogProvider: collect FamilySymbols (DuctTerminal, AirTerminal, MechanicalEquipment), read flow/cooling params, map to TerminalDevice | size:L
- [x] S3.1.2: Parameter name resolution (supply/exhaust airflow, cooling capacity) configurable | size:M
### T3.2: Device placer enhancement | agent:Worker | status: completed
- [x] S3.2.1: RevitDevicePlacer: family type match, rotation (facing wall), airflow param set, level placement | size:M | verified 2026-08-02 (PlaceDevices + PlaceDevicesInTransaction + CreatePreviewMarkers, Revit build EXITCODE=0, full solution EXITCODE=0)
### T3.3: Preview with transaction cancel | agent:Worker | depends:T3.2
- [x] S3.3.1: RevitPlacementPreviewService: start tx → draw preview markers → modal confirm Place/Cancel → commit/rollback | size:M | verified 2026-08-02 (PreviewAndConfirm + PlaceDevicesInTransaction, Revit build EXITCODE=0)
- [x] S3.3.2: RevitHtmlPlacementCommand (mass): rooms from model, family catalog auto, HTML window, preview-in-revit | size:L | verified 2026-08-02 (114 lines, wiring EXITCODE=0, Core tests 29/29)
- [x] S3.3.3: RevitIndividualPlacementCommand (selected spaces) | size:M | verified 2026-08-02 (Revit build EXITCODE=0, solution EXITCODE=0)
- [x] S3.3.4: Application.cs ribbon buttons (Mass, Individual, RunTests) | size:S | verified 2026-08-02 (Application.cs has RunTests button wired, Revit build EXITCODE=0)

## M4: Tests in Revit | status: completed
### T4.1: Revit test runner | agent:Worker | depends:M3 | status: completed
- [x] S4.1.1: RevitTestRunnerCommand: runs test fixtures in-process, writes results JSON to reports dir | size:M | verified 2026-08-02 (Revit build EXITCODE=0, source set + ribbon RunTests wired, smoke PASS)
- [x] S4.1.2: Minimal assertion/attribute framework or NUnitLite integration | size:M | verified 2026-08-02 (RevitTestAttribute + Assert + RevitTestRunner, build EXITCODE=0, smoke PASS)
### T4.2: Revit integration tests | agent:Worker | depends:T4.1 | status: completed
- [x] S4.2.1: Space extraction tests on TestBuildingHvac_2024.rvt (rooms, loads, polygons) | size:M | verified 2026-08-02 (Revit build EXITCODE=0)
- [x] S4.2.2: Family catalog auto-collection tests (families found, params mapped) | size:M | verified 2026-08-02 (Revit build EXITCODE=0)
- [x] S4.2.3: Placement tests (quantity, positions in polygon, offset distance, rotation) | size:M | verified 2026-08-02 (Revit build EXITCODE=0)
- [x] S4.2.4: Preview transaction rollback test (rollback leaves no elements) | size:S | verified 2026-08-02 (Revit build EXITCODE=0)

## M5: App + docs + final verification | status: completed
### T5.1: Desktop App update | agent:Worker | depends:M1,M2 | status: completed
- [x] S5.1.1: MainViewModel: placement options UI, HTML preview, per-room config | size:L | verified 2026-08-02 (App build EXITCODE=0, solution EXITCODE=0, MainViewModel has CurrentMode/WallOffsetMm/ShowHtmlPreviewCommand/PlacementModes, MainWindow.xaml has Placement Options panel + Show HTML Preview button)
### T5.2: Documentation | agent:Worker | status: completed
- [x] S5.2.1: README update (architecture, usage, Revit install, tests) | size:S | verified 2026-08-02 (394 lines, contains Архитектура/Установка/Revit/Тесты sections)
### T5.3: Full verification | agent:Reviewer | depends:ALL | status: completed
- [x] S5.3.1: Full solution build (Debug) | size:M | verified 2026-08-02 (MSBuild HVACLoadTerminals.sln EXITCODE=0, all 4 projects: Core→Infrastructure→App→Revit)
- [x] S5.3.2: Core.Tests pass (dotnet test) | size:M | verified 2026-08-02 (33 passed, 0 failed, 379ms)
- [x] S5.3.3: Revit project compile + addin file valid | size:M | verified 2026-08-02 (Revit DLL exists, HVACLoadTerminals.addin exists, Revit build EXITCODE=0)
- [x] S5.3.4: Final system verification report | size:S | verified 2026-08-02 (all checks passed, see below)
