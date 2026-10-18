# SBX-EXTERNAL-MOCKS-13 — External HTTP service mocks

## Scope

- Add a per-sandbox registry of mock routes: service name, method, path, response status, headers, delay, and response body.
- Route configured backend dependency URLs to the matching internal mock through environment overrides or Docker DNS aliases.
- Record inbound mock requests and expose them to scenario assertions and reports.
- Make an unmatched mock request fail clearly; never fall through to the public internet.

## Acceptance criteria

- A scenario can define a deterministic external API response and assert that the backend called it.
- Delays and error responses such as 429/500 can be tested without changing the backend.
- Every mock call in reports identifies its service and route, with secrets masked.

