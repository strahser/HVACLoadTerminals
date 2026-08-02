# Unit Test UT-10: T3.3.3 RevitIndividualPlacementCommand

- Date: 2026-08-02T12:27:00Z
- Target: src/Revit/Commands/RevitIndividualPlacementCommand.cs (CREATE)
- Test: isolated static verification of the command source (Revit-dependent code
  cannot execute outside a running Revit session; per the project pattern for
  Revit files (UT-4/5/6/9) the primary test is MSBuild compilation + a static
  behavior assertion script)
- Result: PASS (see evidence below)

## Deliverable

- `public class RevitIndividualPlacementCommand : IExternalCommand`
  (namespace HVACLoadTerminals.Revit.Commands, `[Transaction(TransactionMode.Manual)]`)
- Execute flow:
  1. Selection: `uiDoc.Selection.GetElementIds()` filtered with
     `.OfType<SpatialElement>()` (covers Room and Space)
  2. Guard: no selection -> TaskDialog "Select at least one room or space" -> Cancelled
  3. Per selected element: `RevitRoomGeometryProvider.GetRoomById(element.Id.ToString())`
     (provider currently resolves MEP Spaces; architectural Rooms are skipped and
     reported). Guard: nothing extractable -> TaskDialog -> Cancelled
  4. Family catalog: `new RevitFamilyCatalogProvider(doc).GetAllDevices()`;
     guard: empty catalog -> TaskDialog "No terminal families found" -> Cancelled
  5. Per room: `service.CalculatePlacement(new RoomPlacementRequest(room), devices)`
     -> defaults to PlacementOptions.Default; placements + warnings collected.
     Guard: no placements computed -> TaskDialog with warnings -> Cancelled
  6. ONE preview for all rooms: `RevitPlacementPreviewService(uiDoc)
     .PreviewAndConfirm(allPlacements, "Individual Placement Preview")`;
     Yes -> commits devices (single tx), No -> rollback (nothing stays)
  7. try/catch -> message = ex.Message; return Result.Failed
- No touch of the .addin file or ribbon (owned by T3.3.4)

## Test evidence

Static assertion script (PowerShell 5.1, source-level checks):

```
PASS: FileExists
PASS: Namespace
PASS: ClassName
PASS: Attribute
PASS: ExecuteSignature
PASS: Selection_GetElementIds
PASS: Selection_SpatialFilter
PASS: NoSelection_Guard
PASS: ExtractViaProvider
PASS: FamilyCatalog
PASS: NoFamilies_Guard
PASS: PerRoom_Calculate
PASS: DefaultOptions
PASS: NoPlacements_Guard
PASS: SinglePreview
PASS: Commit_Cancel_Result
PASS: TryCatch_Failed
PASS: NoAddinTouch
PASS: NoRibbonTouch
RESULT: ALL TESTS PASSED
```

## Build evidence

- `MSBuild src\Revit\HVACLoadTerminals.Revit.csproj /t:Build /p:Configuration=Debug` -> EXITCODE=0
- `MSBuild HVACLoadTerminals.sln /t:Build /p:Configuration=Debug` -> EXITCODE=0
  (Core + Infrastructure + App + Revit, zero regressions)

## Notes

- Runtime behavior (selection extraction, preview dialog, rollback) is
  deferred to manual verification in M3/M4 (requires Revit 2024 + test model).
- Matches the mass command (T3.3.2) provider usage: both go through
  RevitRoomGeometryProvider, which currently supports MEP Spaces only.
