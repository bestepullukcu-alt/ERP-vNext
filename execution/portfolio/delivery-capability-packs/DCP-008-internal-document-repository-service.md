---
id: DCP-008
slug: internal-document-repository-service
name: Internal Document Repository Service
type: Delivery Capability Pack
standard: CAP-001
status: approved
status_note: "CT approved 2026-09-07 (approved / ready-for-execution per CAP-001 §5). OD-1…OD-6 resolved or decided — see §18."
approved_by: control-tower
approved_on: 2026-09-07
owner_domain: platform-shared-services
owner: platform-shared-services / enterprise-architect (EA items)
created: 2026-09-07
authoring_branch: feature/structured-content-messaging
canonical_source: "docs/System Capability & Implementation Blueprint - master 8.1.xlsx#Blueprint_Data"
canonical_module: MOD-0262
runtime_code_allowed: true
runtime_code_scope: "MOD-0262-FU01 ONLY, started by @orchestrator via /add-module. Every other member (FU02…FU07) remains planned/unreserved and authorizes no code."
contract_status: BLOCKED-on-EA-BP-003
inputs:
  - "execution/registries/module-id-registry.md (MOD-0262 canonical + Retired Reference rows)"
  - "execution/portfolio/blueprint-master-plan-reconciliation.md (MOD-0262 reclassification row; §Blueprint Internal-Consistency Correction Requests EA-BP-001…005)"
  - "docs/analysis/mod-0262-internal-document-repository-prd-and-impact-analysis.md (PRD + measured impact map)"
  - ".antigravity/scripts/verify_module_id.py (DCP-002 fail-closed gate — MOD-0262 exit 0, MOD-0262-FU01 exit 0)"
  - ".antigravity/rules/capability-pack-standard.md (CAP-001)"
  - ".antigravity/rules/permission-key-standard.md (PKS-001)"
---

# DCP-008 — Internal Document Repository Service (Delivery Capability Pack)

> **Artifact and status guard.** This is a CAP-001 **Delivery Capability Pack**: a governance and orchestration contract. It is **NOT** a runtime entity, **NOT** a Module Pack, **NOT** a MOD-0014 runtime Capability Group, and **NOT** a business-capability-matrix row.
>
> **`status: approved` (CT, 2026-09-07).** Per CAP-001 §7 both conditions for implementation are now met: this pack is approved **and** `MOD-0262-FU01` is `ready-for-dev`. Implementation is therefore authorized **for `MOD-0262-FU01` only**, and only through `@orchestrator` / `/add-module`. **FU02…FU07 remain planned and unreserved; this approval authorizes no code for them.**
>
> **Identity guard.** This pack **mints no new `MOD-xxxx`**. `MOD-0262` remains the single canonical module (DCP-002 §5 pattern); every member below is either `MOD-0262` itself or a `MOD-0262-FUxx` child. Only `MOD-0262-FU01` is reserved at this time — the remaining FU identities are **planned, not reserved**, and each is reserved at its own pack-authoring time through the DCP-002 preflight.
>
> ⛔ **Contract guard.** The integration contract `DOC-REPO-BUNDLE` is **undefined in the SoT** (see §4a). Every member FU whose scope depends on that contract is barred from `ready-for-dev` until **EA-BP-003** closes. `MOD-0262-FU01` is deliberately scoped to survive this blocker.

---

## 1. Identity and status

| Field | Value |
|---|---|
| ID | DCP-008 |
| Slug | internal-document-repository-service |
| Type | Delivery Capability Pack (CAP-001) |
| Status | **`approved`** — CT, 2026-09-07 (approved / ready-for-execution) |
| Canonical module | **MOD-0262 — Internal Document Repository Service** |
| Owner domain | platform-shared-services |
| Canonical source | Blueprint 8.1 `Blueprint_Data` (Domain-1 Platform & Shared Services, Suite "Documentation & Evidence Services", Capability Group "Internal Document Repository") |
| Wave / Release | W-1 / `R1 - PPM MVP`, Release Candidate = **Y** |
| Deployment Unit | Platform Document Service (target); **R1 ships in-process inside `Diten.Platform`** — see AD-3 |
| Build / Buy | Build |
| SLO Tier | Tier 2 |
| Integration contracts | `DOCS-BASE + DOC-REPO-BUNDLE` — ⛔ both **undefined** in the Contract Bundle Dictionary (EA-BP-003) |
| SoR | Document binaries; evidence packages; repository objects |
| Soft pages (Blueprint) | Repository Admin · Evidence Package Storage · Document Binary Store · Repository Health |
| DCP-002 gate | ✅ `MOD-0262` exit 0 · ✅ `MOD-0262-FU01 --parent MOD-0262` exit 0 |
| Authority note | Does not alter the AGENTS.md §1 authority hierarchy. Blueprint remains the canonical authority for MOD identity and capability naming. |

---

## 2. Business outcome

A **native, internal system of record for document binaries and evidence packages**, with no external storage dependency (Blueprint Aim/Goal). On completion the platform can, for the first time:

- store and retrieve controlled-document binaries behind an owned, authorising service boundary;
- assemble and retain **evidence packages** — a capability that does not exist anywhere in the repository today;
- **actually execute** approved destruction decisions, closing the retention loop that MOD-0029-FU15 explicitly leaves open;
- resolve content pointers end-to-end so downstream consumers (MOD-0162, MOD-0313) can read what they reference;
- report repository health (orphan metadata ↔ orphan binaries) instead of discovering drift by accident.

---

## 3. Problem statement

There is **no enterprise owner of the document binary** in ERP-vNext. Measured state (`docs/analysis/mod-0262-internal-document-repository-prd-and-impact-analysis.md`):

1. Binary storage exists only as MOD-0029-FU01's `IContentStorageGateway` seam plus one `LocalFileSystemContentStorageGateway`, and the interface lives **inside a consumer's feature folder** (`Diten.Platform.Application/Features/DocumentManagementControlledDocuments/Services/`), not in a shared contract layer.
2. `EvidencePackage` has **zero occurrences** across `services/` and `execution/`, while `MOD-0313` HARD-depends on it.
3. `DocumentDispositionService.cs:10-18` states in code that it *"NEVER deletes anything … Actual destruction is a deliberate future task"* — an ownerless, written-down handoff.
4. MOD-0162 `KnowledgeContent` carries four untyped `string?` pointers (`ContentBodyRef`, `ContentAssetRef`, `FileRef`, `Url`) with **no resolver anywhere** — the content chain does not close.
5. The seam's contract is in-process-shaped: `ContentStoreRequest.Content` is a **`byte[]`**, and its XML doc delegates authorisation to the caller (*"Caller is responsible for all permission checks"*) — both assumptions break at a service boundary.

MOD-0028 and MOD-0313 are recorded **HARD** dependants of MOD-0262 in `Dependencies_Normalized`, so this gap blocks two downstream modules.

---

## 4. Capability boundary

**In scope**
- The system of record for **document binaries**, **evidence packages** and **repository objects**.
- The owned storage abstraction (`IContentStorageGateway` / `ContentRef`) and its provider implementations.
- Authorisation, tenant isolation and audit **at the repository boundary**.
- Physical destruction execution, consuming MOD-0029-FU15 disposition markers.
- Repository operational surfaces: Repository Admin, Repository Health, Document Binary Store, Evidence Package Storage.
- The consumer-facing resolve/download contract used by MOD-0028, MOD-0162 and MOD-0313.

**Out of scope**
- Document **metadata, lifecycle and governance** — MOD-0028 / MOD-0029 keep those; this pack never becomes a second document register.
- Retention **policy and legal-hold decisions** — MOD-0030 / MOD-0029-FU15 own the decision; MOD-0262 only executes.
- **Metadata** destruction — MOD-0030.
- Object ↔ evidence **linking semantics** — MOD-0031.
- **External** document repository connectors (SharePoint / M365 / Drive) — removed from this ID by Blueprint v6.1; see the `Retired Reference` registry row.
- Transient file transfer (CRM CSV import/export, audit export) — produced/consumed in-flight, never a stored repository object.
- ⚠️ **`EnterpriseStrategyService/UploadsController`** — an unmanaged second store that also appears to be **unauthenticated**. This is CT decision **D-5**, an independent security lane, and is **explicitly outside this pack**. See §17.

### 4a. Integration contract — `DOC-REPO-BUNDLE` · ⛔ `BLOCKED-on-EA-BP-003`

> **This subsection is intentionally left undefined.**
>
> Blueprint 8.1 mandates `DOCS-BASE + DOC-REPO-BUNDLE` for MOD-0262, but the workbook's **`Contract Bundle Dictionary` sheet contains no row for either code** (only `EXT-BASE` exists, itself a residue of the pre-v6.1 external-provider era — EA-BP-002). The dictionary is EA/SoT-owned; this repository raises requests against it and never edits it.
>
> **Consequence — enforced, not advisory:**
> - No member FU may define, name, or freeze the `DOC-REPO-BUNDLE` payload here.
> - FUs whose scope *is* the contract (**FU04**, **FU06**) **cannot reach `ready-for-dev`** until EA-BP-003 closes.
> - **FU01 is deliberately scoped to not require it** (see AD-1) so the capability is not fully stalled.
>
> Tracking: `execution/portfolio/blueprint-master-plan-reconciliation.md` § *Blueprint Internal-Consistency Correction Requests* → **EA-BP-003**.

---

## 5. Member modules and follow-ups

Referenced **by ID only**. This pack never replaces a module pack; each member passes its own `module-pack-standard.md` gate separately.

| Member | Scope | Soft page(s) owned | Wave / Release | Depends on | Reserved? | `ready-for-dev` eligible? |
|---|---|---|---|---|---|---|
| **MOD-0262-FU01** — Document Binary Store | Relocate the seam to a shared contract layer; stream/multipart contract; authorisation **at the boundary**; tenant-isolated object keys; audit. In-process (AD-3). | — (backend-only) | **W-1 / R1-PPM MVP** | MOD-0018, MOD-0021 only | ✅ **yes** (DCP-002 exit 0) | ✅ **YES — the only one** |
| MOD-0262-FU02 — Repository Admin | Provider configuration, storage-scope administration, binary registry listing. | Repository Admin · Document Binary Store | W-1 / R1 (stretch) | FU01 | ❌ planned | No — after FU01 |
| MOD-0262-FU03 — Repository Health | Two-way orphan reconciliation (orphan metadata ↔ orphan binary), read-only. | Repository Health | W-1 / R1 (stretch) | FU01 | ❌ planned | No — after FU01 |
| MOD-0262-FU04 — Evidence Package Storage | Evidence package assembly, checksummed package-as-object, audit. **Net-new capability.** | Evidence Package Storage | **W-4** (aligned to MOD-0031) | FU01, MOD-0031, **EA-BP-003** | ❌ planned | ⛔ **No — contract blocked** |
| MOD-0262-FU05 — Physical Disposition Execution | Consume MOD-0029-FU15 markers; re-verify holds **at execution time**; irreversible destruction + destruction evidence. | — | **W-3** (aligned to MOD-0030) | FU01, MOD-0030 | ❌ planned | No — dependency gap |
| MOD-0262-FU06 — Consumer Cutover (MOD-0162) | D-4: pointer-resolution / download contract consumed by CRM Knowledge; retires the four untyped pointers. | — | W-2+ | FU01, **EA-BP-003** | ❌ planned | ⛔ **No — contract blocked** |
| MOD-0262-FU07 — Service Extraction | D-6: lift the in-process boundary out into the standalone `Platform Document Service` deployment unit. | — | post-R1 | FU01…FU05 | ❌ planned | No — deferred by AD-3 |

**Consumers (not members):** MOD-0028 (HARD), MOD-0313 (HARD), MOD-0029, MOD-0162, MOD-0117/PPM (link pattern per DCP-003).

---

## 6. Ownership map

| Concern | Owner |
|---|---|
| Binary/evidence SoR, storage abstraction, destruction execution | **MOD-0262** (this pack) |
| Document metadata, versioning, controlled-document lifecycle | MOD-0028 / MOD-0029 |
| Retention policy, legal hold, disposition **decision** + marker | MOD-0029-FU15 → MOD-0030 |
| Metadata destruction | MOD-0030 |
| Object ↔ evidence linking | MOD-0031 |
| Authorisation primitives / permission catalog | MOD-0018 (`platform.*` namespace per PKS-001 §4) |
| Audit events | MOD-0021 |
| Blueprint workbook corrections (EA-BP-001…005) | **Enterprise Architect** — not this pack |
| `UploadsController` security lane (D-5) | CT — separate lane |

---

## 7. Dependency graph

```text
MOD-0018 (RBAC/ABAC, W-1) ─┐
MOD-0021 (Audit, W-1) ─────┴─→ MOD-0262-FU01  (W-1, R1)  ◄── the only unblocked entry point
                                    │
                    ┌───────────────┼───────────────┬──────────────────────┐
                    ▼               ▼               ▼                      ▼
              FU02 Admin      FU03 Health     FU05 Purge             FU06 CRM cutover
              (W-1)           (W-1)           (W-3, needs MOD-0030)  (needs EA-BP-003)
                                                    │
                                                    ▼
                                              FU04 Evidence Package
                                              (W-4, needs MOD-0031 + EA-BP-003)
                                                    │
                                                    ▼
                                              FU07 Service extraction (post-R1)

Downstream HARD dependants: MOD-0028, MOD-0313
```

**⚠️ Recorded wave inversion.** Blueprint places MOD-0262 at **W-1** while its own `Dependency Gate` names MOD-0030 (**W-3**) and MOD-0031 (**W-4**). Per CT decision **D-3 the module wave is NOT moved**; instead the FUs are aligned to their dependencies' waves (FU05→W-3, FU04→W-4) and the inversion is recorded as an EA observation rather than silently resolved. FU01 is scoped to require **neither** MOD-0030 nor MOD-0031, which is what makes R1 deliverable.

---

## 8. Ordered delivery sequence

| # | Step | Gate to pass before starting |
|---|---|---|
| 0 | **EA lane (parallel, non-blocking for FU01):** raise EA-BP-001…005; EA-BP-003 is the hard one | — |
| 1 | **MOD-0262-FU01 — Document Binary Store** | DCP-008 `approved` + FU01 pack `ready-for-dev` |
| 2 | FU02 Repository Admin · FU03 Repository Health (parallelisable) | FU01 done |
| 3 | FU05 Physical Disposition Execution | FU01 done **and** MOD-0030 owner exists |
| 4 | FU04 Evidence Package Storage | FU01 done, MOD-0031 available, **EA-BP-003 closed** |
| 5 | FU06 Consumer Cutover (MOD-0162) | **EA-BP-003 closed** (contract must exist before a consumer binds to it) |
| 6 | FU07 Service Extraction to Platform Document Service | FU01–FU05 stable |

No step begins before its predecessor's member pack passes its own `approved` / `ready-for-dev` gate (CAP-001 §7).

---

## 9. Prerequisites

| # | Prerequisite | State |
|---|---|---|
| P1 | DCP-002 identity gate green for MOD-0262 | ✅ exit 0 (2026-09-07) |
| P2 | DCP-002 child preflight for MOD-0262-FU01 | ✅ exit 0 |
| P3 | Registry row for MOD-0262-FU01 | ✅ reserved |
| P4 | MOD-0018 RBAC available for boundary authorisation | ✅ |
| P5 | MOD-0021 audit available | ✅ (~98%) |
| P6 | **`DOC-REPO-BUNDLE` + `DOCS-BASE` defined in the Contract Bundle Dictionary** | ⛔ **EA-BP-003 — OPEN. Blocks FU04 + FU06 only.** |
| P7 | MOD-0030 Records Management owner | ❌ absent — no registry row exists. Blocks FU05. **EA-BP-006 raised** (registry reservation) per OD-6; FU05 waits and MOD-0262 does **not** take temporary ownership of destruction. |
| P8 | MOD-0031 Evidence Linking available | ⚠️ pack exists, `review / planned`. Blocks FU04. |
| P9 | Blueprint `Module Pages` soft-page contradiction resolved | ⚠️ EA-BP-001 open — affects FU02/FU03/FU04 page scope, **not** FU01 |

---

## 10. Architecture decisions

CT-ratified inputs (D-1, D-3, D-4, D-6). These are **data for this pack, not open questions**.

| ID | Decision | Rationale |
|---|---|---|
| **AD-1** | **FU01 is scoped to depend on neither MOD-0030, MOD-0031 nor `DOC-REPO-BUNDLE`.** | D-3. Otherwise a W-1 / R1 / RC=Y module is blocked behind a W-3 module, a W-4 module and an open EA item — with two HARD dependants waiting. |
| **AD-2** | **The seam is preserved, not rewritten.** `IContentStorageGateway` and `ContentRef` (`StorageProvider` + `ObjectKey`) remain the abstraction; FU01 fills in behind it and relocates it. | Six consumer classes and three controllers already call through it; changing the abstraction would break all of them for no gain. |
| **AD-3** | **Contract is designed HTTP-first, but R1 ships physically in-process** inside `Diten.Platform`. Extraction to the standalone `Platform Document Service` is FU07. | D-6. Designing in-process-only would have to be undone; extracting in R1 would put a network boundary under an unproven contract. |
| **AD-4** | **`ContentStoreRequest.Content: byte[]` is retired** in favour of stream/multipart upload returning a `ContentRef` handle; download is a stream (or pre-signed URL). **No binary payload travels in a JSON body.** | D-6. A `byte[]` in a JSON envelope is an unacceptable memory profile at any service boundary, and today's controllers already base64 through a proxy. |
| **AD-5** | **The seam's "caller is responsible for all permission checks" contract is REVOKED.** MOD-0262 authorises every call itself; a remote caller's claim is never trusted. | D-6. The current XML doc delegates authorisation to the caller — valid in-process, a privilege-escalation hole across a boundary. |
| **AD-6** | **Purge must never use `TryDeleteAsync`.** That method is best-effort orphan compensation and returns `false` on failure. Destruction is a separate, audited, irreversible operation that **re-verifies legal hold at execution time**. | Sharing the method would allow a document under legal hold to be silently deleted with a swallowed failure. |
| **AD-7** | **`purge.execute` is a distinct permission key**, never implied by `manage`. | Segregation of duties: destroying records is not an administration action. |
| **AD-8** | **The seam relocation (feature folder → shared contract layer) is inside FU01**, not a separate FU. | FU01 cannot be servicised while its interface lives inside a consumer's feature folder. Splitting it would leave FU01 unable to start. Flagged in §18 as OD-1 in case CT prefers a split. |

---

## 11. Scope

**Delivered across the member FUs:** the binary store and its provider abstraction; boundary authorisation, tenant-isolated object keys and audit; stream-based upload/download; repository administration and health reporting; evidence package assembly; destruction execution against disposition markers; the consumer resolve contract; eventual service extraction.

**Blueprint soft-page coverage:** Document Binary Store → FU02 · Repository Admin → FU02 · Repository Health → FU03 · Evidence Package Storage → FU04. FU01 delivers **no page** (backend/contract only).

---

## 12. Explicit exclusions

- No document metadata, register, lifecycle, approval, or QMS governance behaviour.
- No retention **policy** authoring or legal-hold **decisions**.
- No metadata deletion.
- No external storage provider connectors under this ID.
- No modification of the Blueprint workbook.
- No `EnterpriseStrategyService` change (D-5 is a separate lane).
- No CSV import/export or audit-export rework.
- No permission **seed**, Ocelot route, menu entry, or migration authorised by this pack — those belong to member packs at implementation time.
- No `DOC-REPO-BUNDLE` payload definition (§4a).

---

## 13. Governance drift risks

| Risk | Evidence it is real | Mitigation in this pack |
|---|---|---|
| **Stale identity propagates into downstream plans** | Already happened: the pre-v6.1 external identity reached `SCMM-content-studio-work-plan.md` as "external repo / R2" before any code existed. | Registry now carries the canonical row + a `Retired Reference` row; §1 pins the canonical identity. |
| **A "temporary" second binary store becomes permanent** | `EnterpriseStrategyService/UploadsController` is exactly that, and DCP-003 already had to write "PPM kalıcı binary owner olmaz". | §4 out-of-scope + §17 downstream impacts name every known store. |
| **Contract frozen before the SoT defines it** | `DOC-REPO-BUNDLE` is mandated by Blueprint and absent from its dictionary. | §4a `BLOCKED-on-EA-BP-003`; FU04/FU06 barred from `ready-for-dev`. |
| **Purge quietly reuses best-effort delete** | `TryDeleteAsync` returns `false` on failure by design; the purge path does not exist yet, so the shortcut is available to whoever writes it first. | AD-6 written as a hard rule, carried into FU05's pack. |
| **Authorisation assumption survives the boundary** | The seam's own XML doc instructs callers to authorise. | AD-5 revokes it explicitly; FU01 acceptance criteria test it. |
| **Wave inversion silently "fixed" by moving the module** | Blueprint W-1 vs W-3/W-4 dependencies. | D-3: module wave untouched; FUs aligned; inversion recorded as EA observation (§7). |

---

## 14. Review questions

> ✅ **All seven were answered in the CT review of 2026-09-07.** Questions 1–4 and 6 are settled in §18 (OD-1, OD-2, OD-3, OD-4, OD-A) and §10 (AD-3). Question 5 is EA-owned and tracked as EA-BP-001…006. Question 7 is confirmed: D-5 runs on its own security lane and is not pulled into this pack. Retained below as the record of what was asked.

1. Is `MOD-0262-FU01` genuinely deliverable without MOD-0030, MOD-0031 and `DOC-REPO-BUNDLE` (AD-1)?
2. Is HTTP-first-but-in-process (AD-3) the right R1 trade, or should R1 ship the extracted service?
3. Should the seam relocation be folded into FU01 (AD-8 / OD-1) or split out?
4. Is `platform.document-repository.*` the correct namespace given MOD-0029 already owns `platform.document-management.*`?
5. Does EA accept EA-BP-001…005, and on what timeline for the blocking EA-BP-003?
6. Should FU02 and FU03 be one FU rather than two (OD-2)?
7. Is D-5 (`UploadsController`) being tracked on its own lane, or does it need to be pulled in here?

---

## 15. Gate criteria

**DCP-008 → `approved` / `ready-for-execution` — ✅ CLOSED 2026-09-07 (CT)**
- [x] CT reviews §5 member slicing and §8 sequence. — **reviewed and accepted.**
- [x] §18 open decisions OD-1…OD-4 resolved or explicitly deferred. — **OD-1, OD-3, OD-4, OD-A `RESOLVED`; OD-2, OD-5, OD-6 `DECIDED`. See §18.**
- [x] EA-BP-001…005 raised and acknowledged by EA (**closure not required** for approval — only for FU04/FU06). — **raised in the reconciliation ledger and acknowledged; EA-BP-003 stays open by design and gates FU04/FU06 only.**
- [x] Confirmation that this pack mints no MOD ID beyond the reserved `MOD-0262-FU01`. — **confirmed: `--check-all` exit 0, 0 HARD violations; FU02…FU07 remain unreserved.**

**Any member FU → `ready-for-dev`**
- [ ] Own module pack passes `module-pack-standard.md` (20 sections + frontmatter).
- [ ] DCP-002 preflight exit 0 with `--parent MOD-0262`; registry row reserved.
- [x] DCP-008 is `approved` / `ready-for-execution`. — **met 2026-09-07.**
- [ ] ⛔ **If the FU's scope depends on `DOC-REPO-BUNDLE` (FU04, FU06): EA-BP-003 closed.**

**Member gate status**

| Member | Gate | State |
|---|---|---|
| **MOD-0262-FU01** | all four above | ✅ **`ready-for-dev` — 2026-09-07.** Implementation may start via `@orchestrator` `/add-module`. |
| FU02, FU03 | pack not authored; gated on EA-BP-001 (soft-page contradiction) | R1 **stretch** (OD-4) |
| FU04, FU06 | ⛔ EA-BP-003 open | barred from `ready-for-dev` |
| FU05 | ⛔ MOD-0030 registry identity absent (EA-BP-006) | barred |
| FU07 | deferred by AD-3 | post-R1 |

**Capability → `completed`**
- [ ] All four Blueprint soft pages delivered.
- [ ] Evidence package capability exists and is audited.
- [ ] A disposition marker can be executed end-to-end with hold re-verification.
- [ ] MOD-0028 and MOD-0313 HARD dependencies satisfiable.

---

## 16. Acceptance criteria

- [ ] `MOD-0262` resolves to exactly one canonical registry row plus one `Retired Reference` row; `verify_module_id.py --check-all` exit 0 with 0 HARD violations.
- [ ] No member artifact introduces a `MOD-xxxx` other than `MOD-0262` and its `-FUxx` children.
- [ ] §4a remains explicitly `BLOCKED-on-EA-BP-003` until EA closes it; no member pack defines the bundle payload meanwhile.
- [ ] FU01's pack demonstrates, in its own acceptance criteria, that AD-4 (no `byte[]` payload) and AD-5 (boundary authorisation) are testable.
- [ ] AD-6 (purge never uses `TryDeleteAsync`) is carried verbatim into FU05's pack when authored.
- [ ] The Blueprint workbook is unmodified by this pack (`git diff` contains no `.xlsx`).
- [ ] `git diff --name-only` for this pack's authoring contains only `execution/` paths.

---

## 17. Downstream business-module impacts

| Module / surface | Impact | Timing |
|---|---|---|
| **MOD-0028** (HARD dependant) | Gains a real binary owner; stops relying on a seam inside a sibling feature folder. | FU01 |
| **MOD-0029** | Becomes a pure **consumer**; its three controllers and `DocumentVersioningService` keep calling the same seam (AD-2). Its FU15 disposition boundary statement stays valid and becomes FU05's **input**. | FU01, FU05 |
| **MOD-0313** (HARD dependant) | HCM evidence workspace becomes buildable; `HCM-DOC-EVID-FACADE-BUNDLE` binds to `DOC-REPO-BUNDLE`. | FU04 |
| **MOD-0162 / CRM Knowledge** | The four untyped pointers gain a resolver; the content chain closes end-to-end. | FU06 |
| **MOD-0031** | Evidence packages become linkable objects. | FU04 |
| **MOD-0030** | Gains the physical-destruction counterpart to its metadata destruction. | FU05 |
| **MOD-0117 / PPM** | The "evidence/document **link**, never PPM-local binary" pattern (DCP-003, `portfolio-delivery/domain-config.md`) gets a real target. | FU01 |
| ⚠️ **`EnterpriseStrategyService` Uploads** | Migration candidate — but its measured **authentication gap** (no `UseAuthentication()`, no fallback policy, no `[Authorize]`, route open at the gateway) is a security issue **independent of and more urgent than** this pack. **CT decision D-5, separate lane.** Migration would additionally need checksum backfill and tenant derivation, since `DemandIdeaAttachment` lacks `Checksum`, `TenantId`, `VersionId` and `StorageProvider`. | Out of scope |

---

## 18. Open decisions

**All decisions below were closed by CONTROL TOWER on 2026-09-07.** None remains open against this pack.

| # | Decision | State | Outcome (locked) |
|---|---|---|---|
| **OD-1** | Seam relocation inside FU01 (AD-8) or its own FU? | ✅ **RESOLVED** | **Stays inside FU01** per AD-8. Splitting it would leave FU01 unable to start, since the interface still sits inside a consumer's feature folder. |
| **OD-2** | FU02 + FU03 as two FUs or one? | ✅ **DECIDED** | **Two separate FUs.** Blueprint lists Repository Admin and Repository Health as distinct soft pages; the delivery split follows the SoT. |
| **OD-3** | Permission namespace. | ✅ **RESOLVED — LOCKED** | **Separate namespace `platform.document-repository.*`.** PKS-001 §4 requires exactly one owning module per namespace, so MOD-0029's `platform.document-management.*` is not extended. The MOD-0018 owner confirms key registration at implementation time; the **namespace choice itself is not reopened**. |
| **OD-4** | Does R1 require FU02/FU03? | ✅ **RESOLVED** | **FU01 alone is the `R1 - PPM MVP` commitment.** FU02 and FU03 are **R1 stretch**, gated on EA-BP-001 (the `Module Pages` soft-page contradiction). |
| **OD-5** | EA-BP-003 timeline. | ✅ **DECIDED** | FU04 and FU06 enter R1 **only if EA-BP-003 closes within this wave**; otherwise they move to the next release. **No fixed date is committed from this side.** |
| **OD-6** | Does FU05 wait for MOD-0030, or does MOD-0262 temporarily own destruction? | ✅ **DECIDED** | **FU05 WAITS for MOD-0030's registry identity. MOD-0262 does NOT temporarily own physical destruction.** Rationale: destruction is irreversible and AD-7 keeps `purge.execute` segregated — taking temporary ownership would put an irreversible, SoD-sensitive operation under an unowned identity. A new EA request **EA-BP-006** (MOD-0030 registry row reservation) is raised in the reconciliation ledger. |
| **OD-A** | Target location for the relocated seam. | ✅ **RESOLVED** | **A shared contract location inside `Diten.Platform.Application` — NOT `Diten.Common`.** Cross-service contract extraction is deferred to FU07. The exact namespace/folder name is settled at implementation time against the Golden Reference and the existing MOD-0029 code. |

**Additional CT ruling — maximum object size.** Config-driven, **default greater than 50 MB**. The `EnterpriseStrategy` 50 MB `RequestSizeLimit` was a symptom of buffered uploads; **AD-4's streaming contract removes that memory constraint**. The exact per-environment value is set at implementation time.

**Three judgement calls ratified by CT (2026-09-07):** member identity is the FU child `MOD-0262-FU01`; the seam relocation sits inside FU01 (AD-8); FU01 is backend-only with the four soft pages distributed to FU02/FU03/FU04.

---

## 19. Future follow-ups

- Additional storage providers (S3 / Azure Blob) behind the unchanged seam — the abstraction already permits this; no new FU identity assumed.
- Antivirus scanning and encryption-at-rest policy for stored objects.
- Quota / capacity governance per tenant on repository objects.
- Re-examination of `PlatformAuditController` export once evidence packages exist (an export is produced output today, not a stored object — but that may change).
- Blueprint `Module Pages` soft-page correction (EA-BP-001) feeding FU02/FU03/FU04 page scope.

---

## 20. Audit and reconciliation notes

| Date | Event |
|---|---|
| 2026-06-16 | Blueprint v6.1 reclassified MOD-0262 external → internal. Not propagated to repo records. |
| 2026-09-07 | Impact analysis + PRD measured (`docs/analysis/mod-0262-internal-document-repository-prd-and-impact-analysis.md`): 7 repo drifts, 5 Blueprint-internal contradictions, `EvidencePackage` = 0 occurrences. |
| 2026-09-07 | CT decision **D-1**; registry canonicalized using the `MOD-0169` two-row pattern (canonical row + `Retired Reference`). DCP-002 gate exit 0. |
| 2026-09-07 | Repo drift closed in `execution/`: registry, `master-development-plan.md` (MOD-0262 + neighbouring MOD-0266), `SCMM-content-studio-work-plan.md`, `blueprint-master-plan-reconciliation.md`. `docs/platform/master-plan.md` left untouched (out of `execution/` scope) and reported to CT. |
| 2026-09-07 | EA-BP-001…005 raised in the reconciliation ledger; workbook **not** edited. |
| 2026-09-07 | **DCP-008 authored as `draft`**; `MOD-0262-FU01` reserved (preflight exit 0) and its member pack authored as `draft`. |
| 2026-09-07 | **CT approved DCP-008**; OD-1…OD-6 plus OD-A decisions recorded (§18). `MOD-0262-FU01` promoted to `ready-for-dev`. Implementation authorized for **FU01 only**, via `@orchestrator` `/add-module`. **EA-BP-006** (MOD-0030 registry reservation) raised per OD-6. EA-BP-003 remains open by design and gates FU04/FU06 only. |

**To be filled after implementation phases (CAP-001 §5 `reconciled`):** per-FU delivery evidence, live verification, permission seed records, and a re-run of `/reconcile-records` against this pack's claims.
