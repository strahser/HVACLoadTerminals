# Unit Test UT-12: T3.3.4 Ribbon Buttons (Application.cs)

- Date: 2026-08-02T12:34:30Z
- Target: src/Revit/Application.cs (MODIFY, 74 lines)
- Test: isolated PowerShell regex test against file content
- Result: PASS (13/13 assertions)

## Deliverable

- Two new PushButtonData entries added to Application.cs:
  - `massBtnData`: "MassPlacement" / "Mass\nPlacement" → RevitHtmlPlacementCommand
  - `individualBtnData`: "IndividualPlacement" / "Individual\nPlacement" → RevitIndividualPlacementCommand
- Both wired via panel.AddItem after ExportRooms, before RunTests
- Existing 4 buttons (PlaceTerminals, ReviewPlacement, ExportRooms, RunTests) intact
- Total 6 AddItem calls in correct order
- HVACLoadTerminals.addin already had Application entry — no changes needed

## Test Code (Preserved)
```powershell
param()
$appPath = "D:\Projects\HVACLoadTerminals\src\Revit\Application.cs"
$content = Get-Content $appPath -Raw
$pass = 0; $fail = 0
function Assert($name, $condition) {
    if ($condition) { Write-Host "PASS: $name"; $script:pass++ }
    else { Write-Host "FAIL: $name"; $script:fail++ }
}
Assert "MassPlacement button name" ($content -match '"MassPlacement"')
Assert "MassPlacement label" ($content -match '"Mass\\nPlacement"')
Assert "MassPlacement command class" ($content -match '"HVACLoadTerminals\.Revit\.Commands\.RevitHtmlPlacementCommand"')
Assert "IndividualPlacement button name" ($content -match '"IndividualPlacement"')
Assert "IndividualPlacement label" ($content -match '"Individual\\nPlacement"')
Assert "IndividualPlacement command class" ($content -match '"HVACLoadTerminals\.Revit\.Commands\.RevitIndividualPlacementCommand"')
Assert "MassPlacement AddItem" ($content -match 'panel\.AddItem\(massBtnData\)')
Assert "IndividualPlacement AddItem" ($content -match 'panel\.AddItem\(individualBtnData\)')
Assert "PlaceTerminals intact" ($content -match '"PlaceTerminals"')
Assert "ReviewPlacement intact" ($content -match '"ReviewPlacement"')
Assert "ExportRooms intact" ($content -match '"ExportRooms"')
Assert "RunTests intact" ($content -match '"RunTests"')
$addItemCount = ([regex]::Matches($content, 'panel\.AddItem\(')).Count
Assert "6 AddItem calls" ($addItemCount -eq 6)
Write-Host "`nTOTAL: PASS=$pass FAIL=$fail"
if ($fail -gt 0) { exit 1 } else { exit 0 }
```

## Test Result
- Status: pass
- Session: ses_15
- Timestamp: 2026-08-02T12:34:30Z

## Build Evidence
```
MSBuild src\Revit\HVACLoadTerminals.Revit.csproj /t:Build /p:Configuration=Debug
  -> HVACLoadTerminals.Revit.dll (EXITCODE=0, zero warnings)
```
