# SCN-REQUEST-02 — Request templates and variable resolution

## Scope

- Execute node request body, query string, headers, and path parameters from saved JSON.
- Resolve literal values and `{{variableName}}` placeholders from the execution context.
- Report unresolved variables without exposing sensitive values.

## Acceptance criteria

- A node can send a fixed body and a body containing resolved values from an earlier node.
- Query, header, and path parameter templates are covered by focused tests.
