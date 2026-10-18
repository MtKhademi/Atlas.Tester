# SBX-SMS-MOCK-12 — Provider-compatible SMS mock

## Scope

- Run an SMS.ir Verify API-compatible mock inside the sandbox network.
- Route the backend's SMS provider BaseUrl to the mock through runtime environment overrides.
- Record recipient, template ID, parameter names, correlation ID, status, and timestamp.
- Keep message codes and secret parameter values in memory only; reports display masked metadata.
- Add a scenario assertion that waits for and verifies a matching mock SMS recipient/template.

## Acceptance criteria

- Forgot Password succeeds without connecting to SMS.ir Sandbox or any real SMS provider.
- A scenario can assert a message for `{{emailOrPhone}}`.
- The report shows that a matching mock SMS was queued without exposing reset codes.
- TLS/trust setup is deterministic; if a backend enforces HTTPS, the sandbox supplies a trusted local certificate rather than bypassing certificate validation.

