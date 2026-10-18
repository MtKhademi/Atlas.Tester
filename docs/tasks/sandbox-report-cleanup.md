# SBX-REPORT-CLEANUP-15 — Sandbox report and lifecycle cleanup

## Scope

- Show sandbox lifecycle stages in the Test UI: preparing, ready, running, collecting, cleaning up, and failed.
- Produce a report section for backend process state, database, mock SMS, external mocks, and broker activity.
- Provide an explicit emergency cleanup action for orphaned resources belonging to Atlas Tester only.
- Add integration coverage for successful runs, readiness failure, assertion failure, and cleanup failure.

## Acceptance criteria

- The report identifies the isolated run and each mocked dependency used by it.
- Cleanup runs after success, cancellation, and failure.
- The cleanup action never deletes containers, networks, volumes, or processes not owned and labeled by Atlas Tester.

