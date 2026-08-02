# Unit Test UT-12: T4.2 Revit Integration Fixtures

- Date: 2026-08-02T12:38:00Z
- Target:
  - src/Revit/Testing/TestDocumentContext.cs (CREATE, 18 lines)
  - src/Revit/Testing/RevitIntegrationFixtures.cs (CREATE, 318 lines)
  - src/Revit/Commands/RevitTestRunnerCommand.cs (MODIFY, added TestDocumentContext.Document assignment)
- Test: MSBuild compilation check (Revit project + full solution) + dotnet test for Core tests (no regressions)
- Result: PASS (all compilations succeed, 33/33 Core tests pass)

## Deliverables

- `public static class TestDocumentContext` — static holder for active Revit Document
- `public class SpaceExtractionFixture` — [RevitTestFixture] with:
  - [RevitTest] Rooms_AreExtracted() — verifies room extraction from document
  - [RevitTest] Polygon_IsValid() — verifies polygon validity (>=3 vertices, area>0)
- `public class FamilyCatalogFixture` — [RevitTestFixture] with:
  - [RevitTest] Families_AreCollected() — verifies family collection
  - [RevitTest] FlowParam_Mapped() — verifies flow parameter mapping
  - [RevitTest] SystemType_Classified() — verifies system type classification
- `public class PlacementFixture` — [RevitTestFixture] with:
  - [RevitTest] Quantity_ByCalculation() — pure C# quantity test (1200/340 → 4)
  - [RevitTest] Positions_InsidePolygon() — synthetic room placement inside boundary
  - [RevitTest] Offset_500mm() — verifies 500mm offset from wall
  - [RevitTest] Rotation_MatchesNormal() — verifies rotation range
- `public class PreviewRollbackFixture` — [RevitTestFixture] with:
  - [RevitTest] Preview_RequiresStartedTransaction() — documents guard rail (manual verification)
  - [RevitTest] Preview_NullUIDoc_Throws() — verifies ArgumentNullException for null UIDocument

## Integration

- RevitTestRunnerCommand now sets TestDocumentContext.Document before running tests
- All fixtures use TestDocumentContext.Document and skip/return false when null
- Pure C# tests (PlacementFixture.Quantity_ByCalculation) run without Revit session
- Full solution build: EXITCODE=0
- Core tests: 33/33 pass

## Verification Evidence

1. Revit project build: EXITCODE=0
2. Full solution build: EXITCODE=0
3. Core tests: 33/33 pass (no regressions)
4. No compilation warnings (after removing unused variable)