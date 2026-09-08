# ServiceInvoker streaming RPC (NDJSON)

This StarterKit supports **streaming manager methods** over HTTP using **newline-delimited JSON (NDJSON)**.

It matches the normal “call a manager method through ServiceInvoker” paradigm, except the response is a stream of JSON objects instead of a single JSON response.

---

## Overview

- **Non-streaming** (normal):
  - Endpoint: `POST /api/invoke`
  - Manager method returns a single DTO

- **Streaming**:
  - Endpoint: `POST /api/stream`
  - Manager method returns `IAsyncEnumerable<T>`
  - Each yielded `T` item is serialized as JSON and written as a single line:

```text
{ ...json... }\n
{ ...json... }\n
...
```

Content-Type: `application/x-ndjson`

---

## Backend requirements

### Manager method signature

A streaming manager method must return:

- `IAsyncEnumerable<T>`

Optionally, it may accept a `CancellationToken` parameter so it can stop work when the client cancels/disconnects.

Example signature:

- `MyMethod(MyRequestDto request) : IAsyncEnumerable<MyEventDto>`

### Routing

- Streaming endpoint is mapped by `Services/App.ServiceInvoker/Endpoints/ServiceInvokerEndpoints.cs`.
- The endpoint uses the same AuthN/AuthZ components as normal ServiceInvoker calls:
  - `IRequestAuthenticator`
  - `IMethodAuthorizer`

---

## Cancellation behavior

The streaming endpoint passes `HttpContext.RequestAborted` into async stream enumeration and response writes. When the browser aborts the request or the connection closes, stream enumeration is cancelled and the endpoint exits normally instead of emitting an application error event.

Manager streams should still observe cancellation inside long waits or external calls where possible so any in-progress work can stop promptly.

---

## Frontend usage

Use `ApiClient.streamMethod(...)` (or a wrapper in `src/apiClients/*`) to:

1) POST `{ managerName, methodName, parameters }` to `/api/stream`
2) parse each NDJSON line into an object

See:
- `react-app/src/api/ApiClient.ts` (`streamMethod`)
- (Optional demo UI) you can build a small view that calls `ApiClient.streamMethod(...)` and renders NDJSON events.

---

## When to use streaming vs normal calls

Use streaming when you need incremental progress updates or long-running operations.

Use normal manager calls when a single request/response is sufficient.
