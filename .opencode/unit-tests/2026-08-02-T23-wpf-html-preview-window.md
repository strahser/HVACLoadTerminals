# Unit Test UT-12: T2.3 WPF HTML Preview Window

- Date: 2026-08-02T12:37:00Z
- Target:
  - src/App/Views/HtmlPreviewWindow.xaml (CREATE, 42 lines)
  - src/App/Views/HtmlPreviewWindow.xaml.cs (CREATE, 56 lines)
  - src/App/Commands/OpenHtmlPreviewCommand.cs (CREATE, 52 lines)
- Test: MSBuild compilation check (App project + full solution)
- Result: PASS

## Deliverables

- `HtmlPreviewWindow.xaml` — WPF Window with DockPanel layout:
  - Dark theme (#111 background), 1024x700, MinWidth 800
  - Top toolbar (Border + StackPanel): Recompute, Apply, Cancel buttons + status TextBlock
  - Content: native WPF WebBrowser (no WindowsFormsHost needed)
- `HtmlPreviewWindow.xaml.cs` — Code-behind:
  - Constructor: `HtmlPreviewWindow(IHtmlPreviewHost host, Action? recompute = null)`
  - Navigates to host.BaseUrl on load
  - Recompute → invokes recompute callback + browser.Refresh()
  - Apply → host.Apply() + DialogResult=true + Close()
  - Cancel → host.Cancel() + DialogResult=false + Close()
  - Closed event → host.Stop() + host.Dispose()
- `OpenHtmlPreviewCommand.cs` — ICommand implementation:
  - Constructor: `OpenHtmlPreviewCommand(Func<string>? getSceneJson = null)`
  - Execute: accepts sceneJson parameter or falls back to getSceneJson callback or empty default
  - Creates HtmlPreviewServer, starts it, shows HtmlPreviewWindow as dialog

## Build Evidence

```
MSBuild src\App\HVACLoadTerminals.App.csproj /t:Build /p:Configuration=Debug
  -> EXITCODE=0 (Core + Infrastructure + App)

MSBuild HVACLoadTerminals.sln /t:Build /p:Configuration=Debug
  -> EXITCODE=0 (Core + Infrastructure + App + Revit, zero regressions)

dotnet test Core.Tests 33/33 pass
```

## Notes

- Used native WPF `System.Windows.Controls.WebBrowser` instead of WindowsFormsHost + WinForms WebBrowser — same IE MSHTML engine, no extra references needed
- No csproj changes required (WebBrowser is part of PresentationFramework, already included with UseWPF=true)
- SDK-style project auto-includes new .cs files — no manual file inclusion needed
