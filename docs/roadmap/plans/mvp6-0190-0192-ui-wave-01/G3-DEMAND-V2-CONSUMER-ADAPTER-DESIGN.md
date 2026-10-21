---
module: MOD-0192
work_package: WP-LANE-MAIN-SYNC-05
evidence_level: E4
decision: conditional
commit:
---

# G3 — DEMAND v2 consumer-adapter design (MOD-0190 S&OP + MOD-0192 Capacity)

    authored  : 2026-10-18 by CT (gmg-10), from the consumer-acceptance review
                (docs/records/audits/2026-10/mvp6-demand-v2-consumer-acceptance-01/REPORT.md)
    status    : DESIGN. CT can specify the consumer side now; it **cannot be finalised or built** until the
                MOD-0188 owner freezes demand-v2 and resolves the planId↔revision question. This is the
                consumer half of the cross-team long pole — the design is ready so that when v2 freezes the
                build is mechanical.
    scope     : the consumer adapter only. v2 itself (the producer) is MOD-0188's; XMC-001 — CT does not
                edit the v2 draft.

## The question the consumers ask (unchanged by v2)

"Is the DEMAND reference this plan holds the **exact, current, published** demand for this tenant + legal
entity?" — today answered by the fixture seam:
- S&OP `IDemandFixtureReader.MatchesAsync(SandopScope, string planId, string version, string? checksum, ct) → Task<bool>`
- Capacity `IDemandFixtureReader.IsExact(CapacityScope, CapacityPlan{DemandPlanId, DemandPlanVersion, SourceChecksum}) → bool`
Injected by the Sandop create/snapshot/sign-off handlers and the Capacity repository.

## What v2 provides (measured in the acceptance review)

Revision-centric, not (opaque planId)-centric: `GET /revisions/current?planningPeriodKey={date}` →
`CurrentPublishedEnvelope`; `GET /revisions/{revisionId}/status` → state ∈ {Published, Superseded,
Invalidated} + `integrityState: Verified` + `stateVersion`; `GET /revisions/{revisionId}/manifest` →
`contentVersion` (int) + ordered fields; `IntegrityProof{definitionId: MOD0188-CANONICAL-2/SHA-256,
digest: ^[0-9A-F]{64}$}`. Requires `demand.plans.consume`.

## Design

### 1. Abstraction — one interface, two implementations (fixture now, v2 later)

Introduce a production-neutral reader the handlers depend on, so the fixture-demand interim (G1 choice) and
the real v2 adapter are swappable without touching handlers:

```
public interface IDemandReader {                       // replaces the fixture-only IDemandFixtureReader at the seam
    Task<bool> MatchesAsync(SandopScope scope, DemandRef held, CancellationToken ct);
    Task<bool> IsExactAsync(CapacityScope scope, DemandRef held, CancellationToken ct);
}
public sealed record DemandRef(                        // the re-modelled held identity (see §2)
    Guid RevisionId, DateOnly PlanningPeriodKey, int ContentVersion, string IntegrityDigest, string DefinitionId);
```
- `FixtureDemandReader` — the current `DemandFixtureReader` behaviour, kept for the **interim** (in-memory, test-only).
- `DemandV2HttpReader` — the real adapter below; registered when §22 opens AND v2 is frozen (G4).

### 2. Identity re-model (acceptance condition 1 — the pivotal change)

The consumer entities hold an **opaque** `(DemandPlanId, DemandPlanVersion, SourceChecksum)`; v2 has no
resolve-by-planId. Two ways, and this is a **MOD-0188-owner call** (condition 4):
- **(a) producer adds resolve-by-reference** — v2 accepts the consumer's held identity → least consumer
  change; OR
- **(b) consumers re-bind to v2's identity** — recommended if (a) is declined. At plan-create (bind time),
  resolve `GET /revisions/current?planningPeriodKey` once and **store** `DemandRef{revisionId,
  planningPeriodKey, contentVersion, integrityDigest, definitionId}` on the plan, replacing the three
  opaque fields.

Entity field change under (b):
- `CapacityPlan`: `DemandPlanId`→`DemandRevisionId (Guid)`, `DemandPlanVersion`→`DemandContentVersion (int)`,
  `SourceChecksum`→`DemandIntegrityDigest (string 64-hex)` + `DemandDefinitionId (string)`, add
  `DemandPlanningPeriodKey (DateOnly)`. Equivalent on the Sandop plan aggregate.
- Persistence: a new shape; existing fixture-bound rows are draft/test only and are not migrated.

### 3. Validation flow (how Matches/IsExact is answered against v2)

Given the held `DemandRef` (bound at create time):
1. `GET /revisions/{RevisionId}/status` → require `state == Published` **and** `integrityState == Verified`.
   `Superseded`/`Invalidated`/404 ⇒ **false** (fail-closed; a plan bound to a now-superseded demand is no
   longer exact).
2. `GET /revisions/{RevisionId}/manifest` → require `contentVersion == held.ContentVersion` **and** the
   manifest's scope (tenantId, legalEntityId) == the caller scope **and** `integrity.digest ==
   held.IntegrityDigest` **and** `integrity.definitionId == held.DefinitionId`.
3. **Independently recompute** the MOD0188-CANONICAL-2 digest over the manifest's ordered fields and confirm
   it equals `integrity.digest` (acceptance condition 2 — do not trust the producer's self-reported digest
   blindly; this needs the producer's published canonical spec + test vector, an open item).
All true ⇒ exact/match. This is a **2-call** validation (status + manifest), vs the fixture's one in-memory
check — the handlers already `await`, so the signature goes `Task<bool>` for both (Capacity's `IsExact`
becomes `IsExactAsync`).

### 4. Fail-closed & resilience

Any 401/403/404/422/503, timeout, `Superseded`/`Invalidated`, scope mismatch, version mismatch or digest
mismatch ⇒ **false** and **no business write** (no receipt/outbox/audit) — reuse the existing
`CapacityReadUnavailableException`/`DEPENDENCY_UNAVAILABLE` 503 shape and the fail-closed discipline the
packs already specify (MOD-0192 `:194`). TTL caching is not proof of currency (mirror the DC-03 fencing
principle): re-validate per request, do not cache a `Published` verdict past the request.

### 5. Permission & coupling (feeds G7)

`DemandV2HttpReader` forwards the caller's JWT and requires `demand.plans.consume` — a new cross-domain
grant, the same shape and ceremony as BL-652's `mdm.legal-entities.read` coupling. Add it to the relevant
role template with an expiry/record when G7 runs.

## Blocked until MOD-0188 freezes v2 (cannot finalise/build before these)

1. The exact v2 endpoint shapes + error codes (draft, "not frozen", acceptance review line 66).
2. The **planId↔revision resolution** decision — (a) vs (b) above — MOD-0188 owner.
3. The **canonical checksum spec + independent consumer test vector** for MOD0188-CANONICAL-2 (so step 3.3
   can be implemented and tested) — the draft's own open gap.

## Interim path (if G1 picks composed-but-fixture-demand)

Register `FixtureDemandReader` behind `IDemandReader` now; compose + build the UI against it; swap in
`DemandV2HttpReader` when v2 freezes. The `IDemandReader` abstraction means that swap touches only DI
registration + the entity re-model, not the handlers — so the interim is not throwaway.

## Files

- `G3-DEMAND-V2-CONSUMER-ADAPTER-DESIGN.md` — this. Producer side: MOD-0188's demand-v2 draft (not edited by
  CT). Acceptance basis: `mvp6-demand-v2-consumer-acceptance-01/REPORT.md`.
