# Q217 — HTTP evidence

## Read this first

- In **Development** no request could be sent: the process exits before it listens (`STARTUP-VERDICT.md`).
- Everything below comes from the **supplementary run in the Production environment** on
  `http://127.0.0.1:5061` (pid 15026), 2026-10-02 23:02:41 +03, sent with `curl -v` directly to the service.
  The gateway was not involved.
- **No request carried a token.** The service demands a JWT bearer token: HS256, signed with
  `JwtSettings:Secret` (≥ 32 bytes), with matching issuer and audience and a valid lifetime
  (`Program.cs:25-41`). The Shipment branch then demands exactly one each of the claims `sub`, `tenant_id`
  and `legal_entity_id` as non-empty UUIDs, `permission` claims, and `X-Tenant-Id` / `X-Legal-Entity-Id`
  headers equal to the claims (`ShipmentContextMiddleware.cs:15-28`). No token was invented, so every
  call stops at authentication.
- Redaction: nothing needed redacting. No secret, token or cookie appears in any request or response.
  The UUIDs are made-up test values.
- The same ten requests were sent twice; the first batch (23:02:30) lost its output to a broken output
  filter. The service log shows the same status codes for both batches. The text below is the second batch.

## (a) Wired path — MOD-0183 Shipments

```text
> GET /api/shipment-bundle/shipments HTTP/1.1
> Host: 127.0.0.1:5061
> User-Agent: curl/8.7.1
> Accept: */*
> X-Correlation-Id: 11111111-2222-4333-8444-555555555555
> X-Tenant-Id: 00000000-0000-0000-0000-000000000001
> X-Legal-Entity-Id: 00000000-0000-0000-0000-000000000001
>
< HTTP/1.1 401 Unauthorized
< Content-Type: application/json; charset=utf-8
< Date: Fri, 02 Oct 2026 20:02:41 GMT
< Server: Kestrel
< Transfer-Encoding: chunked
< X-Correlation-Id: 11111111-2222-4333-8444-555555555555
<
{"error":{"code":"INVALID_REQUEST","message":"Authentication required.","correlationId":"11111111-2222-4333-8444-555555555555"},"contractVersion":"v1"}
```

Status 401 · correlation header echoed · body is the Shipment contract error. Produced by
`ShipmentContextMiddleware.cs:15`. The controller was not reached.

Without a correlation header:

```text
> GET /api/shipment-bundle/shipments HTTP/1.1
> Host: 127.0.0.1:5061
> User-Agent: curl/8.7.1
> Accept: */*
>
< HTTP/1.1 400 Bad Request
< Content-Type: application/json; charset=utf-8
< Date: Fri, 02 Oct 2026 20:02:41 GMT
< Server: Kestrel
< Transfer-Encoding: chunked
<
{"error":{"code":"INVALID_REQUEST","message":"A non-empty UUID X-Correlation-Id is required.","correlationId":"a9a051f2-3606-4a8d-a95f-4a92cbda2864"},"contractVersion":"v1"}
```

Status 400 · no correlation header in the response (`ShipmentContextMiddleware.cs:11-12`).

## (b) Half-wired path — MOD-0187 Claims

```text
> GET /api/shipment-bundle/claims HTTP/1.1
> Host: 127.0.0.1:5061
> User-Agent: curl/8.7.1
> Accept: */*
> X-Correlation-Id: 11111111-2222-4333-8444-555555555555
> X-Tenant-Id: 00000000-0000-0000-0000-000000000001
> X-Legal-Entity-Id: 00000000-0000-0000-0000-000000000001
>
< HTTP/1.1 401 Unauthorized
< Content-Type: application/json; charset=utf-8
< Date: Fri, 02 Oct 2026 20:02:41 GMT
< Server: Kestrel
< Transfer-Encoding: chunked
< X-Correlation-Id: 11111111-2222-4333-8444-555555555555
<
{"error":{"code":"INVALID_REQUEST","message":"Authentication required.","correlationId":"11111111-2222-4333-8444-555555555555"},"contractVersion":"v1"}
```

```text
> GET /api/shipment-bundle/claims HTTP/1.1
> Host: 127.0.0.1:5061
> User-Agent: curl/8.7.1
> Accept: */*
>
< HTTP/1.1 400 Bad Request
< Content-Type: application/json; charset=utf-8
< Date: Fri, 02 Oct 2026 20:02:41 GMT
< Server: Kestrel
< Transfer-Encoding: chunked
<
{"error":{"code":"INVALID_REQUEST","message":"A non-empty UUID X-Correlation-Id is required.","correlationId":"8dc08741-5ecf-4a14-9484-51a8e04da57a"},"contractVersion":"v1"}
```

**Were the Shipment middleware's rules applied to a request that is not a Shipment request? Yes — measured.**

- Both Claims responses are byte-identical in shape and text to the Shipments responses above.
- The 400 for a missing correlation header is a Shipment rule. The Claims module's own middleware does the
  opposite: it generates a correlation id, sets the response header, and checks authentication first, answering
  401 `UNAUTHENTICATED` with `WWW-Authenticate: Bearer` (`ClaimContextMiddleware.cs:48-62`). None of that
  appeared: wrong status (400 instead of 401), wrong code (`INVALID_REQUEST`), no `WWW-Authenticate`, no
  correlation header.
- So F-Q210-3 holds at runtime: the Claims route exists and is answered by the Shipment branch
  (`Program.cs:68`).

Returns (MOD-0186) behaves the same:

```text
> GET /api/shipment-bundle/returns HTTP/1.1
> Host: 127.0.0.1:5061
> User-Agent: curl/8.7.1
> Accept: */*
> X-Correlation-Id: 11111111-2222-4333-8444-555555555555
> X-Tenant-Id: 00000000-0000-0000-0000-000000000001
> X-Legal-Entity-Id: 00000000-0000-0000-0000-000000000001
>
< HTTP/1.1 401 Unauthorized
< Content-Type: application/json; charset=utf-8
< Date: Fri, 02 Oct 2026 20:02:41 GMT
< Server: Kestrel
< Transfer-Encoding: chunked
< X-Correlation-Id: 11111111-2222-4333-8444-555555555555
<
{"error":{"code":"INVALID_REQUEST","message":"Authentication required.","correlationId":"11111111-2222-4333-8444-555555555555"},"contractVersion":"v1"}
```

## Additional requests (beyond the two asked for)

S&OP and Capacity live under `/api/supply-chain/…`, outside every middleware branch. The Shipment
middleware passes them through (`ShipmentContextMiddleware.cs:9`); the framework's JWT challenge answers:

```text
> GET /api/supply-chain/sandop-plans/aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee HTTP/1.1
> Host: 127.0.0.1:5061
> User-Agent: curl/8.7.1
> Accept: */*
> X-Correlation-Id: 11111111-2222-4333-8444-555555555555
>
< HTTP/1.1 401 Unauthorized
< Content-Length: 0
< Date: Fri, 02 Oct 2026 20:02:41 GMT
< Server: Kestrel
< WWW-Authenticate: Bearer
<
```

```text
> GET /api/supply-chain/capacity-plans/aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee HTTP/1.1
> Host: 127.0.0.1:5061
> User-Agent: curl/8.7.1
> Accept: */*
> X-Correlation-Id: 11111111-2222-4333-8444-555555555555
>
< HTTP/1.1 401 Unauthorized
< Content-Length: 0
< Date: Fri, 02 Oct 2026 20:02:41 GMT
< Server: Kestrel
< WWW-Authenticate: Bearer
<
```

Empty body, no contract error, no correlation header: no module middleware ran for these two.

For contrast, the two modules that are wired with their own branch:

```text
> GET /api/shipment-bundle/carriers HTTP/1.1      (with correlation + scope headers)
< HTTP/1.1 401 Unauthorized
< WWW-Authenticate: Bearer
< X-Correlation-Id: 11111111-2222-4333-8444-555555555555
{"error":{"code":"INVALID_REQUEST","message":"Authentication required.","correlationId":"11111111-2222-4333-8444-555555555555"},"contractVersion":"v1"}

> GET /api/shipment-bundle/loads HTTP/1.1         (no headers)
< HTTP/1.1 401 Unauthorized
< WWW-Authenticate: Bearer
< X-Correlation-Id: e7aad236-ace8-440c-8b64-7a0798a9eee3
{"error":{"code":"INVALID_REQUEST","message":"Load request could not be completed.","correlationId":"e7aad236-ace8-440c-8b64-7a0798a9eee3"},"contractVersion":"v1"}
```

Service log: `Carrier request rejected 401 …` and `Load request rejected 401 …` — their own middleware ran.

Health (anonymous):

```text
> GET /health HTTP/1.1
< HTTP/1.1 200 OK
< Content-Type: application/json; charset=utf-8
{"status":"up","module":"MOD-0183","warehouseIntake":"unimplemented","outboxTransport":"integration-owned"}
```

## Summary

| Request | Status | Body | Correlation header | Answered by |
|---|---|---|---|---|
| GET `/health` | 200 | status JSON | none | minimal endpoint, `Program.cs:70` |
| GET shipments (headers) | 401 | Shipment contract error | echoed | Shipment middleware |
| GET shipments (no correlation) | 400 | Shipment contract error | none | Shipment middleware |
| GET claims (headers) | 401 | Shipment contract error | echoed | **Shipment middleware** |
| GET claims (no correlation) | 400 | Shipment contract error | none | **Shipment middleware** |
| GET returns (headers) | 401 | Shipment contract error | echoed | **Shipment middleware** |
| GET sandop-plans/{id} | 401 | empty | none | JWT bearer challenge |
| GET capacity-plans/{id} | 401 | empty | none | JWT bearer challenge |
| GET carriers (headers) | 401 | Carrier contract error | echoed | Carrier middleware |
| GET loads (no headers) | 401 | Load contract error | generated | Load middleware |

Raw captures: `/private/tmp/q217-startup-01/http-*.txt` (10 files).

## What this does and does not prove

- Proven: the routes of all seven controllers are mapped and answer over HTTP; authentication is enforced on
  every one; the Shipment branch takes Claims and Returns requests.
- Not proven: any authenticated behaviour. No handler, repository, validator, permission check or response
  serialisation ran for any module, MOD-0183 included. No request reached a controller.
