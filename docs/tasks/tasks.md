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
| 2 | `CLIENT-STORAGE-08` | `done` | `SCN-JSON-01` | [Centralize client-side storage](client-storage.md) |
| 3 | `SCN-REQUEST-02` | `done` | `SCN-JSON-01`, `CLIENT-STORAGE-08` | [Request templates and variable resolution](scenario-request-templates.md) |
| 4 | `SCN-CAPTURE-03` | `done` | `SCN-REQUEST-02` | [Response capture](scenario-response-capture.md) |
| 5 | `SCN-ASSERT-04` | `done` | `SCN-CAPTURE-03` | [Assertions and node outcomes](scenario-assertions.md) |
| 6 | `SCN-BUILDER-05` | `done` | `SCN-ASSERT-04` | [Scenario Builder node properties](scenario-builder-properties.md) |
| 7 | `SCN-IMPORT-06` | `done` | `SCN-BUILDER-05` | [Import scenario JSON into the Builder](scenario-json-import.md) |
| 8 | `SCN-RESULT-07` | `done` | `SCN-BUILDER-05`, `SCN-IMPORT-06` | [Execution result details](scenario-execution-results.md) |
| 9 | `SCN-REFERENCE-08` | `done` | `SCN-RESULT-07` | [Reference scenarios and regression coverage](scenario-reference-coverage.md) |
| 10 | `SCN-VARIABLES-09` | `done` | `SCN-REFERENCE-08` | [Scenario variables and captured values](scenario-variables.md) |
| 11 | `SBX-FOUNDATION-10` | `done` | `SCN-VARIABLES-09` | [Isolated sandbox foundation](sandbox-foundation.md) |
| 12 | `SBX-BACKEND-DATABASE-11` | `in_progress` | `SBX-FOUNDATION-10` | [Ephemeral backend and database](sandbox-backend-database.md) |
| 13 | `SBX-SMS-MOCK-12` | `planned` | `SBX-BACKEND-DATABASE-11` | [Provider-compatible SMS mock](sandbox-sms-mock.md) |
| 14 | `SBX-EXTERNAL-MOCKS-13` | `planned` | `SBX-BACKEND-DATABASE-11` | [External HTTP service mocks](sandbox-external-mocks.md) |
| 15 | `SBX-EVENT-BROKER-14` | `planned` | `SBX-BACKEND-DATABASE-11` | [Isolated Kafka-compatible broker](sandbox-event-broker.md) |
| 16 | `SBX-REPORT-CLEANUP-15` | `planned` | `SBX-SMS-MOCK-12`, `SBX-EXTERNAL-MOCKS-13`, `SBX-EVENT-BROKER-14` | [Sandbox report and lifecycle cleanup](sandbox-report-cleanup.md) |

## Queue rules

1. `/task-run` executes exactly one task per invocation.
2. Without an ID, it selects the first eligible `planned` row in table order.
3. A task moves from `planned` to `in_progress`, then `review`, then `done` only after explicit owner acceptance and commit.
4. Before every implementation task, close the WPF app. After each task, build and visibly launch the new WPF executable for owner review.
5. No commit or push happens before the owner confirms the visible result.
6. Keep completed rows for delivery history. Never begin a second task in the same run.
