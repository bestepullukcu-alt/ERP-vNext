# SOP §22 — MVP6-SHIPMENT-UI-B01-B02-INDEPENDENT-VER-01 Final Successor

## Verdict

**PASS for the bounded SHIP-UI-B01/B02 rework.**

The previously OPEN runtime rows are now verified on the same frozen source: B01 list/render, B02 authoritative-root transition/POD, replay, changed-payload conflict, scoped zero-write, and same-binary/same-database restart all pass. PNG remains **OPEN** because no supported persistent export capability was available. Platform aggregate health remained **503** and is not represented as healthy.

This is an independent bounded verification result. It is not CT acceptance, full-module acceptance, G5, or rollout approval.

## Controlling source and inherited verification

- Repository HEAD: `4a8d4d4b339528a88e6220fb8402e5a2c771136c`.
- Shipment/UI source manifest: 354/354, SHA-256 `e6551f4552bd05682dcd9f2753044aa0b803dd4df90875952cca89608ab98ab3`.
- Auth successor manifest: 22/22, SHA-256 `b9713185aba02bf325a9149b37a8cc6ffe3b1dd0e979febdaa773946ae9b1731`.
- Build-source manifest: 3333/3333, SHA-256 `516ba828bed602e4355a5cc6871501cbb3b3c72d60473fa7ec73d135bbadf911`.
- B01/B02 patch SHA-256: `12fa97aef9e2ea360882a3a0f2c80fc32f5658fe7aa1f05d0af03b86c62034af`.
- Prior independent artifact, patch-roundtrip, native .NET 8 build, and focused 9/9 test results remain controlling and were not repeated.

## Runtime environment

- Native .NET: SDK `8.0.417`, runtime `8.0.23`.
- Mongo: `rsShipmentUiB01B02VerFinal`, `127.0.0.1:42134`, PRIMARY; operational port 27017 was not used.
- Databases: lane-specific Auth, Platform, MDM, SupplyChain, and Web databases suffixed `ShipmentUiB01B02VerFinal`.
- Ports: Gateway 5930, Web 5931, Auth 5966, Platform 5967, MDM 5969, SupplyChain 5971.
- Supply binary SHA-256: `568833f6bc3a9dc29390c44d501fbe7fa0bfa206c1998e33fa88ee52deb392eb`.
- Fresh real Auth-issued session was created through Web and Gateway. No password, bearer, signing key, or connection secret is in the evidence archive.

## Browser provenance

The subagent's IAB entry point returned `Browser is not available: iab`; native Chrome binding then returned `MCP elicitations can only be requested by the root thread`. The root thread therefore operated IAB against this verifier's live isolated stack. This is disclosed as **root-assisted browser operation**. It is not writer evidence. The verifier independently checked the resulting persisted documents, receipt fingerprints, audit/outbox correlation, HTTP replay/conflict responses, scoped deltas, and restart state.

## B01/B02 evidence

| Criterion | Result | Evidence |
|---|---|---|
| B01 list/render | PASS | `/SupplyChain/Shipments` rendered 200 with Status, Source Document, Apply, Reset, and DataTable surfaces. |
| B02 transition root | PASS | Browser Draft→Planned→Dispatched; two transition receipts, audits, and snapshots use persisted root `e80cc126-42b6-4264-b31c-d2e5603f8fa8`. |
| B02 POD root | PASS | Browser POD reached Delivered; POD receipt, audit, outbox, and shipment retain the same root. |
| POD content | PASS | Recipient `Independent Receiver`; evidence reference `POD-EVIDENCE-VER-01`. |
| Replay | PASS | Same key/body/root returned HTTP 201 with `idempotentReplay=true`. |
| Changed payload | PASS | Same key with changed recipient returned HTTP 409 `IDEMPOTENCY_KEY_REUSED`; correlation equals authoritative root. |
| Scoped zero-write | PASS | Before/after counts stayed shipments 1, receipts 4, audit 4, outbox 5; every delta is 0 and query exit is 0. |
| Restart | PASS | Supply PID changed `62012`→`62623`; binary hash and database stayed fixed. API detail and browser detail both returned Delivered, identical root, recipient, and evidence. |
| Pending outbox | PASS | 5/5 Shipment outbox rows remained Pending with attempt count 0. |
| PNG | OPEN | No supported persistent PNG export; no restricted workaround. |
| Platform aggregate health | OBSERVED-503 | Recorded separately. On restart-only browser reload the Shipment detail succeeded while the menu displayed its failure/remediation message. |

Shipment ID: `15b654be-6029-42ea-8ea4-2d13cdeacb57`.

## Evidence

- Runtime/browser archive: `runtime-browser-evidence.tar.gz`, SHA-256 `7cbd8e69b1f273ef99aea3f2151f122a9c3bb80d9b28dd531aef56dda89ab598`.
- Historical initial verification archive: `evidence.tar.gz`, SHA-256 `6aa6911c0e6150026741d6aede1c4bab15f090f3872f0db8fc1826308c164867`.
- Final row-level result: `ACCEPTANCE-FINAL.tsv`.
- Source/build/process/browser chain: `SOURCE-BINARY-PROCESS-BROWSER-FINAL.tsv`.

## Boundaries and cleanup

- No product, writer-package, contract, Gateway source, Auth source, pack, guard, or Git mutation.
- The only repository additions by this successor are final evidence files in this independent VER directory.
- All lane processes stopped. Ports 42134, 5930, 5931, 5966, 5967, 5969, and 5971 were FREE.
- Secret scan over archived runtime evidence: no bearer, password, JWT signing secret, or service-identity secret match.

