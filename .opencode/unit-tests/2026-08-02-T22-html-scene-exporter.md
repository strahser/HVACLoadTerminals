# Unit Test UT-8: T2.2 HtmlSceneExporter

- Date: 2026-08-02T12:24:00Z
- Target: src/Infrastructure/Visualization/HtmlSceneExporter.cs (CREATE, 586 lines)
- Test: isolated smoke test of the pure functions `BuildHtml` / `SaveToFile`
  via PowerShell reflection on the built HVACLoadTerminals.Infrastructure.dll (net48)
- Result: PASS (see evidence below)

## Deliverable

- `public static class HtmlSceneExporter` (namespace HVACLoadTerminals.Infrastructure.Visualization — matches PlacementSceneSerializer)
- `BuildHtml(string title, string sceneJson)` — single self-contained HTML5 document:
  - dark UI, left sidebar (room list + per-system summary table: color chip, count, total flow),
    main area canvas, toolbar with 3D toggle
  - `<script>const SCENE = <sceneJson>;</script>` — raw trusted JSON injected; `</` escaped to `<\/`
    to prevent premature script termination
  - Canvas-2D renderer: bounding-box fit view, wheel zoom centered on cursor, mouse-drag pan,
    double-click reset; room boundary (white stroke + translucent fill), offset polygon
    (dashed amber stroke); placements drawn as rotated rectangles (2ft x 1ft) with system color,
    direction tick along rotated +X, hover tooltip (nearest placement within 5px)
  - room-focus: click room name → bright stroke + show only that room's placements
  - optional 3D (Three.js r128 from CDN, 3s load timeout → alert on failure):
    line-loop rooms, colored boxes rotated around Z, orbit drag + wheel zoom, Back-to-2D button
  - plain ES5/ES6 JS, no frameworks, no modules
- `SaveToFile(string directory, string title, string sceneJson)` — writes `index.html`
  (UTF-8, no BOM) and returns full path
- HTML title HTML-escaped (System.Net.WebUtility.HtmlEncode); JS title via JsonConvert serialization

## Test evidence

Smoke test (PowerShell 5.1, `[Reflection.Assembly]::LoadFrom` on bin\Debug\net48):

```
DIAG: htmlChars=18543 htmlBytes=18543 fileBytes=18543
PASS: html len=18543 file len=18543 rooms=1
PASS: node --check OK
ALL TESTS PASSED
```

Assertions that passed:
1. HTML contains `const SCENE =`, `<canvas id="cv">`, Three.js CDN URL, `buildThree` app code
2. Title escaped: `Test &amp; Title` present
3. Embedded scene JSON extracted and re-parsed (ConvertFrom-Json): 1 room, offset polygon 4 pts,
   placement family `Diffuser` — round-trip OK
4. SaveToFile wrote index.html byte-identical to BuildHtml output (UTF8 bytes 18543 == 18543)
5. `node --check` on the embedded app `<script>`: NO JS syntax errors

## Build evidence

- `MSBuild src\Infrastructure\HVACLoadTerminals.Infrastructure.csproj /t:Build /p:Configuration=Debug` -> EXITCODE=0 (Core + Infrastructure DLL)
- `MSBuild HVACLoadTerminals.sln /t:Build /p:Configuration=Debug` -> EXITCODE=0 (Core + Infrastructure + App + Revit, zero regressions)

## Notes

- The `</` → `<\/` escaping is applied only to JSON string values (valid JSON contains `</`
  only inside strings), keeping the injected document safe against early script-close.
- Runtime browser behavior (pan/zoom/tooltip/3D) is deferred to manual verification in M2/M4;
  static JS syntax is machine-validated via node --check.
