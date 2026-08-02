# Unit Test UT-10: S1.6.3 Extended placement integration tests

- Date: 2026-08-02T12:28:25Z
- Target: src/Core.Tests/PlacementServiceTests.cs (MODIFY — append 4 [Fact] methods; +`using System;`)
- Test: full `dotnet test` run of HVACLoadTerminals.Core.Tests
- Result: PASS — passed 33, failed 0, skipped 0, total 33 (420 ms)

## Deliverable

Appended 4 xUnit facts closing the Reviewer-flagged S1.6.3 gaps
(L-room placement, placement-level Bottom/Right coordinate resolution,
rotation correctness):

1. `LRoom_ByCalculation_AllInsideAndCountCorrect` — CCW L-polygon
   (0,0)(12,0)(12,8)(8,8)(8,4)(0,4), Supply 1200 m3/h → `Placements.Count == 4`
   (ceil(1200/340)), every position strictly inside via
   `Boundary.ContainsPoint` (StartOffsetMm=100 keeps end devices off the
   boundary ray-cast edge at x=0), `IsOptimal`, no warning.
2. `CoordinateSystem_Bottom_PlacesAlongBottomEdge` — Rect(0,0,12,-8),
   ByCount=2, WallOffsetMm=500, CoordinateSystem=Bottom → every placement
   `EdgeIndex` equals the max-average-Y edge (service semantics, confirmed by
   RoomGeometryAnalyzerTests), `WallSide == Bottom`, perpendicular distance
   from the edge line (InwardNormal dot) == 500/304.8 ft.
3. `CoordinateSystem_Right_PlacesAlongRightEdge` — same rect,
   CoordinateSystem=Right → `EdgeIndex` == max-average-X edge,
   `WallSide == Right`, `Position.X` within 12 - offset ± 0.1 (offset inward
   from the x=12 wall).
4. `Rotation_MatchesInwardNormal` — CoordinateSystem=Bottom →
   `Rotation == Atan2(InwardNormal.Y, InwardNormal.X)` exactly (precision 6),
   and rotation in degrees in (-91, -89): bottom edge inward normal (0,-1)
   ⇒ -π/2 rad = -90° (device faces INTO the room). Rotation is RADIANS.

## Key API facts discovered (matched exactly)

- `DevicePlacement.Rotation` is in **radians** (`Math.Atan2(normal.Y, normal.X)`),
  NOT degrees — asserted in radians with a degrees sanity check.
- `SelectPrimaryEdge` semantics: Bottom = edge with LARGEST avg Y,
  Right = LARGEST avg X, Top = SMALLEST avg Y, Left = SMALLEST avg X.
- Position = `Edge.Start + Direction*dist + InwardNormal*MmToUnits(offset)`.

## Iteration history (test-driven)

- 1st run: 32/33 — `CoordinateSystem_Right_PlacesAlongRightEdge` FAILED
  because options omitted `CoordinateSystem` (defaulted to Auto → longest
  edge chosen, EdgeIndex 0). Bottom/Rotation tests passed only by geometry
  coincidence (longest edge == bottom edge in Rect(0,0,12,-8)).
- Fix: set `CoordinateSystem = Bottom / Right` explicitly in all three
  coordinate tests so they genuinely exercise the coordinate-system branch.
- 2nd run: 33/33 PASS.

## Build evidence

- `dotnet test src\Core.Tests\HVACLoadTerminals.Core.Tests.csproj --nologo -v m`
  -> passed 33, failed 0 (net48, 420 ms). Existing 29 tests untouched and green.

## Notes

- Existing tests left intact (only a `using System;` line added).
- No production code changed; Core.dll rebuilt identically from existing source.
