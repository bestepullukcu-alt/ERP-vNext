<!-- verify from: this folder -->
# MOD-0220 exists twice — and unlike MOD-0140, the two halves are complementary

    lane      : mod-0220-dual-implementation-01
    measured  : 2026-10-11
    destination: ERP-vNext-recovery @ 2acce233b
    producer   : ERP-vNext (paths and commits measured directly, read-only)
    verdict   : CONFIRMED FORK — but NOT a competing-SoR fork. The two implementations
                cover different halves of one module.
    decision   : CT / owner. This record is evidence, not a ruling.

## Why this differs from MOD-0140, in one line

MOD-0140 was two systems of record competing for one ID in **different domains**. MOD-0220 is two
implementations of the **same** module, in the **same** domain, under the **same** canonical name,
each having built the half the other did not.

## 1 · No shared lineage — same test as MOD-0140

| check | result |
|---|---|
| their `contracts/openapi/mod-0220.openapi.yaml` last changed | `d01cd999` 2026-04-27 — **not in this repo** |
| their `LegalEntitiesController.cs` last changed | `13dabce9` 2026-05-04 — **not in this repo** |
| `contracts/openapi/mod-0220.openapi.yaml` here | **absent** |
| `services/DitenMdmService` here | **absent** |

Two independent builds. No common ancestor for these artifacts.

## 2 · Identity and ownership — AGREEMENT, which is the headline

| | destination | source | Blueprint |
|---|---|---|---|
| id | MOD-0220 | MOD-0220 | — |
| name | Corporate Secretarial / Entity Management | Corporate Secretarial / Entity Management | **Corporate Secretarial / Entity Management** |
| domain | master-data-management | master-data-management | Legal Governance (capability group) |
| service | `Diten.MdmService` | `DitenMdmService` | — |
| status | **ready-for-dev** | **draft** | — |
| owner | `mdm-domain-team` (a team) | `ai-orchestrator` (agent label) | — |
| wave | — | — | W-4 |
| gate | — | — | **Records Management (MOD-0030)** |

**All three agree on the canonical name and the domain.** That is the opposite of MOD-0140, where
the source placed the ID in `platform-shared-services` against a registry assigning it to
`procurement`. There is no identity dispute here to resolve.

Two asymmetries worth recording rather than deciding:

- **Status and ownership favour the destination**: `ready-for-dev` with a named team, versus `draft`
  with an agent label the source's own MOD-0140 disclosure flagged as "not an identified human
  accountable owner".
- **Pack filename favours the source.** Theirs is
  `MOD-0220-corporate-secretarial-entity-management.md`; ours is
  `MOD-0220-legal-entity-foundation.md`. The canonical name is "Corporate Secretarial / Entity
  Management", so **our filename is the one that diverges from it.** Reported, not corrected — the
  pack standard's §1 File Naming governs and this is a destination housekeeping item.

- **MOD-0220's own Blueprint gate is Records Management (MOD-0030), which is not reserved here.** So
  MOD-0220 has an unmet gate on the destination side independent of this fork — relevant because
  MOD-0289 depends on MOD-0220.

## 3 · Contract — the inverse of MOD-0140

| | |
|---|---|
| source | `contracts/openapi/mod-0220.openapi.yaml` · title *MOD-0220 Corporate Secretarial / Entity Management API* · **version 1.0.0** · **14 operations** · sha256 `c18d6f98c127b148e6220fcae7ec43140e90aecdb18db06797be34d262c01a0a` · **no `x-status`, no `x-owner`** |
| destination | **none.** No MOD-0220 contract exists in `docs/analysis/contracts/`, and no destination contract references MOD-0220 |

For MOD-0140 the destination held the frozen contract and the source had none. **Here it is
reversed.** Note also that their contract carries no `x-status`, so it is not marked frozen — it
cannot be treated as a frozen seam the way `supplier.openapi.yaml` can.

Their 14 declared operations: `listLegalEntities`, `createLegalEntity`, `getLegalEntityReferenceData`,
`importLegalEntities`, `getLegalEntityById`, `updateLegalEntity`, `getLegalEntityHierarchy`,
`getLegalEntityRelationships`, `createEntityRelationship`, `updateEntityRelationship`,
`listCorporateActions`, `createCorporateAction`, `getCorporateActionById`, `updateCorporateAction`.

**A declared-versus-implemented gap on the source side:** their contract declares create/update
operations, but the controller surface sampled is **13 GET endpoints** with no POST/PUT among them.
Per TP-001 T5 that is *declared* but not demonstrably *implemented*, and their packet should state
it per operation.

## 4 · Surface comparison — the two halves

### Destination — lifecycle-complete, relationship-absent

`api/legal-entities`, every action permission-guarded:

    GET    /                                 mdm.legal-entities.read
    GET    /lookup                           mdm.legal-entities.read
    GET    /{legalEntityId}                  mdm.legal-entities.read
    GET    /{legalEntityId}/lookup-validation  mdm.legal-entities.read
    POST   /                                 mdm.legal-entities.create
    PUT    /{legalEntityId}                  mdm.legal-entities.update
    PATCH  /{legalEntityId}/activate         mdm.legal-entities.update
    PATCH  /{legalEntityId}/suspend          mdm.legal-entities.update
    PATCH  /{legalEntityId}/archive          mdm.legal-entities.update
    DELETE /{legalEntityId}

plus a second controller at `api/legal-entities/lookups/{type}`.

### Source — relationship and reporting-rich, lifecycle-thin

    GET /  ·  /{id}  ·  /{id}/hierarchy  ·  /{id}/relationships  ·  /{id}/corporate-actions
    GET /{id}/corporate-actions/{corporateActionId}  ·  /{id}/documents  ·  /{id}/filings
    GET /reference-data  ·  /reports/completeness  ·  /reports/filings
    GET /reports/status  ·  /reports/hierarchy

### Measured capability split

| capability | destination | source |
|---|---|---|
| full lifecycle — activate / suspend / archive / delete | **yes** | not in the sampled surface |
| `[HasPermission]` on every action | **yes** | not sampled |
| `lookup-validation` endpoint | **yes** | — |
| **entity relationships** | **`Relationship` 0 files** | **yes — 3 contract ops + GET endpoint** |
| **corporate actions** | **0 files** | yes — 4 contract ops + 2 endpoints |
| **filings** | **0 files** | yes |
| hierarchy | 1 file | yes — contract op + endpoint + report |
| reporting surface | — | 4 report endpoints |
| bulk import | — | `importLegalEntities` |
| tenant / soft delete depth | TenantId **41** · IsDeleted **19** · DeletedAt **9** | not measured |
| eligibility primitives | EffectiveFrom **11** · EffectiveTo **9** · Approved **11** · Active **27** · Eligib **2** | not measured |

**Neither is a superset. The halves barely overlap.**

## 5 · Treasury-required eligibility — the finding that matters

MOD-0289 Bank Master's Blueprint gate list includes *Corporate Secretarial / Entity Management*. And
the source's own MOD-0140 disclosure describes what its LE validator requires: the legal entity
`ACTIVE`/`APPROVED`, an **approved relationship**, an **effective date window**, and **the
relationship touching that LE** — failing closed when MOD-0220 is unavailable.

So Treasury-grade eligibility is **relationship-based**, and:

> **The destination has zero relationship files.** `Relationship` matches 0 files across
> `Diten.MdmService/src`.

The destination holds the LE-level primitives — `Active` 27, `Approved` 11, `EffectiveFrom` 11,
`EffectiveTo` 9, and a `lookup-validation` endpoint — but **no entity-to-entity relationship model at
all.** Treasury eligibility as the source defines it cannot be evaluated here today.

This is the single most consequential difference between the two implementations, and it was
invisible in the earlier "47 LegalEntity files" count.

## 6 · What to retain, what to adapt

### Retain the destination's behaviour

1. **The lifecycle surface** — activate / suspend / archive / delete. Not present in the source's
   sampled surface, and MOD-0220's SoR per the Blueprint is *legal entities, filings, corporate
   actions, statutory evidence packs*, which implies lifecycle ownership.
2. **`[HasPermission]` on every action** — the destination's mandatory standard (§6 UAS-001/RBAC).
3. **The `lookup-validation` endpoint** — the existing eligibility primitive, and the natural place
   to extend for relationship-aware eligibility.
4. **Tenant and soft-delete depth** — TenantId 41, IsDeleted 19, DeletedAt 9.
5. **`ready-for-dev` status and the named team owner.**

### Adapt from the source — one capability, and it is the Treasury blocker

1. **The entity relationship model** — `getLegalEntityRelationships`, `createEntityRelationship`,
   `updateEntityRelationship`, plus whatever carries approval state and the effective-date window.
   **This is what Treasury eligibility requires and the destination has none of it.**
2. **The contract** — the destination has no MOD-0220 contract. Theirs is a candidate basis, but it
   carries no `x-status`, so it must be reviewed and frozen here rather than adopted as frozen.

### Defer, pending scope decision

Corporate actions, filings, documents and the four report endpoints are Legal Governance scope, not
Treasury-required. They are real capability the destination lacks, but **MOD-0289 does not need
them**, so they should not ride in on a Treasury-driven intake without their own decision.

## 7 · The resolution is not "pick a winner"

MOD-0140 needed an SoR ruling because two implementations claimed the same system of record in
different domains. **MOD-0220 does not.** Both sides agree on identity, name and domain; the
destination is further along in governance (`ready-for-dev`, named owner, permission-guarded,
lifecycle-complete); and the source holds one capability the destination lacks and Treasury needs.

Recommended framing for the owner, as an input and not a decision: **the destination is the system of
record; the source's relationship model is an adaptation target, scoped to what Treasury eligibility
requires.** That is a module-pack amendment plus a contract authoring task here, not a migration.

## 8 · What I verified, and what I did not

**Measured directly:** absence of the source's commits and paths here; both packs' frontmatter;
the destination registry's canonical name; the Blueprint's name, wave, group, gate and SoR for
MOD-0220; the source contract's title, version, operation list and SHA-256, and the absence of
`x-status`/`x-owner`; both controllers' route and permission surfaces here; the source controller's
route surface; and the destination capability counts in §4.

**Not verified:** the source's tenant, soft-delete, permission and eligibility depth — its half of
§4 is blank because measuring it belongs in its packet, not in a destination record; whether the
source's declared create/update operations are implemented anywhere outside the sampled controller;
whether the destination's `lookup-validation` endpoint can be extended to relationship-aware
eligibility without a schema change; and MOD-0030 Records Management, MOD-0220's own unmet Blueprint
gate here.

**Nothing was changed in either repository.** No file was modified, staged or committed; the source
repository was read only.
