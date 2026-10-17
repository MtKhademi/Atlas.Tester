# SCN-JSON-01 — Versioned scenario JSON model

## Scope

- Add a versioned persisted scenario document.
- Add node-level Request, Captures, and Assertions definitions.
- Preserve loading of existing scenario JSON files that have no version or new fields.
- Add serialization and legacy-compatibility tests.

## Acceptance criteria

- New saves write version 2 and the new node sections.
- Existing scenario files load as version 1 without losing their method or path.
- Engine tests verify both formats.
