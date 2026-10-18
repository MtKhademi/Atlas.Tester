# SCN-VARIABLES-09 — Scenario variables and captured values

## Scope

- Add a persisted `variables` object to scenario JSON for fixed global values, for example `email`, `phone`, `password`, and `productKey`.
- Show global variables in Scenario Builder with add, edit, delete, and a secret-value option that masks the value in UI and reports.
- Remove Login Email/Phone and Login Password from the Test page. Scenario execution receives all input values exclusively from persisted scenario variables and captures.
- Resolve values using an explicit precedence: values captured by prior nodes → scenario global variables.
- Keep `{{variableName}}` as the single template syntax for Body, Headers, Query, Path parameters, Captures, and Assertions.
- Show the available variable source in the node Modal: global values and captures from earlier nodes.

## Acceptance criteria

- A scenario can define `email = ""`, `password = ""`, or a fixed `productKey` without adding it to every node.
- Register can capture a value and Login/Forgot Password can use it as `{{variableName}}`.
- Missing variables identify the node, field, and variable name.
- Secret global values are not written unmasked to HTML/JSON/CSV reports.

## Design notes

- Captured values are execution-local: they do not overwrite the saved global default.
- The Test page has no credential or scenario-variable inputs; it only configures the target and load run.

