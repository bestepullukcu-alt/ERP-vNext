# MOD-0184 / SHIPMENT-BUNDLE 1.1.0 — publication review package

2026-09-16 · Governance/contract-only · Risk HIGH · **NOT PUBLISHED**

## Verdict

**Review package READY. Publication NO-GO pending explicit publication authority and consumer sign-off.**
Static specification checks PASS. 1.1.0 is a **conditional minor-version candidate**, not an already
compatible/frozen release. GAP-184-04/05 remain **BLOCKED pending publication and verified consumer uptake**.
No runtime, pack promotion, HELD prompt mutation, commit, push or canonical contract write occurred.

## Exact artifacts and proposed write set

- [Candidate OpenAPI](shipment-bundle.openapi.candidate.yaml): info.version1.1.0, x-status CANDIDATE,
  x-publication-status NOT_PUBLISHED; wire contractVersion:v1 and base URL unchanged.
- [Exact canonical→candidate diff](shipment-bundle-1.0.0-to-1.1.0.candidate.diff).
- [Exact publication patch](publication-after-approval.patch): **DO NOT APPLY WITHOUT RELEASE AUTHORITY**.
  This is the concrete final diff for the publisher: info.version1.1.0 / x-status FROZEN, no candidate marker,
  plus the normative annex. Applying it was tested only in a disposable directory.
- [Normative annex](carrier-semantics-v1.1.0.md): verbatim owner decision §§2–7, including source aliases,
  full E01–E14 / RP01–RP16 policies. Candidate only until published with the YAML.
- [Error matrix](error-matrix.json), [44 schema-checked example exchanges](examples.json).
- [Reproducible validator](validate.py), [validation results](validation-results.json),
  [proposed published hashes](proposed-publication-hashes.json), [baseline](baseline.json).

Only these two canonical destinations are proposed for a later authorized publication WP:

1. `docs/analysis/contracts/shipment-bundle.openapi.yaml`
2. `docs/analysis/contracts/carrier-semantics-v1.1.0.md`

The sibling annex location is significant: all three Carrier operations explicitly reference it. Publish
both together; record both hashes. The package does not silently update contracts/README, registry, DCP,
other contracts or prompts. Any required catalog/provenance record is a separate explicit publisher-owned
change. This directory and its patch are review artifacts, not a canonical release. The annex retains its
conditional publication notice so possession of a candidate copy cannot imply release authority.

## Operation changes

| Operation | Existing responses preserved | Added responses |
|---|---|---|
| GET /carriers | 200 | 400,401,403,404,500,503 |
| POST /carriers | 201,409,422 | 400,401,403,404,415,500,503 |
| POST /carriers/{carrierId}/status | 200,404,422 | 400,401,403,409,415,500,503 |

All 27 Carrier responses now guarantee X-Correlation-Id by description; Header Object schema string/uuid,
without unsupported response `required` flags. All401 responses also guarantee `WWW-Authenticate: Bearer`.
Errors reference the unchanged Error schema; examples use the exact owner code/message matrix.
No optional details are produced by this slice; no outer envelope or ProblemDetails is added.
Create422 is retained, without inventing a new create business rule; its inherited baseline examples remain.
Status409 explicitly describes same-key/different-valid-payload conflict. No success fields are added.
All existing component objects, including CorrelationId/IdempotencyKey and shared response definitions,
are identical. New Carrier-only responses isolate the semantics from other bundle consumers.

Success and replay header = **current valid request correlation**; first committed mutation audit root =
**original correlation**. Replay returns original status snapshot with replay=true and original201/200;
no second audit/mutation and no rewriting original actor/root. This distinction is normative in the annex,
not a new success-body field. Failure/unknown commit recovery retains the same key.

## Header precedence and fallback examples

The full exact precedence is annex §4; all cases below are specification oracles, **not executed HTTP tests**.

| Combined condition | Expected response / correlation |
|---|---|
| Missing JWT + malformed correlation + malformed key | 401; one generated trace in header/body/log; no DB access |
| Valid JWT, permission missing + invalid correlation | 403; generated trace; no replay lookup |
| Authorized + invalid correlation/tenant/LE/key | 400 correlation-header message; generated trace |
| Valid tenant mismatch + invalid key | 400 key-header message; current valid correlation |
| Valid headers, tenant mismatch + malformed body | 404 CARRIER_NOT_FOUND; current correlation; no lookup |
| Valid context, invalid body + previously committed key | 400 schema error; no replay lookup |
| Exact committed replay with new valid correlation | create201/status200, replay=true; current response header, original persisted audit |

Nil and uppercase UUID values, parsed whitespace-only keys, commas in a single key, Unicode code-point
length1..128, empty reasonCode, duplicate modes and absent/null externalReference are covered by the static
boundary corpus. No shared-header tightening. HTTP parser/encoding restrictions are separate from schema
acceptance; no runtime transport claim is made. Duplicate correlation values cause fallback/rejection;
first auth/permission failure still wins. Annex excludes transport/router/server-startup failures from the
application JSON guarantee. A stopped process is not evidence of an application503 response.

## Versioning assessment

Policy: `docs/analysis/contracts/README.md:47–50` requires owner review, each consuming MVP review,
then freeze; only additive change may use minor after freeze. The optional-field/new-endpoint examples do
not automatically certify this response/behavior amendment. `.antigravity/rules/api-conventions.md:27`
concerns entity revision endpoints, not permission to choose a SemVer bump. Its generic error convention
does not override this frozen contract's Error shape. SOP §19.1 requires a new prompt v2.0 or superseding
WP for material contract/acceptance change; prompt version and OpenAPI version are different decisions.
SOP K16 is about silent prompt mutation, not a standalone proof of wire compatibility.

Measured schema facts supporting proposed minor: no request validation or required-field changes;
all request/response schemas and existing component objects unchanged; no path removed; no success status
removed; wire v1/base URL unchanged; new response/header guarantees isolated to Carrier.

**Behavioral compatibility is not proved by those facts.** Strict response allowlists/generated SDKs/mock
routers need the new statuses; clients might associate201 with a new mutation rather than replay, compare
correlation text case, or expect the original audit root in a replay response. Earlier unspecified semantics
may have consumer assumptions. Owner-selected error messages and precedence also need consumer review.
No existing consumer implementation is silently certified by schema-only checks. Thus 1.1.0 is appropriate
only if publisher and consumers confirm these are compatible additions. Otherwise stop minor release;
seek a separately approved versioned transition/major strategy. Do not overwrite frozen1.0.0 with a known
breaking amendment. Owner decision §§8–9 imposes the same condition.

## Consumer impact / independent review

| Consumer / owner | Measured effect | Release evidence still required |
|---|---|---|
| MOD-0183 Shipment/POD | Non-Carrier operations and shared components unchanged. No Shipment/POD contract behavior delta. Shipment stores opaque CarrierId; no Carrier HTTP client found. | MOD-0183 co-owner diff sign-off; future composition regression evidence |
| MOD-0184 Carrier / test/mock/SDK owners | New errors/headers/replay semantics are externally observable. No existing Carrier runtime/client located in services/frontend/gateway scan. | Consumer matrix covering all new statuses, header/precedence/replay cases, exact candidate hashes |
| MOD-0185 Routing/Load | Load schema/operations unchanged; potential future Carrier consumer cannot be assumed absent. | Co-owner review and mock/SDK conformance; no runtime/E5 claim |
| MOD-0186 Reverse Logistics | Return schema/operations unchanged. | Co-owner review and identification of Carrier dependencies |
| MOD-0187 Claims | Claim schema/operations unchanged. | Co-owner review and identification of Carrier dependencies |
| MOD-0140 Supplier | No schema/service/reference change. | No Supplier ownership transfer or invented relation |
| External/other bundle consumers | Repository search cannot enumerate external clients. | Publisher-owned consumer inventory and acknowledgements; no implicit consent |

Independent read-only agent reviewed policy, consumers and candidate: operation delta matches owner
record, no shared semantics leakage found. It flagged annex alias context and canonical destination;
resolved by including source §2 and an exact two-file publication patch/hash check. This is independent
static review, not runtime VER or consumer acceptance.

Separate **MOD-0183 implementation risk**, not a proposed contract change:

- `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/Middleware/ShipmentContextMiddleware.cs:9`
  currently intercepts the whole bundle; :11–15 rejects nil/checks correlation before auth; :31–34 trims/rejects blank keys.
- `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Persistence/Features/Shipments/ShipmentRepository.cs:54`
  rejects different-correlation replay.
- `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/Program.cs:45` shares invalid-model mapping;
  :52 registers Shipment middleware. Future Carrier composition must isolate approved Carrier paths rather than
  changing these accepted Shipment policies. No such source change is made by this package.

## Reproduction and evidence

```sh
python3 -m venv /private/tmp/mod0184-oas-tools
/private/tmp/mod0184-oas-tools/bin/pip install openapi-spec-validator==0.7.2 jsonschema==4.25.1 PyYAML==6.0.3
/private/tmp/mod0184-oas-tools/bin/python docs/records/audits/2026-09/mod-0184-contract-publication-v1.1.0/validate.py
git apply --check docs/records/audits/2026-09/mod-0184-contract-publication-v1.1.0/publication-after-approval.patch
git diff --check
```

Actual validator exit0. OpenAPI3.1 canonical and candidate PASS; local refs167/238 resolved; schema-checked
inline examples46/93; original5 Carrier examples preserved; 44 exchange oracles schema-valid; boundary corpus
PASS; exact response inventory PASS. Ten non-Carrier paths and every existing component unchanged.
The validator applies the publication patch **only to a temporary copy**, validates final1.1.0/FROZEN shape,
both proposed hashes and annex resolution. Canonical files remain untouched. `git apply --check` exits0.
First pip attempt failed under restricted networking; installation then succeeded in the temporary venv
with approved network execution. A LibreSSL urllib3 warning did not affect local validation; no remote refs.
No service build, HTTP, Mongo, failure-injection or live consumer execution was performed.

## Baseline, owned inventory and next authority gate / SOP §22

Branch `feature/mvp6-logistics`; HEAD `4a8d4d4b339528a88e6220fb8402e5a2c771136c`; staged empty.
Pre-existing dirty pack and untracked PREP/owner/runtime-gate/CT/continuation/HELD documents preserved.
Task owns only this new directory. Fresh pre-write14262-file hash baseline is in
`/private/tmp/mod0184-publication-baseline.json`; preservation results and artifact inventory are delivered
in preservation-results.json and manifest.json. Those are static audit measurements, not E4 evidence.
No existing file changed. Canonical SHA remains
`f6415bbfda42a61a9845e7e1fc843be087bf249cac6bcdc9cb678284766450a1`.

Required release authority: explicit SHIPMENT-BUNDLE publication authorization naming the final patch,
both target paths/hashes and single publisher; contract/security owner sign-off plus identified consuming
MVPs' conformance/approval under README freeze process. After publication, verify consumers actually use
the approved version/hash and record GAP04/05 closure in a new CT record. Only then may central CT
reevaluate Phase1.5, pack ready-for-dev, Program.cs exception and a new versioned dispatch.

ASSUMPTION-PUB-184-01: local absence of Carrier clients does not imply external consumer absence.
ASSUMPTION-PUB-184-02: this task authorizes review artifacts, not canonical publication.
Agent verdict: package prepared; publication NO-GO; runtime/evidence acceptance not evaluated.
Historical MOD-0183 architecture15 PASS /3 deferred external FAIL remains unchanged and is not rerun/waived.
