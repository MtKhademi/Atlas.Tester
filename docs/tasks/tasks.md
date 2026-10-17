# Atlas Tester task queue

This file is the source of truth for the ordered Atlas Tester delivery queue.

## Status values

- `planned`: ready when every dependency is `done`.
- `in_progress`: implementation has started.
- `review`: implementation, tests, build, and visible WPF review are complete; commit approval is pending.
- `done`: committed delivery is complete.

## Execution order

| Order | ID | Status | Depends on | Task |
| ---: | --- | --- | --- | --- |
| 1 | `SCN-JSON-01` | `done` | — | [Versioned scenario JSON model](scenario-json-model.md) |
| 2 | `CLIENT-STORAGE-08` | `planned` | `SCN-JSON-01` | [Centralize client-side storage](client-storage.md) |
| 3 | `SCN-REQUEST-02` | `planned` | `SCN-JSON-01`, `CLIENT-STORAGE-08` | [Request templates and variable resolution](scenario-request-templates.md) |
| 4 | `SCN-CAPTURE-03` | `planned` | `SCN-REQUEST-02` | [Response capture](scenario-response-capture.md) |
| 5 | `SCN-ASSERT-04` | `planned` | `SCN-CAPTURE-03` | [Assertions and node outcomes](scenario-assertions.md) |
| 6 | `SCN-BUILDER-05` | `planned` | `SCN-ASSERT-04` | [Scenario Builder node properties](scenario-builder-properties.md) |
| 7 | `SCN-RESULT-06` | `planned` | `SCN-BUILDER-05` | [Execution result details](scenario-execution-results.md) |
| 8 | `SCN-REFERENCE-07` | `planned` | `SCN-RESULT-06` | [Reference scenarios and regression coverage](scenario-reference-coverage.md) |

## Queue rules

1. `/task-run` executes exactly one task per invocation.
2. Without an ID, it selects the first eligible `planned` row in table order.
3. A task moves from `planned` to `in_progress`, then `review`, then `done` only after explicit owner acceptance and commit.
4. Before every implementation task, close the WPF app. After each task, build and visibly launch the new WPF executable for owner review.
5. No commit or push happens before the owner confirms the visible result.
6. Keep completed rows for delivery history. Never begin a second task in the same run.
