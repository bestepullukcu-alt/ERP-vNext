<!-- verify from: this folder -->
# MOD-0140 exists twice, in two unrelated repositories — a decision is required

    lane      : mod-0140-dual-implementation-01
    measured  : 2026-10-11
    consumer  : feature/mvp6-logistics @ 2acce233b (ERP-vNext-recovery)
    producer  : ERP-vNext @ c165eb24203024ec2ad4acd0f4e7579b9e114d0d (self-reported)
    verdict   : CONFIRMED FORK — one canonical module ID, two divergent systems of record
    decision  : CT / owner. This record is evidence, not a ruling.

## The decision this record exists to support

**Which repository is the MOD-0140 system of record?** Until that is answered, two Supplier
SoRs sit behind one opaque `supplierId`, in two different domains, and the MVP-6 packs point at
a frozen contract that only one of them implements.

This is a DCP-002 identity question — one ID, one capability — not a technical comparison.
Nothing below should be read as authorising a migration in either direction.

## They share no lineage — measured

| check | result |
|---|---|
| producer's four cited commits `c165eb242`, `13dabce96`, `d01cd999b`, `83bb8f519` | **none exist in this repository** (`git cat-file -t`) |
| `Diten.Platform.API/Controllers/SupplierOnboardingController.cs` | absent here |
| `Diten.Platform.Domain/Entities/Mod0140/SupplierOnboardingEntities.cs` | absent here |
| `platform-shared-services/module-packs/MOD-0140-supplier-onboarding-kyc-sanctions-docs.md` | absent here |
| `execution/domains/supplier-procurement-governance/` | absent here |
| `Mod0140` namespace in any service here | **0 occurrences** |

Not a stale copy and not a diverged branch. Two independent builds of the same module ID.

## Identity and placement conflict

| | producer (`ERP-vNext`) | this repository (`ERP-vNext-recovery`) |
|---|---|---|
| pack | `platform-shared-services/…/MOD-0140-supplier-onboarding-kyc-sanctions-docs.md` | `procurement/module-packs/MOD-0140-supplier-onboarding.md` |
| pack status | `done`, release **conditional** | `ready-for-dev` |
| domain | platform-shared-services; taxonomy target `supplier-procurement-governance` | **procurement** |
| service | `Diten.Platform.API` | `Diten.ProcurementService` |
| port | **5057** | **5065** |
| route | `/api/v1/supplier-onboarding` | `api/suppliers` |
| named owner | `ai-orchestrator` (agent label, producer notes it is not an accountable human) | `procurement / control-tower`; DCP-010 approved by product owner Ali |
| frozen SUPPLIER OpenAPI | **absent** — producer searched 134 candidate contract/openapi files, no match | **present**: `docs/analysis/contracts/supplier.openapi.yaml`, v1.2.0, `x-status: FROZEN`, `x-owner: MOD-0140` |
| DCP-002 identity gate | "could not verify — no producer-side Blueprint/DCP mapping located" | **passed 2026-09-16**; registry row assigns owner domain `procurement`, DCP-010 |
| author / date | producer lane | Cihan, 2026-09-16 (`03499a3e8`, `93f030eb1`, `74e04c454`) |

The asymmetry that matters for identity: **this repository holds the canonical registry row, the
passing DCP-002 preflight and the frozen owned contract. The producer holds neither a Blueprint
mapping it could locate nor an OpenAPI artifact**, and places the same ID in a different domain.

## Operation parity — 4 of 9 align semantically

| contract `operationId` | here | producer |
|---|---|---|
| `listSuppliers` | `GET api/suppliers` | `GET /suppliers` |
| `getSupplier` | `GET {supplierId}` | `GET /suppliers/{supplierId}` |
| `getOnboardingCase` | `GET {supplierId}/onboarding` | `GET /cases/{caseId}` |
| `submitOnboardingCase` | `POST {supplierId}/onboarding` | `POST /cases/{caseId}/submit` |
| `createSupplier` | `POST` | **absent** — `POST /cases` creates a governed case |
| `updateSupplier` | `PATCH {supplierId}` | **absent** — `PATCH /cases/{caseId}/draft`; approved overwrite rejected |
| `deleteSupplier` | `DELETE {supplierId}` | **absent** |
| `bulkDeleteSuppliers` | `DELETE bulk` | **absent** |
| `validateSuppliers` | `POST validate` | **absent** — `POST /preconditions:validate` is a dry-run of onboarding *actions*, not bulk supplier eligibility |

Route shapes differ for all nine. The four "aligned" rows align in *semantics*, not in path,
and the producer is explicit that it claims no `operationId` metadata parity.

## Capability asymmetry — neither is a superset

| capability | here | producer |
|---|---:|---:|
| frozen OpenAPI authority | **yes** | no |
| supplier CRUD incl. delete + bulk-delete | **yes** | no |
| SoD decision gateway | no (0 hits) | **yes** |
| KYC/sanctions screening flow | no (0 `Screening` hits; KYC/sanctions exist as *fields* only) | **yes** (persists `Pending`, tagged `mock-screening-v1`) |
| publication / AP projection | no (0 hits) | **yes** (local bookkeeping only; producer states it marks Published without outbound delivery) |
| approve / reject / request-info / escalate | no | **yes** |
| preconditions dry-run | no | **yes** (`pass_ready` / `blocked` / `dependency_unavailable`) |
| live MOD-0220 LegalEntity relationship validation | LegalEntity present (54 refs) but no MOD-0220 gateway validator | **yes**, through Gateway, fails closed |
| controller HTTP actions | 9 | 20 |

**Migrating in either direction loses real capability.** The producer is materially further along
on regulated governance; this repository is the contract-complete one.

## Convergent finding for MVP-6 — the DC-02 / DC-03 seam exists in neither

The producer states plainly that it does **not** provide: an authoritative
`(validated issuer, stable subject, trusted TenantId, trusted LegalEntityId) → SupplierId`
binding; binding identity/revision with active/revoked state; one-active-binding cardinality;
commit-time revocation fencing; or a no-write/no-receipt/no-event guarantee. Its
`preconditions:validate` is, in its own words, *"neither a supplier binding nor a commit-time
authorization receipt"*, and its taxonomy precondition is wired to a no-op gateway.

The same gap holds here: no affirmative external eligibility endpoint and no actor→supplier
binding exists in `Diten.ProcurementService` either.

**So DC-02 and DC-03 are unblocked by neither implementation.** Two independent codebases
reaching the same conclusion is stronger evidence than either alone, and it corroborates the
2026-10-05 CT ruling that each of those items requires a new producer seam rather than a
signature.

## Defect cross-check — producer disclosed two; measured against ours

**1 · Error envelope.** The producer reports most controller error branches use
`Response<string>.Ok(code, message)`, so HTTP 400/403/404/409 can carry `succeeded=true` with the
code in `data`, and warns consumers that `succeeded` alone is unsafe.

*Here:* `SuppliersController.cs` contains **no hand-rolled error returns at all** — no
`BadRequest`, `NotFound`, `Conflict`, `Forbid` or `StatusCode` call. Mapping is centralised in
`CustomBaseController` plus the mandated exception pipeline behaviour, so the defect does not
reproduce in the same form. **Not verified:** our actual HTTP/envelope mapping end to end.

**2 · Side effects before the concurrency check.** The producer reports
`ExecuteSupplierWorkflowActionCommandHandler.Handle` performs workflow transition, publication and
supplier update *before* the final `UpdateCaseAsync(… ExpectedVersion)`, and that
`UpdateSupplierAsync`'s boolean result is ignored — so a later concurrency failure cannot be read
as "no preceding side effect".

*Here:* `SubmitOnboardingCaseHandler.cs` (84 lines) does **not** have it, and structurally cannot:

    line 21  await _repository.GetBySupplierIdAsync(...)      ← read
    line 34  var expectedVersion = entity.Version;            ← captured before mutation
    lines …  in-memory mutation only, no awaits
    line 72  var ok = await _repository.UpdateAsync(entity, expectedVersion, ...)   ← single CAS
    line 73  if (!ok) return Response<...>.Fail("VALIDATION_FAILED", 422);          ← result CHECKED

Exactly two awaits — one read, one compare-and-swap write — and the CAS result is acted on. The
sole injected dependency is `ISupplierRepository`; there is no publication, audit, workflow or SoD
gateway that *could* fire before the version check.

The honest reading: ours is clean because it does less. The producer's defect is reachable
precisely because the producer implemented the outbound gateways this module will eventually need.

## Options, with what each costs

These are inputs for CT, not a recommendation on identity.

**Option 1 — this repository is the SoR.** Keeps DCP-002 compliance, the canonical registry row,
the frozen contract and the delete surface MVP-6's consumers may reference. Cost: SoD, screening,
publication, approve/reject/escalate, preconditions dry-run and the MOD-0220 validator must be
built here, all of which exist and work in the producer.

**Option 2 — the producer is the SoR.** Keeps the mature governance implementation. Cost: the ID
sits in `platform-shared-services` against a registry that assigns it to `procurement`; no frozen
contract exists, so MVP-6's six `CONSUMES: SUPPLIER-BASE` declarations lose their referent; and
`deleteSupplier` / `bulkDeleteSuppliers` / `validateSuppliers` would have to be added or the
contract narrowed — which K16 in our pack forbids ("daraltma yasak").

**Option 3 — split the capability under two IDs.** Requires a DCP-002 reservation and a new
canonical name; makes the boundary explicit instead of implicit. Cost: a governance cycle, and
MVP-6's consumer declarations must be re-pointed.

**What no option permits:** copying code between the repositories to "reconcile" them. Both sides
would fail the receiving repository's gates, and a second SoR behind one opaque `supplierId` is
the risk this record exists to surface, not to resolve by merge.

## What I verified, and what I did not

**Measured here, directly:** absence of all producer paths and commits; our pack frontmatter,
domain, service and status; the registry row and DCP-002 result; the contract's version, status,
owner and nine `operationId`s; our controller's nine HTTP actions; our feature-file inventory;
capability hit counts; `SubmitOnboardingCaseHandler` await ordering and CAS handling; the absence
of hand-rolled error returns.

**Not verified here:** that our tests pass; that `Tenant + LegalEntity + IsDeleted=false` filtering
is implemented in every repository method rather than documented in the interface comment; our
end-to-end HTTP/envelope mapping; whether an affirmative LE *eligibility* result exists anywhere
in our service as distinct from LE-scoped filtering.

**Not verified at all — the producer's half is its own self-report.** I did not inspect
`/Users/natig/Projects/ERP-vNext`. The disclosure is commit-pinned, candid about its own limits
(no build, no tests executed, port probes blocked by its sandbox) and its SHA-256 matches its own
manifest, so I treat it as reliable. It remains a disclosure, not my measurement. Its manifest
records the authoritative-contract digest as UNAVAILABLE, which is a finding on the producer side,
not a gap in this record.

The disclosure is vendored unedited at
`docs/vendor/MOD-0140-SUPPLIER-BASE-disclosure-v1.0/` per docs-organization K5, with a
`PROVENANCE.txt`, so every citation in this record resolves inside this repository.

## Compliance

Nothing was staged or committed. No source file, pack, contract or configuration was modified in
either repository. The producer repository was read only. Two new paths were created here: this
record and the vendored disclosure folder.
