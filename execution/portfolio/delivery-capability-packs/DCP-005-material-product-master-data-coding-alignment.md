---
id: DCP-005
slug: material-product-master-data-coding-alignment
name: Material and Product Master Data Coding Alignment
type: Delivery Capability Pack
standard: CAP-001
status: approved
owner_domain: master-data-management
owner: enterprise-architect / product-data-owner / regulatory-owner / quality-owner
branch: none
created: 2026-08-03
canonical_blueprint: "docs/System Capability & Implementation Blueprint - master 8.1.xlsx"
business_policy_input: "GMG-SCM-SOP-0001 v0.3, 2026-07-11"
status_note: "Governance-approved cross-domain scope and ordered delivery plan only. No production code, Module Pack readiness, branch, stage, commit or push is authorized."
---

# DCP-005 — Material and Product Master Data Coding Alignment

> **Artifact type:** Delivery Capability Pack governed by CAP-001. It is not a runtime entity, Module Pack,
> production module, or MOD-0014 runtime Capability Group.
>
> **Authority:** Master 8.1 is the sole Blueprint authority for real module identities and ownership evidence.
> `GMG-SCM-SOP-0001 v0.3` is the current business/policy input by explicit management decision despite its
> `Draft for review` metadata. Master 7 is only a legacy verifier input.
>
> **Premature-coding guard:** This DCP authorizes no runtime work. Execution requires each next member delivery to
> have its own approved/ready-for-dev Module
> Pack or other owner-approved contract. Candidate identities must pass DCP-002 before any Module Pack is authored.

## 1. Identity and status

| Field | Value |
|---|---|
| ID | DCP-005 |
| Name | Material and Product Master Data Coding Alignment |
| Status | `approved` — governance scope and sequencing only; no code-start authority |
| Primary domain | master-data-management |
| Cross-domain owners | Master Data, R&D/Product Data, Regulatory, Quality, Manufacturing |
| Canonical Blueprint | Master 8.1 `Blueprint_Data` |
| Business/policy input | GMG-SCM-SOP-0001 v0.3 |
| Production authority | None |
| Branch | None; branch creation was explicitly prohibited for this authoring task |

**Classification:** A normal Module Pack is insufficient. Material identity, product abbreviation, formulation,
artwork, regulatory registration, market supply, controlled records and downstream manufacturing/quality consumers
have different systems of record and must be delivered in dependency order.

## 2. Business outcome

Create a governed path from stable internal identities to SOP-controlled material, formulation, artwork and
market-product codes without duplicating objects, reusing codes, or overloading MOD-0290 fields. The outcome is:

- one business object identity, with explicit technical UID, internal canonical code, controlled code/revision and
  legacy-alias roles;
- governed ABB, material-class, grade and generic-versus-printed packaging decisions;
- separate formulation (FPF), artwork and registered-presentation (FPP) lifecycles;
- market relationships and regulatory change impact expressed as relations, not accidental code semantics;
- stable MOD-0290 identities consumed by regulatory, manufacturing, quality, batch, stability and dossier modules.

## 3. Problem statement

MOD-0290 currently establishes immutable, system-generated, low-semantic `CanonicalCode` values and a no-reuse
`CodeReservation` ledger for a Product/SKU identity foundation. The SOP introduces meaningful, revision-bearing
codes such as `FPF-...-V1`, `FPP-...-V1` and `LF-...-V1`, plus object-specific ownership and change-cascade rules.
Treating these as the same field would break the existing internal identity model and conflate technical identity,
business identity, revision and regulatory context.

Master 8.1 contains real owners for Product/Item/SKU, Labeling, Controlled Documents, Change Control, Variations,
BOM, Batch, Stability and Dossiers. It has no exact Blueprint module for Formula/Composition, Registered
Presentation/Marketing Authorization, or Market Supply Assignment. No numeric MOD identity may be invented for
those gaps.

## 4. Capability boundary

### In boundary

- SOP-to-ERP identity and coding decisions for ABB, materials, FPF, artwork and FPP;
- ownership and segregation-of-duties boundaries for code allocation and approval;
- relationship between permanent UID, MOD-0290 `CanonicalCode`, future controlled base code, controlled revision
  and `LegacyAlias`;
- generic-versus-printed packaging classification;
- language/market normalization and artwork-to-market reuse;
- registered presentation, market supply and regulatory impact boundaries;
- dependency sequence and code-start/exit gates;
- downstream consumer contracts that must reject legacy aliases as controlled references.

### Outside boundary

- runtime entities, fields, APIs, UI, gateway routes, tests or configuration;
- changing the first MOD-0290 GSKU technical foundation;
- authoring or approving member Module Packs;
- copying the SOP into the repository or loading it into Controlled Documents runtime;
- changing Master 8.1, renaming existing module IDs or reserving a numeric MOD ID;
- implementing formulas, BOMs, MA records, artwork binaries, regulatory assessments or batch/release behavior.

## 5. Member modules and follow-ups

| Identity / owner | Role in this delivery | Classification | Membership decision |
|---|---|---|---|
| MOD-0290 — Product / Item / SKU Master | Product/item/SKU identity SoR; candidate owner for ABB and Material Master because Master 8.1 assigns product identifiers and item master records | Real Master 8.1 module | Member; any ABB/material work requires a separate approved MOD-0290 follow-up scope and must not expand the current slice |
| MOD-0238 — Labeling Lifecycle | Label/artwork lifecycle, change requests, approvals, signatures and published versions | Real Master 8.1 module | Member for artwork/label/leaflet identity and lifecycle contract |
| MOD-0237 — Variations & Renewals | Market regulatory variation lifecycle and decisions | Real Master 8.1 module | Member for affected-market regulatory assessment integration |
| MOD-0209 — Change Control | Regulated change request, approval and impact evidence | Real Master 8.1 module | Member for cross-source change-impact orchestration |
| Formula / Composition owner | FPF formulation identity and lifecycle | Blueprint exact match absent | Candidate/owner uncertainty; DCP-002 CAND-CAP reservation is required before a Module Pack. No candidate number is reserved by this draft |
| Registered Presentation / Marketing Authorization owner | MA and market-specific registered presentation SoR | Blueprint exact match absent | Candidate/owner uncertainty; reconcile against RIM ownership, then pass DCP-002. No candidate number is reserved |
| Market Supply Assignment owner | Explicit Registered Presentation/LSKU-to-Finished-Good market supply relation | Blueprint exact match absent | Candidate/owner uncertainty; pass DCP-002 before Module Pack authoring. No candidate number is reserved |

### Dependencies, not forced members

| Identity | Dependency role | Why not automatically a member |
|---|---|---|
| MOD-0029 — Controlled Documents | Controlled SOP/work-instruction states and records; possible document/version foundation | Generic controlled-document lifecycle does not own regulatory labeling semantics; a later explicit contract decides whether code must change |
| MOD-0048-FU01 — Enterprise Business Reference Data Provider | Potential provider for material classes, languages, markets and controlled dictionaries | SOP does not change provider runtime, Auth S2S or current GSKU catalog delivery; only an approved dependency contract may be consumed |
| MOD-0204 — Stability Studies | Consumes primary-pack configuration and formulation change context | Downstream consumer unless its contract must change |
| MOD-0236 — Dossier/Submissions Management | Consumes controlled identities/revisions and regulatory evidence | Downstream consumer unless its contract must change |
| MOD-0193 / MOD-0195 / MOD-0174 / MOD-0175 | BOM, batch/eBR, lot/batch tracking and quarantine/release consumers | Downstream impacts; not identity/issuance owners in this DCP |

## 6. Ownership map

| Object / contract | Accountable owner | Consumer boundary |
|---|---|---|
| Technical aggregate UUID / permanent internal UID | Each aggregate SoR; MOD-0290 for its aggregates | Immutable reference, not a human controlled code |
| Low-semantic internal `CanonicalCode` and `CodeReservation` | MOD-0290 for its four code-bearing identities | Never assumed to be FPF/FPP/artwork code or revision |
| Product Abbreviation Register (ABB) | Proposed MOD-0290 product-identifier scope; Head of R&D + Master Data business stewardship must approve | SOP requires group-wide three-character uniqueness and no reuse. User decision closes ABB scope as one ERP tenant per GMG group; multiple Legal Entities may exist inside that tenant. Grammar, SoD and runtime contract remain open before issuance. |
| Material Master identities/classes/grade | Proposed MOD-0290 item-master scope; Supply Chain/Master Data accountable | API, HD, EXC, PP, BX, LB, LF, DV classification; artwork boundary delegated to MOD-0238 |
| Formula/Composition / FPF | Unresolved candidate owner; R&D/Product Data accountable | Packaging-independent formulation identity consumed by presentations, BOM, stability and regulatory |
| Artwork/label/leaflet identity and lifecycle | MOD-0238 | Language set and revisions; market list is a relation/attribute, not a code segment |
| Controlled document record/version | MOD-0029 where an explicit binding is approved | Does not replace MOD-0238 labeling semantics or approval lifecycle |
| Registered Presentation / MA / FPP | Unresolved RIM candidate owner | Market-specific registered product identity; mapping to MOD-0290 identities is explicit and not 1:1 by assumption |
| Market Supply Assignment | Unresolved candidate owner | Only valid LSKU/Registered Presentation-to-Finished-Good market supply relationship; no direct LSKU-FG FK |
| Regulated change request | MOD-0209 | Orchestrates impact evidence, not source-object revision issuance |
| Variation/renewal assessment | MOD-0237 | Owns market regulatory decisions and timelines |
| Codeset mechanics | MOD-0048 owner/provider path | Hosts controlled values only; source modules own identity semantics |

## 7. Dependency graph

```text
MOD-0290 internal identity foundation (continues under DCP-004)
  -> ABB governance
  -> Material Master identity/classification
       -> Formula/Composition (FPF) contract [candidate required]
       -> MOD-0238 artwork identity/lifecycle
            -> Registered Presentation/MA [candidate required]
                 -> Market Supply Assignment [candidate required]
                      -> SOP-controlled FPF/FPP/artwork code + revision issuance
                           -> MOD-0209 Change Control + MOD-0237 Variations impact matrix
                                -> BOM / batch / lot / stability / dossier / quality consumers

MOD-0029 Controlled Documents and MOD-0048 reference data are conditional dependencies,
not automatic owners of the business identities above.
```

## 8. Ordered delivery sequence

| Step | Prior decision / record | Owner | Consumes from MOD-0290 | Code-start condition | Exit to next step |
|---|---|---|---|---|---|
| 1. ABB governance | ABB meaning, 3-char grammar, one-tenant-per-GMG-group scope, uniqueness/no-reuse, rebrand and allocator/SoD decision | MOD-0290 proposed; R&D + Master Data approval | Global Product identity and immutable internal UID/code; tenant-scoped CanonicalCode is not ABB namespace proof, but approved GMG tenant topology makes the tenant the ABB group boundary | DCP approved; owner accepted; approved MOD-0290 follow-up pack | ABB register contract, reservation/no-reuse, allocator/SoD and duplicate-resolution ACs approved |
| 2. Material Master and packaging boundary | Material classes; class-specific identity keys; grade rule; supplier-link separation; generic PP vs printed BX/LB/LF; Material SOP-code namespace dependency | MOD-0290 proposed, with MOD-0238 boundary | Stable item/product identities and aliases | Step 1 exit; item-master ownership and applicable namespace scope confirmed; approved pack | Material identity/classification contract, namespace dependency and artwork handoff accepted |
| 3. Formula/Composition (FPF) contract | SoR, active/quantity/UoM model, freeze/revision rules and primary-pack relationship | Candidate-required R&D/Product Data owner | Global Product/Product Definition references only; no Composition FK added to current slice | DCP-002 identity/owner gate + approved Module Pack/domain contract | Packaging-independent FPF identity and revision contract published |
| 4. Artwork/Label/Leaflet identity and lifecycle | Printed-component boundary; language normalization; market relation; new-language identity rule; document binding | MOD-0238; MOD-0029 only if explicit storage/version contract needed | Stable product/ABB references; no CanonicalCode reuse | Step 2 exit; MOD-0238 pack approved; SOP language contradiction resolved | Artwork identity, language-set and lifecycle contract published |
| 5. Registered Presentation / MA / market mapping | RIM SoR, MA lifecycle, market owner, FPP semantics and mapping cardinality | Candidate-required RIM owner; MOD-0237 dependency | Explicit GSKU/LSKU/Finished Good IDs; mapping not inferred | Steps 3-4 exit; candidate identity and pack approved | Registered Presentation/MA contract and market mapping accepted |
| 6. Market Supply Assignment | Relationship SoR, effective dating, legal entity/market semantics, approvals | Candidate-required owner | Explicit LSKU and Finished Good references; no direct FK | Step 5 exit; DCP-002 + approved pack | Assignment lifecycle and non-leaking reference contract accepted |
| 7. SOP-controlled code and revision issuance | UID/base-code/revision separation; ABB dependency; separate Material/FPF/FPP/artwork namespace-scope decisions; no-reuse; allocation SoD; migration/alias policy | Each source owner under one cross-domain issuance contract | Internal UID and tenant-scoped CanonicalCode only as references, never semantic output or namespace substitution | Steps 1-6 contracts stable; every controlled-code namespace scope closed; issuance Module Packs approved | FPF/FPP/artwork code grammar, namespace scope, revision, reservation and audit evidence pass |
| 8. Regulatory change-impact matrix | Version propagation graph and affected-market completeness rule | MOD-0209 + MOD-0237 | Stable source identity references and Market Supply Assignment | Step 7 exit; approved change/variation packs | Every source change resolves affected presentations/markets and blocks incomplete assessment |
| 9. Consumer integrations | Version/reference acceptance and legacy-alias rejection contracts | MOD-0193, MOD-0195, MOD-0174, MOD-0175, MOD-0204, MOD-0236 and quality owners as needed | Stable UID/internal code plus approved controlled code/revision references | Step 8 exit; each changed consumer has approved pack | Contract, idempotency, historical lookup, audit and reconciliation tests pass |

## 9. Prerequisites

- Human review and governance approval of this DCP.
- Master 8.1 remains unchanged and authoritative for real MOD identities.
- Candidate-required owners receive registry and reconciliation-ledger reservations through DCP-002; the attempted
  unregistered candidate path is fail-closed and cannot be used as a Module Pack identity.
- The SOP internal language-code contradiction is resolved by the policy owner.
- MOD-0290 current first-slice boundaries and DCP-004 gates remain intact.
- ABB and every SOP-controlled code family has an owner-approved namespace-scope decision; existing tenant-scoped
  MOD-0290 indexes are not accepted as group-wide uniqueness/no-reuse evidence.
- No member begins without its own approved/ready-for-dev delivery artifact.

## 10. Architecture decisions

1. **Distinct identity layers:** aggregate UUID/permanent internal UID, MOD-0290 `CanonicalCode`, future SOP-controlled
   base code, controlled revision and legacy/commercial alias are distinct concepts. One object may legitimately carry
   these distinct identifiers; duplicate business identity remains prohibited.
2. **No field overloading:** SOP examples containing semantic segments and `Vn` are not written into existing
   `CanonicalCode` or MOD-0290 `RevisionIdentifier`.
3. **Mapping is explicit:** FPF is not automatically Product Definition Revision or GSKU. FPP is not automatically
   LSKU or Finished Good. Cardinality is decided after Composition and Registered Presentation contracts exist.
4. **No direct LSKU-Finished Good relationship:** Market Supply Assignment remains the only future route.
5. **Materials versus artwork:** generic unprinted primary packaging is a shared material; printed BX/LB/LF are
   product-specific artwork components with MOD-0238 lifecycle ownership.
6. **Language and market:** market is not an artwork controlled-code segment; artwork-to-market usage remains a
   relation/attribute. Language remains an unresolved policy choice: either (A) the normalized language set is an
   artwork controlled-code segment or (B) it is only a controlled/register attribute. Neither option is normative or
   implementation-ready before policy-owner approval. Alphabetical multilingual ordering becomes a code-generation
   rule only under option A, and adding a language creates a new artwork identity only if the approved identity/revision
   model says so. FPP market/language semantics remain separately owner-confirmed.
7. **Supplier independence:** adding an approved supplier does not create a new material identity; supplier
   qualification is a separate link, even where regulatory variation is still required.
8. **Herbal distillate reuse:** when the same herbal distillate is used by another product or formulation, its original
   material identity and controlled HD code remain unchanged. A second product ABB does not create another HD identity,
   HD code, supplier record or specification. Additional use is recorded by a future material-to-formulation/product
   usage relationship; reuse alone is not a new material revision or controlled-code reason. A new HD identity/revision
   is considered only for a genuine material-identity change under the future Material Master policy. This decision
   creates no runtime relation, entity, field or code.
9. **Change propagation:** material, formula, primary-pack and artwork changes generate explicit impact edges; source
   owners issue revisions while MOD-0209/MOD-0237 govern assessment and decision evidence.
10. **Provider stability:** MOD-0048-FU01, AuthService S2S and current GSKU reference catalog work are not changed by
   this SOP alignment. Any dependency is recorded and consumed through a separately approved contract.
11. **First GSKU continuation:** the DCP-004 internal foundation may continue. It stops only if a delivery attempts
     SOP issuance, semantic FPF/FPP/artwork output, Composition/MA/artwork scope, or an unapproved 1:1 mapping.

### Namespace Scope

- MOD-0290 `CanonicalCode` remains tenant-scoped. Its current persistence evidence is a per-tenant counter and unique
  indexes keyed by `TenantId + ReservedCode` or `TenantId + CanonicalCode`. These indexes prove tenant-local
  uniqueness only; they do not prove the SOP's group-wide ABB uniqueness/no-reuse requirement.
- ABB namespace scope is resolved by explicit user policy/enterprise decision: one GMG group is one ERP tenant, and
  multiple Legal Entities may exist within that tenant. ABB uniqueness/no-reuse is therefore tenant-wide within the
  GMG group boundary. Current tenant-scoped persistence did not establish this operating assumption by itself.
- Material SOP codes and FPF/FPP/artwork controlled-code scopes remain open governance decisions. Each family must
  explicitly select and justify group-wide, tenant-wide or another owner-approved scope; no scope is inferred from
  MOD-0290 `CanonicalCode` or from the resolved ABB scope.
- If the approved decision requires a group-wide registry distinct from MOD-0290, it needs a separate accountable
  owner, a DCP-002-compliant identity and its own approved/ready-for-dev Module Pack before code-start.
- ABB scope affects every controlled grammar that embeds ABB, including HD, FPF, FPP and BX/LB/LF artwork codes.
  Those controlled-code namespaces must still close separately; they are not automatically the ABB namespace.
- Tenant-scoped `CanonicalCode` and any group-wide regulated code must never be treated as the same namespace merely
  because they refer to the same business object.
- This decision record designs no runtime field, `GroupId`, central registry, cross-tenant query or persistence
  change. Runtime topology is explicitly deferred until the namespace owner and scope gates close.

## 11. Scope

- TO-BE/AS-IS impact analysis and ownership mapping.
- Delivery sequencing and cross-domain gates.
- Targeted governance clarification in DCP-004, the MOD-0290 Domain Contract and Module Pack.
- Additive backlog visibility for SOP-driven deferred work.
- Candidate-owner gaps without invented MOD identities.

## 12. Explicit exclusions

- Runtime implementation and data migration.
- New fields, collections, placeholder FKs or Composition references in MOD-0290.
- Composition, artwork, MA, Market Supply, BOM, batch, stability, dossier or quality behavior in the first slice.
- SOP document import/copy, Master 8.1 edits, ID renames and provider refactors.
- Branch, stage, commit, push, reset, stash, restore, deletion or broad formatting.

## 13. Governance drift risks

- The registry describes MOD-0290 as planned/draft while its Module Pack is `in-progress`; this DCP records but does
  not reconcile that unrelated status drift.
- MOD-0048-FU01 pack/registry status wording is inconsistent; the SOP does not authorize changing it.
- MDM domain-config contains stale service-scaffold language already tracked by BL-028.
- Calling every related module a member would blur ownership; consumers stay downstream until their contract changes.
- Treating MOD-0029 as the labeling owner would erase MOD-0238 regulatory semantics.
- Reserving an unregistered CAND-CAP or inventing a numeric MOD would violate DCP-002.
- `one object, one code` may be misread as one string only; the decision is one business identity and no duplicate
  allocation, while technical UID, internal code, controlled revision and aliases have different roles.
- SOP §3.1 requires language in artwork codes, while §6 says languages and markets are attributes never in the code.
  Revision History supports the §3.1 intent, but only the policy owner can resolve the contradiction.

## 14. Review questions

1. Does the policy owner select language option A (normalized language set is an artwork controlled-code segment) or
   option B (normalized language set is only a controlled/register attribute), while market remains only a relation?
2. Does MOD-0290 own the Product Abbreviation Register and raw Material Master as product-identifier/item-master
   follow-ups, or is an EA-approved separate capability required?
3. What exact Blueprint/candidate owner receives Formula/Composition?
4. What exact RIM/candidate owner receives Registered Presentation and Marketing Authorization?
5. Who owns Market Supply Assignment, and what are its cardinality and effective-dating rules?
6. Are FPF/FPP controlled codes one base identity plus revision records, or is each revision itself a controlled code?
7. Which roles allocate materials, FPF/FPP and artwork, and which roles approve, assess or release them?
8. What event proves an affected market has completed regulatory assessment before implementation?
9. **Resolved 2026-08-03:** ABB scope is one ERP tenant per GMG group; multiple Legal Entities may exist within that
   tenant. What allocator/SoD and runtime contract implement this scope without treating `CanonicalCode` as ABB?
10. What are the separately approved namespace scopes for Material, FPF, FPP and artwork controlled codes, and how
    does ABB scope constrain their grammars without merging them with tenant-scoped `CanonicalCode`?

## 15. Gate criteria

| Gate | Closure evidence | Blocking effect |
|---|---|---|
| D5-G1 Identity semantics and controlled-code scope | Approved UID/internal-code/controlled-code/revision/alias decision plus separate namespace-scope decisions for Material, FPF, FPP and artwork codes; tenant-scoped CanonicalCode is not group-wide regulated-code proof | Blocks all controlled-code issuance |
| D5-G2 ABB owner and namespace | **Scope resolved 2026-08-03:** one ERP tenant per GMG group; multiple Legal Entities may exist within that tenant. Remaining closure requires owner acceptance, grammar, uniqueness/no-reuse, allocator/SoD and runtime contract. A distinct group-wide registry is not selected for ABB. | Blocks ABB issuance code-start and every downstream grammar that embeds ABB until remaining closure evidence exists |
| D5-G3 Material boundary | Approved classes, grade, supplier link, generic/printed rules, Material SOP-code namespace dependency and a testable HD cross-product reuse rule: same HD + second product means no new ABB-derived HD identity/code and usage is relationship-based | Blocks Material issuance and FPF/artwork source identity code-start |
| D5-G4 Candidate identity | DCP-002 registry + reconciliation-ledger + verifier PASS | Blocks Formula/Composition, Registered Presentation/MA and Market Supply Module Packs |
| D5-G5 Artwork policy | MOD-0238 ownership; market-as-relationship rule; policy-owner selection between language option A (controlled-code segment) and option B (controlled/register attribute); conditional alphabetical ordering and new-language identity/revision behavior resolved | Blocks artwork issuance and Registered Presentation linkage |
| D5-G6 Regulatory mapping | MA/Registered Presentation and Market Supply contracts approved | Blocks FPP issuance and impact matrix |
| D5-G7 Issuance and SoD | Reservation/no-reuse, actor trust, maker-checker and audit evidence | Blocks controlled-code production use |
| D5-G8 Consumer readiness | Historical lookup, alias rejection, impact completeness and reconciliation tests | Blocks downstream rollout |

## 16. Acceptance criteria

- [x] This pack contains all 20 CAP-001 sections and is governance-approved following human review; this status grants no code-start authority.
- [ ] Every real MOD ID/name used here matches Master 8.1.
- [ ] No numeric MOD or unregistered CAND-CAP identity is invented.
- [ ] CanonicalCode, permanent internal UID, controlled base code, controlled revision and legacy alias are distinct.
- [ ] MOD-0290 CanonicalCode remains tenant-scoped, and its `TenantId` unique indexes are not represented as
      group-wide ABB or regulated-code uniqueness proof.
- [x] ABB namespace scope is explicitly approved: one ERP tenant per GMG group, with multiple Legal Entities permitted
      within that tenant. ABB remains separate from `CanonicalCode`.
- [ ] Material, FPF, FPP and artwork controlled-code namespace scopes are explicit owner-approved decisions; no family
      is automatically merged with another, with resolved ABB scope or with tenant-scoped CanonicalCode.
- [ ] FPF/FPP/artwork mappings are explicit decisions and not inferred from GSKU/LSKU/Finished Good.
- [ ] Direct LSKU-Finished Good FK remains prohibited; Market Supply Assignment is the only future route.
- [ ] Material classes, grade identity and generic-versus-printed packaging rules have testable owner contracts.
- [ ] Herbal distillate cross-product reuse is testable: the same HD used by a second product/formulation retains its
      original material identity and controlled HD code, creates no second-ABB-derived duplicate, and records added use
      through a relationship; reuse alone is not a new revision/code reason.
- [ ] Artwork market usage is relationship/attribute-based, and the policy owner has selected language option A
      (controlled-code segment) or B (controlled/register attribute). Alphabetical multilingual code ordering applies
      only under A; new-language identity/revision behavior is separately resolved and testable.
- [ ] Code allocation roles and SoD are explicit for materials, FPF/FPP and artwork.
- [ ] Affected-market regulatory assessment is complete before implementation of an impacted change.
- [ ] MOD-0048-FU01, Auth S2S and current GSKU catalog delivery remain unchanged.
- [ ] The first GSKU foundation continues only within DCP-004 and MOD-0290 authorized boundaries.
- [ ] Deferred work is visible in additive backlog entries; BL-015 through BL-027 remain intact.

## 17. Downstream business-module impacts

### SOP rule to ERP capability impact matrix

| # | SOP rule | ERP entity/capability | AS-IS | Gap / correct owner | Proposed wave |
|---:|---|---|---|---|---|
| 1 | One object, one code; no duplicate identity | Identity + reservation + duplicate correction | MOD-0290 supports four code-bearing identities | Cross-domain equivalence and change-controlled duplicate correction; each SoR | 1-8 |
| 2 | Permanent UID; no reuse | UUID/UID + CodeReservation/tombstone | MOD-0290 UID/code no-reuse foundation exists | Explicit separation from controlled revisions | 1, 7 |
| 3 | Legacy/commercial aliases are not controlled references | LegacyAlias + consumer validation | MOD-0290 LegacyAlias exists | Expand owner/consumer contracts; batch/stability/spec/dossier must reject aliases | 7, 9 |
| 4 | ABB is 3-char, group-wide unique, never reused | Product Abbreviation Register | Absent; current MOD-0290 uniqueness is tenant-scoped only | Proposed MOD-0290 product-identifier scope; approved ABB group boundary is one ERP tenant per GMG group with multiple Legal Entities permitted; R&D/Master Data stewardship and runtime contract remain required before issuance | 1 |
| 5 | API/HD/EXC/PP/BX/LB/LF/DV classes | MaterialClass + class policies | Absent | Proposed MOD-0290 item-master scope; MOD-0238 owns artwork lifecycle | 2 |
| 6 | Grade change creates another material identity | Material identity key | Absent | Material Master invariant | 2 |
| 7 | Generic unprinted primary pack is shared | Packaging Material + usage relation | Absent | Material Master; no ABB/product-child identity | 2 |
| 8 | Printed BX/LB/LF are product-specific artwork | Artwork Component | BL-020 only | MOD-0238, with explicit Material Master handoff | 4 |
| 8a | Same HD reused by another product keeps its original identity/code | Material-to-formulation/product usage relation | Absent | Material Master future contract; no second-ABB-derived HD identity/code, duplicate supplier/specification or reuse-only revision | 2 |
| 9 | Market is not an artwork code segment | Artwork-to-market/presentation relation | Absent | MOD-0238 + Registered Presentation/Market Supply contracts; market remains usage relation/attribute | 4-6 |
| 10 | Language representation has two policy options: controlled-code segment or controlled/register attribute | Artwork identity + LanguageSet | Absent | Policy owner + MOD-0238; neither option is implementation-ready before approval and neither reuses CanonicalCode | 4, 7 |
| 11 | One artwork may serve many markets | Artwork-to-market/presentation relation | Absent | MOD-0238 + Registered Presentation/Market Supply contracts; no market code segment | 4-6 |
| 12 | Alphabetical multilingual code ordering applies only under language option A; adding language follows the approved identity/revision model | LanguageSet normalization + artwork identity/revision policy | Absent | Policy owner + MOD-0238 issuer validation after option and model approval | 4, 7 |
| 13 | FPF is formulation; FPP is market registered product | Formula/Composition + Registered Presentation | Both excluded from MOD-0290 first phase | Two separate owners/candidate gates | 3, 5 |
| 14 | Primary-pack configuration affects stability | PrimaryPackagingConfiguration relation | Absent | Formula/Presentation owners; MOD-0204 consumer | 3, 5, 9 |
| 15 | Source change requires every affected market assessment | ChangeImpactCase + market assessment matrix | Absent | MOD-0209 + MOD-0237 | 8 |
| 16 | Allocation owner and SoD | Issuance RBAC/workflow/audit | Generic MOD-0290 steward/approver split exists | Object-class allocation matrix across Master Data, R&D, Regulatory and QA | 1-8 |

### Consumer effects

- MOD-0193 BOM & Routings references approved material, FPF and presentation identities; it does not own them.
- MOD-0195 Batch Execution/eBR and MOD-0174 Lot/Batch/Serial must reference permanent UID plus approved version and
  reject legacy/commercial aliases as controlled references.
- MOD-0175 Quarantine/Blocked Stock consumes quality/release decisions; it does not issue product codes.
- MOD-0204 Stability consumes formulation and primary-pack configuration identities.
- MOD-0236 Dossier/Submissions consumes controlled identities/revisions and regulatory decision evidence.
- Quality specifications and release ownership require an exact Master 8.1 owner decision before code-start; no
  placeholder module identity is created here.

## 18. Open decisions

- SOP language contradiction: §3.1 versus §6.
- Meaning of the `HD-TRT-01` legacy-alias example versus the base of controlled `HD-TRT-01-V1`.
- Base controlled identity versus revision-instance model for FPF/FPP/artwork.
- Material, FPF, FPP and artwork controlled-code namespace scopes; they remain open and must not be inferred from
  the resolved ABB scope or from MOD-0290's tenant-scoped CanonicalCode.
- ABB and Material Master ownership within MOD-0290 versus a future EA candidate.
- Exact candidate identities/owners for Formula/Composition, Registered Presentation/MA and Market Supply Assignment.
- FPF-to-Product Definition/GSKU and FPP-to-LSKU/Finished Good cardinalities.
- Quality specification/release owner and the event contract for all-market assessment completeness.
- Whether MOD-0029 needs a later binding change or is an unchanged document/version dependency.

## 19. Future follow-ups

- DCP-002 candidate reservations after EA/owner decisions.
- Approved Module Packs or domain contracts for each ordered step.
- Legacy-code profiling and migration under BL-023, never silent promotion into canonical output.
- Reference-data set proposals only after source owners define semantics; provider runtime remains unchanged here.
- Controlled-document bindings, retention/evidence, training and e-signature only through their real owners.
- External ERP/PLM distribution after DCP-004 G7/BL-025 re-entry conditions close.

## 20. Audit and reconciliation notes

### 2026-08-03 authoring audit

- DCP collision check found DCP-001 through DCP-004; DCP-005 is the next available canonical DCP identity.
- SOP v0.3 was structurally extracted: 37 paragraphs, 8 tables, no comments or tracked changes. LibreOffice was not
  available, so no verified page-number citations were produced; evidence uses exact SOP sections/tables.
- Master 8.1 `Blueprint_Data` evidence was inspected read-only. Key ranges:
  `A29:V30`, `A174:V176`, `A193:V205`, `A206:V210`, `A232:V245`, `A285:AG295`.
- Confirmed real modules: MOD-0029, MOD-0174, MOD-0175, MOD-0193, MOD-0195, MOD-0204, MOD-0209, MOD-0236,
  MOD-0237, MOD-0238 and MOD-0290.
- Exact Blueprint matches were not found for Formula/Composition, Registered Presentation/Marketing Authorization or
  Market Supply Assignment. The unregistered CAND-CAP path fails closed; this DCP therefore records candidate/owner
  uncertainty without inventing an ID.
- DCP-004, the MOD-0290 Domain Contract and Module Pack were reconciled only with boundary notes. No runtime scope or
  readiness status was changed.
- Namespace correction evidence: current CodeReservation allocation and uniqueness use `TenantId`-scoped counters,
  filters and `TenantId + ReservedCode` indexes; Global Product uses `TenantId + CanonicalCode`. This proves only
  tenant-local CanonicalCode uniqueness. DCP-005 therefore leaves ABB and Material/FPF/FPP/artwork controlled-code
  scopes open and blocks issuance until owner-approved scope gates close; no runtime topology was designed.
- Existing dirty-worktree changes belong to the user and were preserved. This authoring created no branch and did no
  stage, commit, push, reset, stash, restore or deletion.
