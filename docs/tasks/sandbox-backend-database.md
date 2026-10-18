# SBX-BACKEND-DATABASE-11 — Ephemeral backend and database

## Scope

- Start a disposable PostgreSQL container inside the sandbox network with no externally published database port.
- Start the selected backend as a child process or container with a generated connection string that points only to that database.
- Apply runtime environment overrides for the backend; no tracked backend configuration file is changed.
- Detect backend readiness from its OpenAPI endpoint before allowing scenario execution.
- Capture stdout/stderr safely and stop the backend when the sandbox ends.

## Acceptance criteria

- The backend cannot reach the developer's configured local or remote database during a sandbox run.
- The database volume is unique to the run and is removed at cleanup.
- A readiness timeout reports the backend output needed for diagnosis.
- The existing Test page can execute a scenario against the isolated backend URL.

