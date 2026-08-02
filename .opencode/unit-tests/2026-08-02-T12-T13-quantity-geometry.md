# Unit Test UT-3: T1.2 Room geometry analysis + T1.3 Quantity modes

- Date: 2026-08-02T12:05:00Z
- Target:
  - src/Core/Services/RoomGeometryAnalyzer.cs (CREATE)
  - src/Core/Services/QuantityCalculator.cs (CREATE)
  - src/Core/Services/TerminalSelectionService.cs (MODIFY: +SelectDevicesForQuantity, +PickBestDevice)
- Test: isolated csc harness referencing built Core.dll (net48), 43 assertions
- Result: ALL 43 PASSED (exit code 0)
- Verification:
  - `"C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" d:\Projects\HVACLoadTerminals\src\Core\HVACLoadTerminals.Core.csproj /t:Build /p:Configuration=Debug /v:m /nologo` -> success
  - Full solution build (`HVACLoadTerminals.sln`, Debug) -> EXITCODE=0 (Core/Infrastructure/App/Revit)

## Coverage

### RoomGeometryAnalyzer (T1.2)
1. GetEdges: rect 10x8 -> 4 edges, lengths 10/8/10/8, unit directions, midpoints correct
2. InwardNormals: for every edge, mid + 0.1*normal is inside the polygon (rect)
3. SelectEdgesByPreference: LongSide -> 2 edges within 5% of longest (10); ShortSide -> 2 edges within 5% of shortest (8); Any -> all 4
4. SelectPrimaryEdge: Bottom -> max avg Y edge; Right -> max avg X; Top -> min avg Y; Left -> min avg X; Auto+LongSide/Any -> longest; Auto+ShortSide -> shortest
5. ResolveCoordinateSystem: top/right/bottom/left edges of rect classified Top/Right/Bottom/Left
6. Non-convex L-room (6 verts) -> 6 edges; LongSide -> unique longest edge (12)

### QuantityCalculator (T1.3)
7. ByCalculation: ceil(300/100)=3; ceil(250/100)=3; flow 0 -> 0; deviceMaxFlow 0 -> 0 (div-by-zero guard); capped at maxCount (1000/100, max 5 -> 5)
8. ByCount: fixedCount 4 -> 4; fixedCount 0 -> min 1; fixedCount 8 with maxCount 5 -> 5
9. ByStep: flow==capacity -> 1; 101/100 step1 -> 2; 250/100 step2 -> 3; never reached within maxCount 5 -> 5
10. TotalCapacity(100,3) == 300

### TerminalSelectionService (T1.3)
11. SelectDevicesForQuantity: filters by SystemType + MaxFlowRate>0 (Exhaust device excluded for Supply)
12. ByCount: returns exactly count devices of largest-flow Supply device
13. ByCalculation/ByStep: returns count devices of best device (highest flow)
14. Empty catalog -> empty; count 0 -> empty
15. PickBestDevice: highest-flow device; empty list -> null

## Notes
- EdgeInfo is a public class with public fields per spec (Index/Start/End/Length/Direction/InwardNormal/MidPoint).
- ComputeInwardNormal returns Point2D (the spec text "double ComputeInwardNormal" was a typo — a normal is a vector); it is the left-hand normal of the edge direction, flipped toward the polygon centroid (vertex average).
- L-room LongSide returns exactly 1 edge (longest=12 is unique, threshold 11.4) — matches spec "within 5% of longest".
- Isolated test harness deleted after recording (temp: C:\Users\Strakhov\AppData\Local\Temp\opencode\T12T13*).
