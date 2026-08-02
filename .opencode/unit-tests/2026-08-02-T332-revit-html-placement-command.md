# Unit Test UT-9: T3.3.2 RevitHtmlPlacementCommand

- Date: 2026-08-02T12:26:30Z
- Target: src/Revit/Commands/RevitHtmlPlacementCommand.cs (CREATE, mass placement command)
- Test: compile verification (Revit API code cannot run without a Revit runtime; matches the
  established pattern for Revit files — RevitDevicePlacer UT-4/UT-5, RevitFamilyCatalogProvider UT-6)
- Result: PASS (see evidence below)

## Deliverable

- `public class RevitHtmlPlacementCommand : IExternalCommand` (namespace
  HVACLoadTerminals.Revit.Commands, matches PlaceTerminalsCommand style), `[Transaction(TransactionMode.Manual)]`
- `Execute(ExternalCommandData, ref string message, ElementSet elements)` wrapped in try/catch
  → Result.Failed with `message = ex.Message` on exception
- Flow:
  1. `uiDoc = commandData.Application.ActiveUIDocument` (null guard → Result.Cancelled)
  2. Rooms: `new RevitRoomGeometryProvider(doc).GetAllRooms()` — RoomPolygon list whose
     Systems are already populated by the provider from Space parameters (supply/exhaust, m3/h);
     empty → TaskDialog "No MEP Spaces found" → Result.Cancelled
  3. Catalog: `new RevitFamilyCatalogProvider(doc).GetAllDevices()`; empty → TaskDialog
     "No terminal families found" → Result.Cancelled
  4. Requests: `new RoomPlacementRequest(room)` per room (default PlacementOptions, no
     per-room config exposed by the provider)
  5. `new TerminalPlacementService().CalculateAllPlacements(requests, devices)`; null results
     filtered; all empty → TaskDialog with first 10 distinct room warnings → Result.Cancelled
  6. HTML: `PlacementSceneSerializer.ToJson(results, "Terminal Placement")` →
     `HtmlSceneExporter.SaveToFile(%TEMP%\HVACLoadTerminalsPreview, ...)` (creates dir, writes
     index.html UTF-8 no BOM) → `Process.Start(UseShellExecute = true)` opens default browser
  7. Revit preview: collect ALL placements via `SelectMany(r => r.Placements)` →
     `new RevitPlacementPreviewService(uiDoc).PreviewAndConfirm(allPlacements, "Terminal Placement Preview")`
     → Result.Succeeded on Place, Result.Cancelled on Cancel (rollback keeps model clean)

## Test evidence

```cmd
MSBuild src\Revit\HVACLoadTerminals.Revit.csproj /t:Build /p:Configuration=Debug /v:m /nologo
  HVACLoadTerminals.Core -> ...\Core.dll
  HVACLoadTerminals.Infrastructure -> ...\Infrastructure.dll
  HVACLoadTerminals.Revit -> ...\Revit.dll
REVIT_EXITCODE=0

MSBuild HVACLoadTerminals.sln /t:Build /p:Configuration=Debug /v:m /nologo
  HVACLoadTerminals.Core -> ...\Core.dll
  HVACLoadTerminals.Infrastructure -> ...\Infrastructure.dll
  HVACLoadTerminals.App -> ...\App.exe
  HVACLoadTerminals.Revit -> ...\Revit.dll
SOLUTION_EXITCODE=0
```

Static wiring check (findstr on the created file, all present):
- `class RevitHtmlPlacementCommand : IExternalCommand` (line 24)
- `RevitRoomGeometryProvider(doc).GetAllRooms()` (line 44)
- `RevitFamilyCatalogProvider(doc).GetAllDevices()` (line 53)
- `service.CalculateAllPlacements(requests, devices)` (line 71)
- `PlacementSceneSerializer.ToJson(results, DialogTitle)` (line 95)
- `HtmlSceneExporter.SaveToFile(htmlDir, DialogTitle, sceneJson)` (line 97)
- `preview.PreviewAndConfirm(allPlacements, "Terminal Placement Preview")` (line 104)

## Notes

- ADDIN FILE NOT MODIFIED: src/Revit/HVACLoadTerminals.addin is a `Type="Application"` addin
  (registers only HVACLoadTerminals.Revit.Application). External commands in this project are
  ribbon-wired from Application.cs (PlaceTerminalsCommand/ReviewPlacementCommand/ExportRoomDataCommand
  have no addin command entries either). Ribbon wiring for this command is owned by T3.3.4;
  adding a Type="Command" entry here would duplicate registration. Correctly deferred.
- Runtime behavior (real Space extraction, browser open, transaction commit/rollback) is
  exercised by T4.1/T4.2 Revit in-model tests; static compile + API wiring is machine-verified here.
