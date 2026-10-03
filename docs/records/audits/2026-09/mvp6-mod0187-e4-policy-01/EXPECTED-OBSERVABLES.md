# MVP6-MOD0187-E4-POLICY-01 — execution contract

Recorded before the fresh process/HTTP/DB run. The controlling sources are the published
`SHIPMENT-BUNDLE 3.0.0 / wire v1` Claims operations, `claims-semantics-v3.0.0.md`, and the
independent `R01-R30.md`. Controlled dependency responses are evidence for Claims mapping and
precedence only; they are never labelled as live Shipment/Carrier producer uptake.

| Row | Test IDs | Required observable in this lane |
|---|---|---|
| R01 | `E4-R01-S01..S08` | Eight Shipment states over HTTP: Dispatched/InTransit/Delivered/Exception/Closed create 201; Draft/Planned/Cancelled return 422 `CLAIM_SHIPMENT_INELIGIBLE`; every rejected case has zero delta in claims/receipts/audit/outbox. |
| R02 | `E4-R02-C01..C12` | Explicit matching Active/Suspended/Retired carriers create 201 and produce an unfiltered `GET /carriers`; mismatch or null source carrier returns 422; omitted decision field returns 503; malformed/missing Carrier fields return 502; absent explicit carrier returns 404. |
| R03 | `E4-R03-O01..O04` | Fresh create persists the values and `observedAt` from the response actually read. A fixture changed after that response does not change the snapshot. Committed replay and transition do not re-read Shipment/Carrier. |
| R04 | `E4-R04-F01..F08` | Missing/foreign reference 404; malformed JSON/type/identity 502; incomplete decision data 503; timeout/refusal 503. Error code/message/correlation are exact and all four Claims collection deltas are zero. |
| R05 | `E4-R05-E01..E05` | Evidence omission equals `[]`; empty array and `['', 'e1', 'e1']` are accepted and exact order/duplicates/empty string persist; explicit null is 400; no evidence verification request/flag is produced. |
| R06 | `E4-R06-A01..A14` | Claimed amounts must be positive. Approval accepts 0, -0, equal and less; rejects missing/null, negative and above-claim with exact code. Non-Approved non-null amount returns 422. Omission/null retains the approved text through Settled/Closed in this process. |
| R07 | `E4-R07-M01..M06` | Resolution/note omission/null/empty follow schema and audit retention. Different keys may create multiple claims for one Shipment with no aggregate ceiling. No finance client/call/collection is observed. |
| R08 | `E4-R08-P01..P03` | A long coefficient/scale value round-trips through HTTP and BSON string without narrowing in this process. Process-boundary restart persistence and capacity/unknown-commit evidence remain owned by Lane C and cannot be closed here. |
| R09 | `E4-R09-L01..L14` | Leading zeros and exact positive tiny value are retained. Exponent, plus, trailing/bare dot, whitespace, non-ASCII digits and JSON number reject 400. Claimed zero/-0 reject 422; approved zero/-0 remain valid. Currency profile is exact `[A-Z]{3}`. |
| R10 | `E4-R10-I01..I10` | Fingerprint keeps amount/time lexical text; decoded object order and escape equivalence replay; omission equals null and evidence omission equals `[]`; array reorder conflicts. Same-root changes return 409 `IDEMPOTENCY_KEY_REUSED`; wrong-root precedes fingerprint with 409 `CLAIM_CORRELATION_MISMATCH`. |
| R11 | `E4-R11-P01..P05` | Withdrawn requires investigate; create-only is 403, investigate is 200; authorized foreign-scope target is 404; denied cases add no documents. |
| R12 | `E4-R12-P01..P05` | Closed requires decide and succeeds from Rejected/Settled; settle-only is 403; target Open with create permission passes target authorization then returns 422 `INVALID_CLAIM_TRANSITION`. |
| R13 | `E4-R13-P01..P04` | Same creator with create-only cannot approve; the same creator with decide can approve after Investigating. No hidden dual-control/tier rule is added. |
| R14 | `E4-R14-H01..H22` | Invalid authentication is 401; post-auth unusable/duplicate identity is 403 when it reaches Claims context validation. Optional scope malformed=400/mismatch=403. UUID/header duplicates, Unicode-scalar key 128/129, comma data, six scope aliases, and ordinary unknown query behavior match the annex. |
| R15 | `E4-R15-O01..O09` | Precedence is authentication, trusted context/coarse grant, headers/query, media/body, target grant, receipt, aggregate/reference, root, business. Rejection correlation body/header agree; wrong-root plus changed payload returns root mismatch first. |
| R16 | `E4-R16-R01..R08` | Create and transition identical request replay original 201/200 with `idempotentReplay:true` and no write/reference read; changed payload gives 409 payload code; wrong root gives 409 root code first. |
| R17 | `E4-R17-00..48` | All 49 source×target pairs traverse the real HTTP middleware/handler/repository with a Mongo fixture aggregate: exactly seven legal arrows return 200; the other 42 return 422 `INVALID_CLAIM_TRANSITION`. |
| R18 | `E4-R18-E01..E18` | Declared application status/error/header shapes observed in this lane have exact safe message, `contractVersion:v1`, and matching response/body correlation; 401 carries `WWW-Authenticate: Bearer`; no raw token, exception or foreign identifier leaks. |
| R21 | `E4-R21-R01..R08` | Authoritative Shipment root missing/null/empty=503, malformed=502, mismatch=409, matching nil is valid. Trace/Shipment ID cannot substitute. A separate self-hosted real Shipment read establishes producer uptake; controlled root faults remain labelled fixture evidence. |

R19–R20 and R22–R26 are outside this lane and receive no blanket PASS. R08 process-boundary persistence is explicitly handed to `MOD0187-E4-PERSISTENCE-01`.
