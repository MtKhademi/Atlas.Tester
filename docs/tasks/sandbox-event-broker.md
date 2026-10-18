# SBX-EVENT-BROKER-14 — Isolated Kafka-compatible broker

## Scope

- Provision a disposable Kafka-compatible broker inside the sandbox network only when a selected project requires it.
- Supply the broker endpoint to the backend through runtime environment overrides.
- Capture published topics, keys, headers, timestamps, and masked payload metadata.
- Add scenario assertions for message publication and optional consumption.

## Acceptance criteria

- No developer or shared Kafka broker is reachable during a sandbox run.
- A scenario can assert that an expected topic message was published.
- Broker resources are removed after each run, including failure paths.

