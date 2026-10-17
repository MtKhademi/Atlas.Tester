# CLIENT-STORAGE-08 — Centralize client-side storage

## Scope

- Define one executable-adjacent client-data root for all Atlas Tester persisted data.
- Place project definitions, OpenAPI catalog cache, saved scenarios, and generated reports under named subdirectories of that root.
- Keep data in named folders beside the executable so it is easy to find with the deployed application.
- Provide safe compatibility behavior for data already saved in the current locations.

## Acceptance criteria

- The executable directory contains `projects`, `catalogs`, `scenarios`, and `reports` subdirectories.
- A newly saved scenario and report remain available after updating or rebuilding the application.
- Existing project definitions and scenario files remain discoverable or are migrated without deletion.
- The UI reports the new storage locations accurately.
