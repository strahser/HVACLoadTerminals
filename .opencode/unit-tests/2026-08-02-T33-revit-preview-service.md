# Unit Test UT-5: T3.3.1 RevitPlacementPreviewService + PlaceDevicesInTransaction

- Date: 2026-08-02T12:17:30Z
- Target: src/Revit/Services/RevitPlacementPreviewService.cs (CREATE), src/Revit/Services/RevitDevicePlacer.cs (MODIFY)
- Test: compile-time verification against the real Revit 2024 RevitAPI.dll
  (native API — cannot be executed outside a live Revit session)
- Result: PASS (MSBuild Revit project build EXITCODE=0; full solution build EXITCODE=0, zero regressions)
- Runtime behavior: deferred to M4 (S4.2.3 placement tests, S4.2.4 preview rollback test)

## Changes (per T3.3 spec)

1. NEW FILE RevitPlacementPreviewService.cs (namespace HVACLoadTerminals.Revit.Services):
   - ctor `RevitPlacementPreviewService(UIDocument uiDoc)` — stores _uiDoc/_doc (null-guarded).
   - `public bool PreviewAndConfirm(IReadOnlyList<DevicePlacement> placements, string caption = "Terminal Placement Preview")`:
     - null/empty placements -> return false.
     - Owns a single `Transaction(_doc, "Preview Terminal Placement")` (tx.Start()).
     - Creates `RevitDevicePlacer(_uiDoc)` and calls `CreatePreviewMarkers(placements, tx)`.
     - Modal `System.Windows.MessageBox.Show(YesNo, Question)`:
       - Yes -> `placer.PlaceDevicesInTransaction(placements, tx); tx.Commit(); return true;`
       - No  -> `tx.RollBack(); return false;`
     - catch(Exception) -> `tx.RollBack()` + error MessageBox -> return false.
2. MODIFIED RevitDevicePlacer.cs:
   - NEW `public void PlaceDevicesInTransaction(IReadOnlyList<DevicePlacement> placements, Transaction tx)`
     — holds the previous PlaceDevices body (LoadSymbols + instance creation + rotation/airflow/comments)
     WITHOUT starting its own transaction. Guards: null tx -> ArgumentNullException;
     tx.GetStatus() != TransactionStatus.Started -> InvalidOperationException (mirrors CreatePreviewMarkers).
   - `PlaceDevices(...)` refactored to `using var tx = new Transaction(...); tx.Start();
     PlaceDevicesInTransaction(placements, tx); tx.Commit();` — public behavior unchanged.

## API verification (reflection on C:\Program Files\Autodesk\Revit 2024\RevitAPI.dll / RevitAPIUI.dll)

- Transaction.GetStatus() / TransactionStatus.Started — EXISTS (already verified UT-4)
- Transaction.RollBack() / Commit() — EXISTS
- UIDocument.Document — EXISTS (RevitAPIUI)
- System.Windows.MessageBox (PresentationFramework, referenced in Revit.csproj) — AVAILABLE

## Build evidence

- `MSBuild src\Revit\HVACLoadTerminals.Revit.csproj /t:Build /p:Configuration=Debug` -> EXITCODE=0
  (HVACLoadTerminals.Revit.dll produced)
- `MSBuild HVACLoadTerminals.sln /t:Build /p:Configuration=Debug` -> EXITCODE=0
  (Core + Infrastructure + App + Revit, zero regressions)

## Notes

- No nested transaction: preview markers AND real devices share the caller-owned transaction;
  cancel/error rolls everything back (S4.2.4 covers the runtime rollback assertion).
- IDevicePlacer interface unchanged; PlaceDevicesInTransaction is additive.
