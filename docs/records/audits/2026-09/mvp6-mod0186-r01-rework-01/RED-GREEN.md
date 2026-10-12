# R01 RED → GREEN

The added regression invokes the Returns-owned `ReturnReferenceReader` with the real producer error shape:

```json
{"error":{"code":"SHIPMENT_ROOT_INVALID","message":"SHIPMENT_ROOT_INVALID","correlationId":"aaaaaaaa-1111-4111-8111-aaaaaaaaaaaa"},"contractVersion":"v1"}
```

Before the product change, `ProducerShipmentRootInvalid500_MapsToReturnsRootInvalid` failed with `Expected: 502` and `Actual: 503`. The exact log is `raw/red/unit-r01.log` and its process exit is `1`.

After the change, the targeted set passed 10/10. It covers the exact producer error, unrelated `500`, malformed `500` body, wrong contract version, `501`, producer `401/403`, connection refusal, timeout and malformed root in a successful `200` body. The exact log is `raw/green/unit-r01.log` and its process exit is `0`.

The live composed API then proved the cross-endpoint path:

- direct Shipment detail: `500 SHIPMENT_ROOT_INVALID` (`raw/http/025-producer-root-bad-direct-detail.json`);
- Returns create for the same record: `502 RETURN_SHIPMENT_ROOT_INVALID` (`raw/http/028-producer-root-bad.json`);
- request and response `X-Correlation-Id` / error correlation use the current valid request trace.

The historical `mvp6-mod0186-http-01` files were not edited. Its old “null” wording remains historical; this package records the observed producer `500` accurately.
