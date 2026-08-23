# UT — WebView2PreviewWindow (WebView2 HTML<->Revit data exchange) — 2026-08-03

Scope: task_aff63a18 — Implement WebView2PreviewWindow (WebView2 host window + JS bridge).

## Files verified
- `src/Revit/Visualization/WebView2PreviewWindow.xaml` (CREATE)
- `src/Revit/Visualization/WebView2PreviewWindow.xaml.cs` (CREATE)
- `src/Infrastructure/Visualization/HtmlSceneExporter.cs` (MODIFY — JS bridge `window.chrome.webview`)
- `src/Revit/Commands/RevitHtmlPlacementCommand.cs` (MODIFY — WebView2 window + browser fallback)
- `src/Revit/HVACLoadTerminals.Revit.csproj` (MODIFY — `UseWPF=true`, `Microsoft.Web.WebView2 1.0.4129.50`, WebView2Loader.dll copy)

## Verification evidence
- Full solution MSBuild (`HVACLoadTerminals.sln`, Debug): **EXITCODE=0** — all 4 projects
  (Core, Infrastructure, Revit, App) compiled. WebView2PreviewWindow -> HVACLoadTerminals.Revit.dll OK.
- Build emitted 0 errors. Warnings are pre-existing (CS8604/CS8602 in unrelated command files
  ExportRoomDataCommand/PlaceTerminalsCommand/ReviewPlacementCommand). No warnings in new WebView2 files.
- dotnet test `HVACLoadTerminals.Core.Tests` (net48): **33 passed, 0 failed** (regression suite).
- Deployment: addins dir `C:\ProgramData\Autodesk\Revit\Addins\2024\HVACLoadTerminals` contains
  `HVACLoadTerminals.Revit.dll`, `WebView2Loader.dll` (x64), `Microsoft.Web.WebView2.{Core,WinForms,Wpf}.dll`,
  plus `x64`/`x86` native dirs.

## Protocol (C#<->JS) consistency check
- Host -> Page: `{"type":"scene","payload":<JObject>}` (SendScene via `PostWebMessageAsString`).
  JS: `onHostMessage` parses and calls `applyScene(d.payload)` when `d.type === 'scene'`.
- Page -> Host: `{type:'apply'}` | `{type:'cancel'}` | `{type:'recompute'}`.
  C# `WebMessage{Type,Options}` deserializes; switch handles all three. Match confirmed.

## Quality findings
- `CoreWebView2Environment.CreateAsync(userDataFolder: ...)` with `Directory.CreateDirectory`
  (mandatory userDataFolder) before `EnsureCoreWebView2Async`.
- `WebMessageReceived`/`NavigationCompleted` work marshaled via `Dispatcher.Invoke` before any
  Revit-touching work.
- `WebView.CoreWebView2` null-guarded on every use; `TryGetWebMessageAsString` in try/catch.
- Graceful degradation: if WebView2 init fails, WPF Apply/Cancel fall back to
  `_applied/DialogResult`; `RevitHtmlPlacementCommand` additionally falls back to the system browser
  when window construction throws.
- Cleanup: `Closed` unsubscribes both events and `Dispose()`s the control.
- Fire-and-forget `_ = InitializeWebViewAsync()` fully try/catch'd (no unobserved exception).
- `UseWPF=true` (XAML compile); wv2 assembly namespace `Microsoft.Web.WebView2.Wpf` correct.
- Recompute lambda calls pure-Core `TerminalPlacementService.CalculateAllPlacements` (no Revit API
  touch on Dispatcher thread) — safe.

Result: PASS (tests, quality, integration)