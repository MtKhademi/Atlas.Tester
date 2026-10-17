# SCN-IMPORT-06 — Import scenario JSON into the Builder

## Scope

- Add an Import JSON action in Scenario Builder.
- Let the user paste a complete scenario JSON document or choose a JSON file.
- Validate the versioned scenario schema and report parse/validation errors without replacing the current workflow.
- On success, populate the scenario name, description, and Node canvas from the imported document.

## Acceptance criteria

- Pasting a valid scenario JSON creates its Nodes in the visual workflow.
- Invalid JSON or an invalid scenario shows a clear error and leaves the existing workflow unchanged.
- Imported scenarios can be edited and saved using the normal Builder controls.
