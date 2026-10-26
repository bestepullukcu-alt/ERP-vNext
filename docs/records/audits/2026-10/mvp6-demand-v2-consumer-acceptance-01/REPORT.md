---
module: MOD-0190
work_package: WP-LANE-MAIN-SYNC-05
evidence_level: E4
decision: conditional
commit:
---

# DEMAND v2 draft — MVP-6 consumer-acceptance review (MOD-0190 S&OP + MOD-0192 Capacity)

    reviewed  : 2026-10-18 · author: CT (gmg-10) · consumers: MOD-0190, MOD-0192
    producer  : MOD-0188 owner · draft: services/Diten.PlanningService/contracts/drafts/demand-v2.openapi.yaml
                (v2.0.0-draft.1, DRAFT), answering REQUEST-DEMAND-V2.md (Q442)
    verdict   : **ACCEPT WITH CONDITIONS.** The draft satisfies the consumers' immutable-provenance need and
                is in fact stronger than asked — but it is NOT the "one additive resolve-by-reference
                operation" the request framed; it is a richer revision model that requires the two consumers
                to re-model their binding identity. The draft's own note (line 66) asks for exactly this
                consumer verification; this is it.

## What the consumers need (measured, from their code)

- **S&OP** `IDemandFixtureReader.MatchesAsync(SandopScope scope, string planId, string version, string? checksum, ct) → bool`
- **Capacity** `IDemandFixtureReader.IsExact(CapacityScope scope, CapacityPlan plan) → bool`, where the plan
  carries `DemandPlanId` (string), `DemandPlanVersion` (string), `SourceChecksum` (string, e.g. `sha256:ee56d4f9a3c8`).

Both answer one question: *is this exact plan reference, at this version and checksum, the current published
plan for this tenant + legal entity?*

## What the draft provides, mapped

| consumer field | v2 draft equivalent | fit |
|---|---|---|
| scope (tenant + LE) | JWT + `SelectedLegalEntityHint`; every op scope-bound | **✓** |
| "is published / current" | `state` ∈ {Published, Superseded, Invalidated} + `integrityState: Verified` (`/revisions/{id}/status`, `/revisions/current`) | **✓ stronger** — distinguishes superseded from unknown (the request's binary 200/404 could not) |
| `version` (string) | `contentVersion` (int, "included in checksum") + monotonic `stateVersion` | **✓ with a type change** |
| `checksum` (`sha256:…`, short) | `IntegrityProof {definitionId: MOD0188-CANONICAL-2/SHA-256, digest: ^[0-9A-F]{64}$}` | **✓ but incompatible format** — 64-hex uppercase + definitionId vs the consumer's short opaque string |
| `planId` / `DemandPlanId` (opaque string) | `revisionId` (uuid) + `planningCycleId` (uuid) + `planningPeriodKey` (date) | **✗ no 1:1** — the pivotal gap |

## The three acceptance conditions (to close with the MOD-0188 owner)

1. **Identity re-model (the big one).** The draft keys a plan by `revisionId` + `planningPeriodKey`, and its
   only lookup is `GET /revisions/current?planningPeriodKey={date}` (current-by-period) plus
   `GET /revisions/{revisionId}/status|manifest`. There is **no resolve-by-`planId`** — and the consumers
   hold an opaque `DemandPlanId` with no v2 counterpart. So either the producer adds a resolve-from-the
   consumer's reference, **or** the consumers stop binding to an opaque `planId` and instead capture and
   store v2's `(revisionId, planningPeriodKey, contentVersion, integrity.digest)` at bind time. The latter
   changes `MatchesAsync`/`IsExact`'s signatures and the `DemandPlanId/DemandPlanVersion/SourceChecksum`
   fields — a consumer re-model, not a drop-in. This is larger than the request's "one additive operation".
2. **Checksum adoption.** The consumers must store and compare v2's **64-hex uppercase** `digest` +
   `definitionId`, and verify they can trust/recompute the MOD0188-CANONICAL-2 hash over the manifest — the
   draft's own open gap ("full external checksum recomputation … needs independent consumer verification").
   The current `sha256:ee56d4f9a3c8` fixture value is a placeholder, not this.
3. **Permission.** v2 ops require `demand.plans.consume`. 0190/0192 (and the actor) must hold it — a new
   cross-domain grant, the same shape as BL-652's `mdm.legal-entities.read` coupling.

## Validation path under v2 (how MatchesAsync/IsExact would be implemented)

Not one call. Having stored `(revisionId, contentVersion, digest)` at bind time:
`GET /revisions/{revisionId}/status` → `state == Published`; then `GET /revisions/{revisionId}/manifest` →
`contentVersion` and the recomputed/compared `integrity.digest` match the stored values. A `Superseded`/
`Invalidated` state or a digest mismatch ⇒ not exact (false). This is sound and better than binary, but it
is a two-call validation the consumers do not have today.

## Verdict and what it does / does not mean

**ACCEPT WITH CONDITIONS.** The draft genuinely answers the provenance requirement and more; the consumers
*can* be satisfied by it. But acceptance is **conditional on the identity re-model (1), the canonical
checksum adoption (2) and the consume permission (3)** — all consumer-side changes plus one producer
clarification (resolve-from-reference, or an explicit "bind to revisionId" contract). None of this is MVP-6
work: it is the DEMAND-v2 / future-MVP uptake path for 0190/0192, which remain fixture-bound here
(`mvp6-0190-0192-fixture-bound-owner-decision-01`). CT does not edit the draft (MOD-0188 owns it) or the
consumer modules; this review is the feedback the draft asked for.

## For the MOD-0188 owner (next)

Confirm the intended consumer binding: **(a)** add a resolve-by-reference that accepts the consumer's own
plan identity, or **(b)** ratify that consumers bind to `(revisionId, planningPeriodKey, contentVersion,
digest)` and re-model accordingly. Either way, publish the canonical checksum's independent test vector so
the consumers can verify (2).

## Files

- `REPORT.md` — this consumer-acceptance. No draft or consumer code changed.
