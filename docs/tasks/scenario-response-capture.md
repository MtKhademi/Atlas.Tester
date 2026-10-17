# SCN-CAPTURE-03 — Response capture

## Scope

- Extract named values from JSON responses using the persisted capture selectors.
- Store captures in the scenario execution context for later nodes.
- Produce a clear node-specific error for a non-matching selector.

## Acceptance criteria

- Register can capture an email or identifier that Login uses.
- Capture failures identify the node, variable, and selector.
