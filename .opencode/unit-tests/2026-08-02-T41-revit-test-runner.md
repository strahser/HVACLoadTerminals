# Unit Test UT-11: T4.1 RevitTestRunner (in-Revit test framework)

- Date: 2026-08-02T12:31:19Z
- Target: src/Revit/Testing/* (RevitTestAttribute, RevitTestFixtureAttribute,
  TestAssertFailedException, Assert, RevitTestRunner, RunnerSmokeFixture) +
  src/Revit/Commands/RevitTestRunnerCommand.cs (T4.1)
- Test: isolated reflection smoke test on the built HVACLoadTerminals.Revit.dll —
  the Testing types are Revit-API-free (System-only), so the whole framework is
  exercisable without a running Revit session
- Result: PASS (all 6 assertions, 2/2 real fixtures executed)

## Deliverables (final on-disk state after concurrent-session reconciliation)

| File | Content |
|------|---------|
| src/Revit/Testing/RevitTestAttribute.cs | `RevitTestAttribute` (AttributeTargets.Method, optional FixtureType) + `RevitTestFixtureAttribute` (AttributeTargets.Class) — fixture attribute added to fix Planner's CS0246 blocker |
| src/Revit/Testing/RevitTestRunner.cs | `TestCaseResult` (Fixture, Method, Passed, DurationMs, Error?) + static `RevitTestRunner` with `RunAll(params Assembly[])` (SafeTypes discovery, public-instance [RevitTest] methods, bool-return support, IDisposable cleanup, Stopwatch timing), `ToJson(results, hostName, timestamp)` (manual StringBuilder JSON — zero deps), `WriteReport(results, hostName, directory=null)` (UTF-8 no BOM, revit-tests-<Timestamp>.json, default %LocalAppData%\HVACLoadTerminals\TestResults) |
| src/Revit/Testing/Assert.cs | Minimal assertion helpers (True/False/NotNull/Null/Equal/NotEqual/InRange/Near) throwing TestAssertFailedException — no runtime framework deps |
| src/Revit/Testing/TestAssertFailedException.cs | Exception type for Assert failures |
| src/Revit/Testing/RunnerSmokeFixture.cs | 2 smoke tests (written by concurrent T4.2 session): AssertHelpersWork, CoreGeometryHasOwnZeroException_AlwaysPasses |
| src/Revit/Commands/RevitTestRunnerCommand.cs | `[Transaction(Manual)] IExternalCommand`: RunAll(executing assembly) → WriteReport → TaskDialog (Passed/Total/Failed + report path) → Failed==0 ? Succeeded : Failed; try/catch → Result.Failed |
| src/Revit/Application.cs | RunTests ribbon button added by concurrent session (T3.3.4) |

## Reconciliation note (Worker ses_12, concurrent-write recovery)

1. A concurrent session replaced the initial design with `RunAll`/`TestCaseResult`
   + Assert helpers. The leftover command referenced `Run(Document,string)`/
   `RevitTestSummary` which no longer existed → project could not compile.
2. Kept the concurrent (richer) design; added the missing spec pieces around it:
   - `RevitTestFixtureAttribute` appended to RevitTestAttribute.cs (Planner CS0246 blocker)
   - `ToJson`/`WriteReport`/`Escape` appended to RevitTestRunner.cs
     (manual StringBuilder JSON per no-framework policy — no Newtonsoft dependency)
   - RevitTestRunnerCommand.cs rewritten to call `RunAll` + `WriteReport`
3. A concurrent write truncated RevitTestRunner.cs (stray `}` at EOF, method lost)
   — repaired via full-file rewrite; fixed CS8602 (`v!` in Escape).
4. Revit project builds EXITCODE=0 with ZERO warnings after the fixes.

## Test evidence

Isolated PowerShell 5.1 reflection smoke (`revit_runner_test.ps1`, load-from
bin\Debug\net48, no Revit runtime needed):

```
PASS: AttributesExist_WithTargets=True
PASS: RunnerMembers=True
INFO: RunAll discovered=2 passed=2 failed=0
  TEST: RunnerSmokeFixture.AssertHelpersWork passed=True err=True
  TEST: RunnerSmokeFixture.CoreGeometryHasOwnZeroException_AlwaysPasses passed=True err=True
PASS: RunAll_ExecutedAllClean=True
PASS: ToJson_ReportMatches=True
PASS: ToJson_SyntheticFailing=True
PASS: WriteReport_File=True
RESULT: ALL TESTS PASSED
```

Assertions covered:
1. Both attributes exist; AttributeUsage targets Method and Class respectively
2. RunAll / ToJson / WriteReport public members exist
3. RunAll discovered and EXECUTED 2 real [RevitTest] fixtures: 2 passed, 0 failed
4. ToJson summary matches actual counts; Host/Timestamp fields correct
5. Synthetic failing result with quotes/backslashes round-trips exactly
   (escaping correct, JSON parses, DurationMs/Error fields intact)
6. WriteReport created the file (UTF-8, no BOM, parseable, revit-tests-* name)

## Build evidence

```
MSBuild src\Revit\HVACLoadTerminals.Revit.csproj /t:Build /p:Configuration=Debug
  -> EXITCODE=0, no warnings (after v! fix)
MSBuild HVACLoadTerminals.sln /t:Build /p:Configuration=Debug
  -> EXITCODE=0 (Core + Infrastructure + App + Revit, zero regressions)
```

## Notes

- The runner framework is fully verifiable outside Revit because the Testing
  types reference only System assemblies; command/Application wiring requires a
  live Revit session (deferred to M4 manual verification).
- RunnerSmokeFixture (2 tests) landed from the concurrent T4.2 session — its
  execution inside this smoke test doubles as live proof of discovery + invoke.
