# INT-SMS-MOCK-10 — SMS mock integration

## Scope

- Add an in-process/local SMS mock provider for test and development environments; no real SMS is sent.
- Capture outbound SMS records: recipient, template/message, correlation ID, status, and timestamp.
- Add scenario assertions/waits that can verify a matching SMS was produced after a Register or Forgot Password request.
- Provide a small UI/report view of mock SMS activity and an explicit environment mode: `Mock` or `External`.

## Acceptance criteria

- Register/Forgot Password scenarios can assert that an SMS was queued for `{{emailOrPhone}}`.
- The test report shows the matching mock message metadata without exposing reset codes or secrets by default.
- Mock mode is the default; external SMS can never be used accidentally by a local test run.

## Open decision before implementation

- The application under test must route SMS through an injectable provider, an HTTP mock endpoint, or a test-only message sink. We will inspect the API's existing SMS abstraction before choosing the adapter.
