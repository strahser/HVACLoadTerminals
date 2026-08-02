# Unit Test UT-6: T3.1 RevitFamilyCatalogProvider

- Date: 2026-08-02T12:20:00Z
- Target: src/Revit/Services/RevitFamilyCatalogProvider.cs (CREATE)
- Test: compile-time verification against the real Revit 2024 RevitAPI.dll
  (native API — cannot be executed outside a live Revit session)
- Result: PASS (MSBuild Revit project build EXITCODE=0; full solution build EXITCODE=0, zero regressions)
- Runtime behavior: deferred to M4 (Revit test runner, S4.2.x catalog tests)
- Related issue: SYNC-3 (build-blocking errors in this file, now resolved)

## Changes (per T3.1 spec + SYNC-3 fixes)

1. NEW FILE RevitFamilyCatalogProvider.cs (namespace HVACLoadTerminals.Revit.Services):
   - `public class RevitFamilyCatalogProvider : ITerminalCatalogRepository`
   - ctor `RevitFamilyCatalogProvider(Document doc)` — `_doc ?? throw new ArgumentNullException`, lazy catalog via `_cachedDevices` + `CollectDevices()`.
   - `CollectDevices()`: FilteredElementCollector OfClass(FamilySymbol).WhereElementIsElementType();
     per-symbol try/catch; skip null Family; category mapping; reads flow (m3/h), cooling (W),
     width/height (mm) via parameter candidates; builds TerminalDevice with ctor args matched to
     TerminalDevice.cs (id, familyName, typeName, manufacturer, maxFlowRate, flowParameterName,
     systemType, coolingCapacityW, widthMm, heightMm).
   - `GetAllDevices()` / `GetDevicesBySystemType(HVACSystemType)` / `GetDeviceById(string)`
     implement ITerminalCatalogRepository exactly.
   - Candidate arrays FlowCandidates / CoolingCandidates / WidthCandidates / HeightCandidates as spec.
   - `ReadDoubleParam(Element, string[], ForgeTypeId, out string)`: LookupParameter loop,
     HasValue guard, try/catch around UnitUtils.ConvertFromInternalUnits, fallback ("Air Flow", 0).
   - `IsExhaustName` / `IsFanCoilName`: OrdinalIgnoreCase substring checks (RU + EN keywords).
2. SYNC-3 fixes applied:
   - Removed invalid `OST_AirTerminal` branch (no such Revit 2024 BuiltInCategory member; verified
     by reflection on RevitAPI.dll — the "Air Terminals" category is OST_DuctTerminal, already handled).
   - `fam.Manufacturer` replaced with `ReadStringParam(symbol, "Manufacturer")` (Family/FamilySymbol
     have no Manufacturer property in Revit API; verified by reflection).
   - `fam.Category.Id.IntegerValue` replaced with `fam.Category.Id.Value` (IntegerValue deprecated in
     Revit 2024 — CS0618 eliminated).

## API verification (reflection on C:\Program Files\Autodesk\Revit 2024\RevitAPI.dll)

- BuiltInCategory members matching 'Air|Terminal': OST_DuctTerminal, OST_DuctTerminalTags,
  OST_gbXML_OpeningAir, OST_gbXML_SurfaceAir, OST_MEPAnalyticalAirLoop — NO OST_AirTerminal(s).
- Family / FamilySymbol properties: NO Manufacturer property on either type.
- ElementId.Value exists, type System.Int64 (castable to BuiltInCategory).
- BuiltInParameter.ALL_MODEL_MANUFACTURER exists (used via LookupParameter("Manufacturer") on symbol).

## Build evidence

- `MSBuild src\Revit\HVACLoadTerminals.Revit.csproj /t:Build /p:Configuration=Debug` -> EXITCODE=0
  (Core + Infrastructure + Revit DLL produced, zero warnings)
- `MSBuild HVACLoadTerminals.sln /t:Build /p:Configuration=Debug` -> EXITCODE=0
  (Core + Infrastructure + App + Revit, zero regressions)
