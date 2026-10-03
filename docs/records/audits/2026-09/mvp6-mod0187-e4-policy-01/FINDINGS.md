# Exact rework findings

## F-E4-01 — Unicode-scalar Idempotency-Key cannot reach Claims policy

- **Input:** valid JWT/scope/correlation, valid create payload and `Idempotency-Key` containing exactly 128 `é` Unicode scalar values.
- **Expected:** the annex accepts 1–128 Unicode scalar values; request reaches Claims policy and this valid create returns 201. A 129-scalar key must return the Claims 400 `INVALID_REQUEST` envelope.
- **Actual:** both 128 and 129 non-ASCII headers are rejected by Kestrel before `ClaimContextMiddleware`, with bare HTTP 400, zero response body and no `X-Correlation-Id`. The same probe proves 128 ASCII returns 201 and 129 ASCII returns the exact application 400 envelope.
- **Evidence:** `policy-results.json` tests `E4-R14-H12/H13`; `header-boundary.json` for the ASCII control.
- **Disposition:** product/contract compatibility defect. Runtime rework must either make the published Unicode-scalar domain transportable at the real HTTP boundary or require a separately authorized contract amendment. This lane makes neither change.

## F-E4-02 — authoritative nil Shipment root is not consumable end-to-end

- **Input:** an actual Shipment BSON record with authoritative `LifecycleCorrelationId = 00000000-0000-0000-0000-000000000000`; Claims create uses the same nil `X-Correlation-Id`.
- **Expected:** published Claims/root semantics preserve a real nil UUID; Shipment detail returns 200 with that root and Claims create returns 201.
- **Actual:** direct Shipment detail rejects nil request correlation with 400 `INVALID_REQUEST` (“A non-empty UUID X-Correlation-Id is required.”). Claims forwards its nil correlation to Shipment; the producer 400 is mapped to 502 `CLAIM_REFERENCE_INVALID`. The same process proves non-nil root create 201, missing BSON root emitted null then Claims 503, and real Carrier list uptake/create 201.
- **Evidence:** `producer-uptake.json` (`nilDetail:false`, `nilCreate:false`; all other producer checks true).
- **Disposition:** cross-component runtime defect. A rework must keep trace correlation distinct from the authoritative business root or otherwise reconcile the published nil rule without deriving/replacing the root. No source change was made.
