---
name: webview2-revit-data-exchange
description: Use when implementing or debugging HTML ↔ Revit communication through WebView2 (Microsoft.Web.WebView2) — postMessage bridge between a WPF-hosted WebView2 control and the Revit add-in, or when working with HtmlSceneExporter / WebView2PreviewWindow / HtmlPreviewServer in this repo. Covers the protocol (scene/apply/cancel/recompute), required CoreWebView2Environment userDataFolder, and Revit 2024 deployment (WebView2Loader.dll).
---

# WebView2 ↔ Revit Data Exchange

Reference article (RU): https://mostbim.vercel.app/blog/web-data-exchange/
(cached: `.opencode/docs/mostbim_vercel_app_blog_web-data-exchange_.md`, local copy of the
author's sample: https://github.com/SergeyNefyodov/revit-blog-fe and
https://github.com/SergeyNefyodov/WPFApplication)

## Core idea

Embed a Chromium/Edge engine (WebView2) inside a WPF window hosted **in the Revit
process** and exchange JSON messages both ways:

- Host → Page: `CoreWebView2.PostWebMessageAsString(json)`
- Page → Host: `window.chrome.webview.postMessage(JSON.stringify(obj))`,
  host subscribes to `CoreWebView2.WebMessageReceived`

No HTTP server is needed for the message bridge (it is a direct in-process pipe).
The existing `HtmlPreviewServer` (HttpListener) remains for the standalone WPF app /
plain-browser fallback.

## NuGet + project setup (Revit 2024, net48, SDK-style)

```xml
<PropertyGroup>
  <UseWPF>true</UseWPF>            <!-- XAML compile support in the Revit project -->
</PropertyGroup>
<ItemGroup>
  <PackageReference Include="Microsoft.Web.WebView2" Version="1.0.4129.50" />
</ItemGroup>
```

`UseWPF=true` auto-adds PresentationCore/Framework, WindowsBase, System.Xaml — do NOT
also list them as manual `<Reference>` items (duplicate assembly references).

### Deployment — WebView2Loader.dll (critical for Revit add-ins)

The NuGet package drops `WebView2Loader.dll` under
`runtimes\win-x64\native\` / `runtimes\win-x86\native\` (NOT the output root).
Revit loads the add-in from its own folder, so the loader must be copied next to the
plugin DLLs. Add to the post-build copy target:

```xml
<ItemGroup>
  <RevitAddinWebView2Loader Include="$(TargetDir)runtimes\win-x64\native\WebView2Loader.dll"
                            Condition="Exists('$(TargetDir)runtimes\win-x64\native\WebView2Loader.dll')" />
</ItemGroup>
<!-- then <Copy SourceFiles="@(RevitAddinWebView2Loader)" DestinationFolder="$(RevitAddinsDir)\HVACLoadTerminals" .../> -->
```

Without it the window shows "WebView2 error: ... loader ..." and the preview fails.

## Initialization (mandatory: userDataFolder)

`CoreWebView2Environment.CreateAsync` **requires an existing userData folder**,
otherwise the app crashes on startup:

```csharp
var userDataFolder = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "HVACLoadTerminals", "WebView2");
Directory.CreateDirectory(userDataFolder);
var env = await CoreWebView2Environment.CreateAsync(userDataFolder: userDataFolder);
await WebView.EnsureCoreWebView2Async(env);
```

## Message protocol (this repo)

| Direction | JSON | Meaning |
|-----------|------|---------|
| Host → Page | `{"type":"scene","payload":<sceneJson>}` | (Re)send the placement scene (sent on `NavigationCompleted` with `e.IsSuccess`) |
| Page → Host | `{"type":"apply"}` | User confirmed placement → close with `DialogResult=true`, set `IsApplied` |
| Page → Host | `{"type":"cancel"}` | User cancelled → `DialogResult=false` |
| Page → Host | `{"type":"recompute","options":{...}}` | Recompute placements; host replies with a new `scene` message |

C# DTO: `class WebMessage { public string? Type { get; set; } public JObject? Options { get; set; } }`
received via `e.TryGetWebMessageAsString()` then `JsonConvert.DeserializeObject<WebMessage>`.

JS side (in `HtmlSceneExporter.TailTemplate`):

```js
var wv = (window.chrome && window.chrome.webview) ? window.chrome.webview : null;
function postToHost(obj) { if (wv) wv.postMessage(JSON.stringify(obj)); }
function onHostMessage(ev) {
  var d = (typeof ev.data === 'string') ? JSON.parse(ev.data) : ev.data;
  if (d && d.type === 'scene' && d.payload) applyScene(d.payload);
}
if (wv) { wv.addEventListener('message', onHostMessage); }
```

Buttons (Apply/Cancel/Recompute) are created dynamically and only shown when `wv` is
present; in a plain browser the page still renders (no buttons).

## Threading rules

`WebMessageReceived` and `NavigationCompleted` may fire on the WebView2 UI thread.
Wrap all Revit API work (recompute → `TerminalPlacementService`, `Document` access) in
`Dispatcher.Invoke(...)` before touching Revit objects.

## Files in this repo

- `src/Revit/Visualization/WebView2PreviewWindow.xaml(.cs)` — the WebView2 host window
  (env init, message bridge, Apply/Cancel buttons, cleanup on close).
- `src/Infrastructure/Visualization/HtmlSceneExporter.cs` — builds the HTML scene and
  injects the JS bridge (`window.chrome.webview`).
- `src/Revit/Commands/RevitHtmlPlacementCommand.cs` — opens the WebView2 window with a
  `recomputeSceneJson` lambda (re-runs `CalculateAllPlacements`); falls back to the
  system browser if WebView2 init throws.
- `src/Infrastructure/Visualization/HtmlPreviewServer.cs` — HttpListener fallback host
  (used by the standalone WPF app / browser mode).

## Gotchas

- `"</script>"` inside the JS string terminates the HTML document — write `<\/script>`.
- `NavigationCompleted` fires for every navigation; guard with `e.IsSuccess` and
  null-check `WebView.CoreWebView2`.
- Always `try/catch` init and message handling and log via
  `HvacLogger.LogException` (log path: `%LocalAppData%\HVACLoadTerminals\logs\`).
- `WebView2Loader.dll` architecture must match Revit (x64).
