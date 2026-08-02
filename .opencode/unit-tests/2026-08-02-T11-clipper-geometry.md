# Unit Test UT-2: T1.1 Clipper2 geometry services

- Date: 2026-08-02T11:48:00Z
- Target: src/Core/Services (ClipperGeometryService, LengthUnitConverter, PolygonOffsetService; modified Core.csproj +Clipper2)
- Test: isolated csc harness referencing built Core.dll (net48) + Clipper2Lib.dll, 33 assertions
- Result: ALL 33 PASSED (exit code 0)
- Verification: `"C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" d:\Projects\HVACLoadTerminals\src\Core\HVACLoadTerminals.Core.csproj /t:Restore /t:Build /p:Configuration=Debug /v:m /nologo` -> success, HVACLoadTerminals.Core.dll produced

## Coverage
1. PolygonArea shoelace: rect 10x8 == 80
2. OffsetInward rect 10x8 by 1 -> 4 vertices, area 48, vertices inside original, wall distance == 1
3. Orientation robustness: reversed (CW) polygon gives same inward result (area 48)
4. OffsetInward oversized (100) -> empty; null/zero/negative -> empty
5. OffsetOutward rect 10x8 by 1 -> bounding box (-1,-1)-(11,9), area ~= 119.14 (JoinType.Round corner arcs), contains original rect
6. CleanPolygon: dedupe consecutive equal, remove duplicated closing vertex, remove collinear -> 4 vertices, area preserved 80; empty/null -> empty
7. IsPointInPolygon: center true, outside false, outside corner false
8. Distance (0,0)-(3,4) == 5
9. LengthUnitConverter: MmPerFoot == 304.8, MmToUnits(304.8) == 1, UnitsToMm(1) == 304.8
10. PolygonOffsetService.OffsetInward delegates to Clipper2 (area 48); DistributePointsOnOffset preserved (4/1 points)
11. OffsetInward L-shaped room (non-convex) shrinks

## Notes
- Clipper2 2.0.0 resolved from local NuGet cache (netstandard2.0 -> net48 OK).
- JoinType.Round produces arc vertices at corners for outward offset; inward offset of rect yields sharp 4-vertex result.
- Orientation handling verified: OffsetInward retries with reversed Path64 when negative delta expands instead of shrinking.
- Isolated test harness deleted after recording (temp: C:\Users\Strakhov\AppData\Local\Temp\opencode\T11*).
