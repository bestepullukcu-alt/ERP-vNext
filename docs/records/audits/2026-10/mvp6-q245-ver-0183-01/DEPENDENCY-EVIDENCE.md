# Q245 — The dependency question (task 5)

Two unchecked boxes point at MOD-0183. Neither was ticked. No MOD-0183 box was ticked.

| Pack | Line | Text |
|---|---:|---|
| `MOD-0186-reverse-logistics.md` | 137 | "MOD-0183 dependency has executable verified evidence." |
| `MOD-0187-claims-management.md` | 138 | "MOD-0183 executable verification is available." |

Neither pack defines the phrase: "executable" occurs once in each file, on that line. So what follows is what the
two modules **actually consume** from MOD-0183, measured in code, and what "executable" and "verified" would
each need. CT and the owner decide whether that reading is the intended one.

## What Returns and Claims consume from MOD-0183

One call each, read-only: `GET api/shipment-bundle/shipments/{id}`
(`…Infrastructure/Features/Returns/ReturnReferenceReader.cs:72`, `…/Claims/ClaimReferenceReader.cs:115`).
From the answer they need: `shipmentId`, `status` (one of the eight names), `carrierId`, `loadId`, `lines`, `pod`,
`contractVersion`, and `lifecycleCorrelationId` (Returns rejects a null root, `ReturnReferenceReader.cs:65`).
They also depend on the 404 for a shipment outside the caller's scope.

## What MOD-0183 would have to show, and what exists

| # | Needed | Exists today | Evidence |
|---:|---|---|---|
| 1 | The detail operation returns that shape, including `lifecycleCorrelationId` | **Yes**, on the test host | `ShipmentTests.cs:120` (lines 131–132, 148); `ShipmentRootStorageTests.cs:26` |
| 2 | A shipment can reach the states the consumers accept (Dispatched … Closed) | **Yes**, on the test host | `ShipmentTests.cs:120` (lines 134–141) |
| 3 | Out-of-scope reads answer 404 | **Yes**, on the test host | `ShipmentTests.cs:161` (lines 164–169) |
| 4 | The lifecycle is durable and replay-safe, so a consumer reads a stable fact | **Yes**, on the test host | `ShipmentTests.cs:120`, `:190`, `:208` |
| 5 | "Verified": an independent verification record accepted by CT | **Not yet** | This record is the first VER of MOD-0183; its verdict is BLOCKED (`SOP-22.md`) and it is not CT-accepted |
| 6 | "Executable" at runtime: the service starts and answers an authenticated request (E4) | **No** | Does not start in Development (Q217; Q232: still 13 errors with the four lines). No authenticated call exists outside the test host |
| 7 | The consumers can reach it: `Returns:ReferenceBaseUrl` / `Claims:ReferenceBaseUrl` configured, readers registered | **No** | The keys are read at `ReturnReferenceReader.cs:71`, `ClaimReferenceReader.cs:88`; no appsettings value exists; the readers are unregistered (Q232 `STARTUP-AND-HTTP.md`) |
| 8 | The real cross-module call has run once: Returns or Claims reading a real MOD-0183 shipment | **No** | `Returns/ReturnReferenceTests.cs:31` and `Claims/ClaimReferenceTests.cs:32,46` assert the request against a stub; no test calls the real Shipment endpoint from those modules |
| 9 | The Shipment contract the consumers validate against is the one MOD-0183 is pinned to | **Open** | 3.0.0 vs 3.1.0 — Q218 |

## Reading

- If "executable verified evidence" means **tests that execute MOD-0183's own behaviour**, rows 1–4 exist and
  have existed since Q208 (74 / 74, twelve of them through HTTP). What is missing is row 5: an accepted
  verification.
- If it means **the dependency works when Returns or Claims call it**, rows 6–8 are missing and cannot be
  produced until the service starts and the readers are wired.
- The two lines are worded differently ("has executable verified evidence" / "executable verification is
  available") but nothing in either pack distinguishes them.

Either way the boxes are not satisfiable today on this record alone: under the first reading they wait for CT's
acceptance of a MOD-0183 verification; under the second they wait for runtime.
