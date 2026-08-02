# Unit Test UT-9: T2.2 Preview Server Bridge (HtmlPreviewServer + IHtmlPreviewHost)

- Date: 2026-08-02T12:31:00Z
- Target:
  - src/Infrastructure/Visualization/IHtmlPreviewHost.cs (CREATE, 25 lines)
  - src/Infrastructure/Visualization/HtmlPreviewServer.cs (CREATE, 202 lines)
- Test: isolated live smoke test via PowerShell reflection on the built
  HVACLoadTerminals.Infrastructure.dll (net48): real HTTP round-trips against a
  running HttpListener on a free loopback port
- Result: PASS (all assertions below)

## Deliverable

- `public interface IHtmlPreviewHost : IDisposable` (namespace
  HVACLoadTerminals.Infrastructure.Visualization) — Start() / Stop() /
  IsRunning / BaseUrl / RecomputeScene(string) / Apply() / Cancel()
- `public sealed class HtmlPreviewServer : IHtmlPreviewHost`
  - ctor `(string title, string initialSceneJson, Func<string> recomputeSceneJson)`
    — the callback returns the NEW scene JSON when the browser POSTs
    /api/recompute (UI options changed)
  - `FindFreePort()`: TcpListener on IPAddress.Loopback:0 → port → stop
  - `Start()`: HttpListener bound to `http://127.0.0.1:<port>/`, async accept
    loop via Task.Run (per-context handling on a pool thread; loop exits on
    HttpListenerException/ObjectDisposedException when stopped)
  - Routes:
    - GET  /              → text/html, HtmlSceneExporter.BuildHtml(_title, _sceneJson)
    - GET  /api/scene     → application/json, current _sceneJson
    - POST /api/recompute → recomputeSceneJson() under lock(_sync), stores and
                            returns the new JSON
    - any other           → 404 text/plain
  - Responses: Content-Type with charset=utf-8, ContentLength64 from UTF-8 byte
    count; per-context try/catch (HttpListenerException / ObjectDisposedException /
    IOException)
  - `Stop()`: listener.Stop/Close guarded, field nulled; `Dispose() => Stop()`
  - `RecomputeScene(string)`: lock(_sync) replace scene JSON
  - `Apply()`/`Cancel()`: no-op hooks for window/dialog hosts

## Test evidence

Smoke test (PowerShell 5.1, real HTTP via System.Net.WebClient against the live server):

```
PASS: all preview server bridge smoke tests (GET /, api/scene, api/recompute, 404, Stop/Dispose)
```

Assertions that passed:
1. HtmlPreviewServer implements IHtmlPreviewHost; interface exists and is an interface
2. Start() → IsRunning=True, BaseUrl starts with `http://127.0.0.1:`
3. GET / → HTML: `<!DOCTYPE html>`, `const SCENE =`, placement family `Diffuser`,
   title HTML-escaped (`Test &amp; Title`)
4. GET /api/scene → JSON parses: 1 room, RoomName "Room 1"
5. POST /api/recompute → returns new scene (Title "Recomputed"); subsequent GET /
   reflects the recomputed scene
6. RecomputeScene("...Manual...") → GET /api/scene returns Title "Manual"
7. GET /nope → HTTP 404 (verified by unwrapping PS MethodInvocationException →
   WebException.Response.StatusCode)
8. Stop() → IsRunning=False; Dispose() → clean

## Build evidence

- `MSBuild src\Infrastructure\HVACLoadTerminals.Infrastructure.csproj /t:Build /p:Configuration=Debug` -> EXITCODE=0
- `MSBuild HVACLoadTerminals.sln /t:Build /p:Configuration=Debug` -> EXITCODE=0
  (Core + Infrastructure + App + Revit, zero regressions)

## Notes

- Test-harness gotcha (documented for future smoke tests): passing a PowerShell
  scriptblock as the `Func<string>` recompute callback deadlocks — the scriptblock
  is bound to the PS runspace and cannot re-enter while the script blocks on
  UploadString (server calls it on a threadpool thread). Fixed by Add-Type C# helper
  + `Delegate.CreateDelegate`, a pure .NET method.
- PowerShell 5.1 wraps .NET method exceptions in MethodInvocationException; the 404
  assertion unwraps InnerException to reach the WebException/HttpWebResponse.
- Browser runtime behavior (fetch/poll of /api/scene, recompute UX) is deferred to
  manual verification in M2/M4; the HTTP contract is machine-verified here.
