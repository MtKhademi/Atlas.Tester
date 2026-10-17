# SCN-ASSERT-04 — Assertions and node outcomes

## Scope

- Support reachability, transport failure, exact/allowed/range status codes, JSON-path existence, and JSON-value assertions.
- Distinguish no response from an HTTP response such as 404 or 500.
- Return human-readable assertion failures.

## Acceptance criteria

- Tests distinguish unavailable service, unexpected 404, expected 401, and successful 200 responses.
