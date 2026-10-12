# R-04 — data classification, measured live (2026-10-03)

`RUNTIME-REQUIRED.tsv` R-04 asked for proof that console/Serilog output of a create and a POD capture
carries no recipient name, note, evidence reference or token. It was unmeasurable until Q236 made the
service boot. Measured now, against the running service on :5061.

## Method

A full golden flow was driven through the live service, with a **distinct marker per PII field** so a
leak would name its own source:

| field | classified as | marker |
|---|---|---|
| `Create.shipToReference` | address | `ZZADDRMARKERZZ` |
| `Transition.note` and `Pod.note` | note | `ZZNOTEMARKERZZ` |
| `Pod.recipientName` | recipient | `ZZRECIPIENTMARKERZZ` |
| `Pod.evidenceReferenceIds` | evidence reference | `ZZEVIDENCEMARKERZZ` |

Classification is the readiness record's, §8: "recipient/address/note/evidence references
confidential/PII; tokens and full payloads never logged."

Flow outcome: create → id `81051df4…`, `Planned` **200**, `Dispatched` **200**, POD **201**. This is
a complete backend golden flow executed against a started service rather than a test host.

## Result

| searched for | occurrences in `service.log` |
|---|---|
| address marker | **0** |
| note marker | **0** |
| recipient marker | **0** |
| evidence reference marker | **0** |
| the signing secret | **0** |
| any JWT value (`eyJ…`) | **0** |

## Both controls, so the zero means what it claims

A zero is worthless without proving the data arrived and that logging was switched on. Both were
checked.

**Positive control — the data really flowed.** `diten_r04.sce_shipments` contains the address
marker: `shipment stores shipToReference: true`. The PII reached persistence; it simply did not reach
the log.

**Negative control — logging was active and request-level.** 45 lines, including 10
`Request starting` / `Request finished` pairs, endpoint execution, route matching, full controller
action signatures, and the `LoggingBehavior` lines `Handling TransitionShipmentCommand` and
`Handling CapturePodCommand`. Serilog is at `MinimumLevel.Information` to console
(`Program.cs:23`). Request **content-length** is logged (89, 145 bytes) and request **content** is
not.

So the result is redaction, not silence.

## Noted, not a defect

The request path logs the shipment UUID. That is an identifier, and the readiness record's PII list
is recipient, address, note and evidence references — an entity id is not on it. Recorded so a later
reviewer does not read it as a leak. One `WRN` line appears at startup, unrelated to this flow.

## Verdict

**R-04 MET.** Gate row 9 (Data classification) was already marked MET by Q231 from inspection; this
is the live confirmation Q231 said it could not perform.

## Regenerating

Start the service with `ASPNETCORE_ENVIRONMENT=Development`, a generated `JwtSettings__Secret`, and
`Mongo__ConnectionString=mongodb://localhost:27017/?replicaSet=rs0`, then POST create, two
transitions and a POD with marker strings in the four fields above and grep the console output. Never
reuse the secret Q235 found exposed; this run generated its own with `openssl rand` and never printed
it.
