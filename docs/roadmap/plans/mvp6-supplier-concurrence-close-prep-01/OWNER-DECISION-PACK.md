# MVP6 Supplier concurrence — bounded owner decision pack

Status: **DECISION-READY / UNAPPROVED**. The SS-01…SS-09 A policy selection and AC-01…AC-27 targets are already bound by [DECISION-BINDING](../mvp6-supplier-spec-split-01/DECISION-BINDING.md) SHA-256 `1cda9f581959b83c2c83f8cff9eb8bc5b2a029c7127ddbd40c424f1a2867646b`. This pack does not reopen that selection.

The five items below are the remaining substantive owner concurrences. Each owner may approve its own item independently. Silence, another owner's approval, a draft pack, a fixture result or an agent statement is not concurrence.

## DC-01 — durable domain/service ownership

**Decision owner:** Enterprise/domain owner, with CT and Supply Chain Execution concurrence.

**Artifact binding:** [common SG-01](../mvp6-supplier-spec-split-01/COMMON-SEAM-GAPS.md), [MOD-0147 placement gate](../mvp6-mod0147-spec-split-01/CONCURRENCE-OPEN.md), [MOD-0148 placement gate](../mvp6-mod0148-spec-split-01/CONCURRENCE-OPEN.md). The bound split package seal is `26ffe843110b2c9596908c8b7a1599fa5f20e61c608c4bf0472c07f815979c0b`.

**Exact proposed concurrence:** MOD-0147 Supplier Performance & Risk and MOD-0148 Supplier Portal are durably placed in `supply-chain-execution` and implemented, when separately authorized, by `Diten.SupplyChainService`. MOD-0140 remains the Supplier master owner. This concurrence assigns no runtime, contract, gateway or permission ownership to the two modules.

**SUPPLIER v1 coverage:** none; contract metadata cannot decide organizational placement.

**New producer seam required:** none. A later, separately authorized governance writer must align domain-config, DCP-009 and the registry without changing module identity.

**Acceptance binding:** AC-01.

**Before this decision:** module spec review and the existing static hash checks may continue. Governance amendment, pack promotion and DEV cannot start.

**After this decision:** one governance-diff preparation lane may create an exact proposed domain/DCP/registry alignment. Applying it, changing pack status and runtime remain separate authorities.

**Owner response:** `APPROVE DC-01 as written` or `REJECT DC-01; exact alternative domain/service owner: …`.

## DC-02 — MOD-0140 Supplier consumption and LegalEntity eligibility ownership

**Decision owner:** MOD-0140 Supplier owner and its eligibility owner; MOD-0147/0148 remain design consumers.

**Artifact binding:** FROZEN SUPPLIER v1 SHA-256 `87a297edfb8eabf9ecc8beff7870955f46b38413a1490a05a36e3f255d77bb00`; [SG-02…SG-04](../mvp6-supplier-spec-split-01/COMMON-SEAM-GAPS.md); [MOD-0147 base boundary](../mvp6-mod0147-spec-split-01/SPEC-DELTA.md); [MOD-0148 base boundary](../mvp6-mod0148-spec-split-01/SPEC-DELTA.md).

**Exact proposed concurrence:**

1. MOD-0147 and MOD-0148 may consume existing `getSupplier` for singleton opaque identity and the exact `Active`, `OnHold`, `Blocked`, `Inactive` status vocabulary.
2. MOD-0140 remains responsible for authoritative Supplier identity/status behavior and for an affirmative Tenant+LegalEntity eligibility result. Tenant-scoped identity/status alone is not affirmative LE eligibility.
3. Exact input identity must equal the response identity; missing, malformed, mismatched, incomplete or unavailable authority fails closed. No consumer may infer eligibility from name, contact, tax/profile data or `Active` alone.
4. This concurrence approves the behavior boundary only. It does not name or approve a new endpoint, claim, transport, response schema, contract version or rollout.

**SUPPLIER v1 coverage:** current lookup operations, opaque ID and four status values. Existing v1 does not cover affirmative LE eligibility, complete strict result declarations, all dependency errors or a live producer proof.

**New producer seam required:** affirmative Tenant+LE eligibility, exact failure/correlation semantics and live-producer evidence. The carrier remains unresolved and must be designed once by the CT Supplier Seam Owner with security concurrence.

**Acceptance binding:** AC-02…AC-06; MOD-0147 AC-03/04 and MOD-0148 AC-03…05 are the controlling consumer examples. AC-06 remains future shared-fixture coverage because neither bounded module surface has a bulk operation.

**Before this decision:** frozen-v1 static contract checks and module-only domain test design may continue. A real Supplier adapter, LE-dependent mutation, successor contract or producer acceptance cannot start.

**After this decision:** CT may combine this behavior boundary with DC-03 to prepare one exact seam/carrier and compatibility proposal. Runtime uptake still waits for a published artifact, consumer consent, pack promotion and runtime authority.

**Owner response:** `APPROVE DC-02 as written` or `REJECT DC-02; identify the exact disputed clause and owner-supported replacement`.

## DC-03 — auth/security binding, current authorization and revocation fencing

**Decision owner:** Platform auth/security identity owner, with MOD-0148 and MOD-0140 concurrence at their boundary.

**Artifact binding:** [MOD-0148 actor-binding and replay delta](../mvp6-mod0148-spec-split-01/SPEC-DELTA.md) SHA-256 `6dce93fde8c4069a2524193f6a593ee5b64300ba000f199af77655fd66cb0076`; [MOD-0148 acceptance map](../mvp6-mod0148-spec-split-01/ACCEPTANCE-TEST-MAP.tsv) SHA-256 `883f31f19ee7feff1cc88d5333934fb9439f9f3579906fc865cccf9c0423816e`; SG-05, SG-06 and SG-10.

**Exact proposed concurrence:**

1. The authoritative binding key is `(validated issuer, stable subject, trusted TenantId, trusted LegalEntityId)` and resolves to one opaque SupplierId plus binding identity/revision and active/revoked state.
2. One actor has at most one active Supplier per Tenant+LE; a Supplier may have multiple actors; an actor may have separate bindings in separately authorized LEs. There is no implicit LE selection.
3. Email, contact, display name, generic user ID or `userId == supplierId` never establishes Supplier identity.
4. Authentication, current membership/permission and current binding are checked before every request and receipt replay.
5. Revocation committed before mutation commit wins through binding-revision fencing and produces no business write, receipt or event. A committed mutation remains historical, while later access is denied. TTL-only caching is not proof of this guarantee.
6. The receipt namespace remains actor-, LE-, Supplier-, operation- and target-isolated as specified in the MOD-0148 delta.
7. This concurrence chooses no provider endpoint, JWT claim, mapping-store location, cache implementation, permission seed or deployment mechanism.

**SUPPLIER v1 coverage:** tenant-scoped Supplier lookup after a SupplierId is known. It provides no actor mapping, trusted LE, current grant, binding revision or revocation protocol.

**New producer seam required:** binding resolution/current-revision authority, trusted LE issuance, membership/permission currency, outage behavior and the mutation/revocation atomic fence.

**Acceptance binding:** AC-08…AC-17, especially AC-08 cardinality, AC-09 precedence, AC-10 own-record isolation and AC-11 revocation/outage.

**Before this decision:** MOD-0148 request/replay test design and static schema review may continue. Binding adapter, identity carrier, revocation implementation, permission registration and portal runtime cannot start.

**After this decision:** DC-02 and DC-03 jointly permit a single CT seam/carrier proposal and a security-owned producer design artifact. They do not authorize implementation or publication.

**Owner response:** `APPROVE DC-03 as written` or `REJECT DC-03; identify the exact disputed clause and owner-supported replacement`.

## DC-04 — Metric Registry policy and immutable revision authority

**Decision owner:** Metric Registry owner; MOD-0147 is the design consumer. Risk owner concurs only on score bands under DC-05.

**Artifact binding:** [MOD-0147 scoring delta](../mvp6-mod0147-spec-split-01/SPEC-DELTA.md) SHA-256 `68732d5ae31740e0954b712229ccf17b2fbccb1bba31b4e95c84e699d5dd4ced`; [MOD-0147 acceptance map](../mvp6-mod0147-spec-split-01/ACCEPTANCE-TEST-MAP.tsv) SHA-256 `4b6a967a1c2cbec0499068a3fbf8249bf32073cdb59977acb563c0d8ad5becf0`; SG-07.

**Exact proposed concurrence:** Metric Registry owns effective immutable metric revisions for `supplier-score-policy/1`, including Tenant+LE scope, unique metric code, `PERCENT` UoM, `HIGHER_IS_BETTER` direction, exact approved weight, validity interval and withdrawal/authenticity state. Values and weights follow the already selected four-fractional-digit and exact-total-100 rules; scoring is exact decimal `sum(value × weight)/100`, with one two-decimal round-half-even output. Create stores the resolved revision/policy/inputs/weights/scope/source-hash snapshot; later registry revisions do not rewrite it. Known unsupported values fail 422; unavailable, incomplete or unverifiable authority fails closed under the future agreed wire disposition.

**SUPPLIER v1 coverage:** none beyond identifying the Supplier being evaluated. It has no metric policy or revision authority.

**New producer seam required:** live Metric Registry pointer/carrier, immutable revision representation, validity and withdrawal/authenticity query. No endpoint or transport is approved here.

**Acceptance binding:** AC-18, AC-20…AC-22. AC-19 score-band ownership also requires DC-05.

**Before this decision:** pure arithmetic/rounding test vectors may be reviewed as spec examples. A registry adapter, authoritative evaluation creation/submit or producer acceptance cannot start.

**After this decision:** Metric owner may produce one exact registry artifact/pointer proposal; CT may assess whether shared wire amendment is required. MOD-0147 runtime still waits for all relevant gates.

**Owner response:** `APPROVE DC-04 as written` or `REJECT DC-04; identify the exact disputed clause and owner-supported replacement`.

## DC-05 — Risk taxonomy, score bands and source-resolution boundary

**Decision owner:** Risk Register owner with MOD-0147 lifecycle concurrence; MOD-0148 concurs on portal-source visibility; Metric owner concurs on the numeric band inputs.

**Artifact binding:** [MOD-0147 risk delta](../mvp6-mod0147-spec-split-01/SPEC-DELTA.md), its exact hashes under DC-04, SG-08 and SG-09, and the MOD-0148 AC-25 source contribution.

**Exact proposed concurrence:**

1. Risk Register is taxonomy/reference authority only. It owns immutable taxonomy revisions for the six existing categories and four existing levels. MOD-0147 owns SupplierRisk instances, versions, lifecycle, audit and Pending outbox.
2. Score bands use the unrounded aggregate: `[90,100] LOW`, `[75,90) MEDIUM`, `[50,75) HIGH`, `[0,50) CRITICAL`; display rounding never changes the band and never auto-creates or mutates SupplierRisk.
3. SupplierRisk lifecycle is `OPEN→ACKNOWLEDGED→MITIGATED→CLOSED`; self, skip and reopen transitions are invalid. Existing remediation uses its stored taxonomy snapshot and is not blocked by a later registry outage.
4. `SCORECARD` sources must be same-scope published scorecards. `PORTAL_SUBMISSION` sources must be same Tenant+LE+Supplier and `SUBMITTED` or a later declared review state. `DRAFT`, foreign scope, `MANUAL` and `EXTERNAL_SIGNAL` are rejected in the bounded slice without existence disclosure.
5. This concurrence names no Risk endpoint, transport, taxonomy carrier or new Evidence API.

**SUPPLIER v1 coverage:** opaque Supplier identity/status only. It has no risk taxonomy, score band, lifecycle or source-resolution authority.

**New producer seam required:** immutable taxonomy revision and validity source, plus a scoped portal-source resolver shared with MOD-0148. The exact carrier and failure wire remain CT seam work.

**Acceptance binding:** AC-18/19 for bands and AC-23…AC-26 for lifecycle, sources and taxonomy outage.

**Before this decision:** local lifecycle and band examples remain spec-only. Risk registration against live taxonomy and PORTAL_SUBMISSION source acceptance cannot start.

**After this decision:** Risk owner and the two modules may provide exact producer/source artifacts to the single CT seam owner. No runtime or publication follows automatically.

**Owner response:** `APPROVE DC-05 as written` or `REJECT DC-05; identify the exact disputed clause and owner-supported replacement`.

## Explicitly deferred after these five decisions

Even unanimous concurrence does not decide the exact endpoint/claim/transport, shared schema, contract version, external strict-consumer consent, publication, permission seed, Program.cs composition, pack promotion or runtime rollout. Those are artifact-bound later gates in `DEPENDENCY-SEQUENCE.md`.

The test-only `supplier-exact-policy-fixture/1` is not an alternative to any owner decision or producer artifact. It may only generate evidence labeled `SIMULATED`.
