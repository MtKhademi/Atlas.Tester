# CLIENT-STORAGE-08 — Centralize client-side storage

## Scope

- Define one per-user client-data root for all Atlas Tester persisted data, rather than writing scenarios and reports beside the executable.
- Place project definitions, OpenAPI catalog cache, saved scenarios, and generated reports under named subdirectories of that root.
- Use a Windows per-user application-data location that remains writable after installation and does not depend on the current build output folder.
- Provide safe compatibility behavior for data already saved in the current locations.

## Acceptance criteria

- One documented root contains `projects`, `catalogs`, `scenarios`, and `reports` subdirectories.
- A newly saved scenario and report remain available after updating or rebuilding the application.
- Existing project definitions and scenario files remain discoverable or are migrated without deletion.
- The UI reports the new storage locations accurately.
