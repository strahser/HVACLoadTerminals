# Unit Test UT-4: T3.2 RevitDevicePlacer enhancement

- Date: 2026-08-02T12:05:00Z
- Target: src/Revit/Services/RevitDevicePlacer.cs (MODIFY)
- Test: compile-time verification against the real Revit 2024 RevitAPI.dll
  (native API — cannot be executed outside a live Revit session)
- Result: PASS (MSBuild Revit project build EXITCODE=0; full solution build EXITCODE=0)
- Runtime behavior: deferred to M4 (Revit test runner, S4.2.3 placement tests)

## Changes (per T3.2 spec)

1. LoadSymbols: match by `TerminalDevice.TypeName` first (FamilySymbol.Name,
   case-insensitive), fall back to `FamilyName` (s.Family.Name). Inactive symbols
   activated. Null guards on s.Name / s.Family.
2. Level fallback: `GetLevelForRoom(roomId) ?? GetFirstLevel()` — new GetFirstLevel()
   via FilteredElementCollector.OfClass(typeof(Level)).FirstOrDefault().
3. Rotation: after instance creation, `(instance.Location as LocationPoint).Rotate`
   around the vertical axis through the placement point with placement.Rotation
   (radians, CCW, 0 = front faces +X). Skipped when |rotation| <= 1e-9.
4. Airflow param: if FlowParameterName not empty — LookupParameter, if not read-only
   convert m3/h via `UnitUtils.ConvertToInternalUnits(..., UnitTypeId.CubicMetersPerHour)`
   and param.Set(). Wrapped in try/catch (type-only or non-writable params skipped).
5. Comments: existing SystemName write preserved (ApplyComments helper).
6. Instance creation failure → try/catch → skip, continue.
7. NEW public method `CreatePreviewMarkers(IReadOnlyList<DevicePlacement>, Transaction)`
   — caller owns the transaction (must be Started; GetStatus()==Started enforced).
   Creates an ellipse circle (Ellipse.CreateCurve 7-arg, r=0.3) + label line
   (Line.CreateBound) at each position on a single SketchPlane at origin.

## API verification (reflection on C:\Program Files\Autodesk\Revit 2024\RevitAPI.dll)

- Ellipse.CreateCurve(XYZ, Double, Double, XYZ, XYZ, Double, Double) — EXISTS
- LocationPoint.Rotate(Line, Double) — EXISTS
- UnitUtils.ConvertToInternalUnits(Double, ForgeTypeId) — EXISTS
- UnitTypeId.CubicMetersPerHour / UnitTypeId.Watts — EXISTS
- TransactionStatus.Started / Transaction.GetStatus() — EXISTS
- Element.Location → LocationPoint (FamilyInstance : Instance) — EXISTS

## Build evidence

- `MSBuild src\Revit\HVACLoadTerminals.Revit.csproj /t:Build /p:Configuration=Debug` -> EXITCODE=0
  (HVACLoadTerminals.Revit.dll produced)
- `MSBuild HVACLoadTerminals.sln /t:Build /p:Configuration=Debug` -> EXITCODE=0
  (Core + Infrastructure + App + Revit, zero regressions)

## Notes

- IDevicePlacer interface unchanged (CreatePreviewMarkers is an additive public
  method; T3.3 command will own the preview transaction).
- No isolated runtime harness: Document/Transaction/LocationPoint are native Revit
  classes and cannot be constructed outside a Revit session; runtime coverage is
  assigned to M4 (S4.2.3 placement tests, S4.2.4 preview rollback test).
