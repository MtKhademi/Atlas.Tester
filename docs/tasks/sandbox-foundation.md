# SBX-FOUNDATION-10 — Isolated sandbox foundation

## Goal

Define the Atlas Tester sandbox contract without modifying the backend under test.

## Scope

- Model an isolated run: backend project path, backend start command, target port, timeout, and environment overrides.
- Verify Docker availability before a run and give an actionable themed error when it is unavailable.
- Create a unique, per-run Docker network and run identifier.
- Define an allowlist of sandbox dependencies. All other outbound connections are blocked by default.
- Persist only non-secret run metadata and cleanup diagnostics in the Tester report directory.

## Acceptance criteria

- A run has a unique ID and its own Docker network.
- The user can see which backend project and dependencies will be started before execution.
- No environment variable value marked secret is written to logs or reports.
- A failed preparation cleans up resources created by that preparation.

