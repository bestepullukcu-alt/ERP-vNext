# MVP6-SUPPLIER-SPEC-SPLIT-01 — SOP §22 handoff

**Verdict: SPEC SPLIT PREPARED / POLICY TARGET BOUND / CONCURRENCES OPEN / PACKS DRAFT / NO DEV GO.**

## Decision and scope

The 2026-09-23 user dispatch selects every SS-01…SS-09 option A and AC-01…AC-27 from the exact policy package as the target for the next specification preparation. `DECISION-BINDING.md` records the binding and its limits. It is not treated as MOD-0140, auth/security, Metric Registry, Risk Register, domain/enterprise, strict-consumer or publication-owner concurrence.

No domain config, DCP-009, registry, module pack, canonical contract, permission, gateway or runtime source was changed. Both live packs remain `draft`.

## Split results

| Lane | Bounded result | Output |
|---|---|---|
| MOD-0147 | Nine frozen evaluation/scorecard/risk operations; exact scoring, precision, revision snapshot, risk lifecycle and source acceptance targets; six prospective module roots; AC-01…27 classified | `docs/roadmap/plans/mvp6-mod0147-spec-split-01/` |
| MOD-0148 | Five frozen portal operations; security-owned actor binding, own-record isolation, revocation fencing and actor-isolated replay targets; six prospective module roots; AC-01…27 classified | `docs/roadmap/plans/mvp6-mod0148-spec-split-01/` |
| Shared seam | One coordinator, one GAP list and one remaining-concurrence matrix; no amendment candidate or version selection | `docs/roadmap/plans/mvp6-supplier-spec-split-01/` |

The two prospective module-owned path sets have zero intersection. Shared `Program.cs`, contracts, auth, gateway, permission registries and governance files are explicitly outside both module sets.

## Existing authority versus GAP

FROZEN SUPPLIER v1 can supply tenant-scoped opaque Supplier identity and the four base status values through existing lookup/validation operations. It does not supply LegalEntity eligibility, actor binding, revocation fencing, Metric Registry revisions, Risk taxonomy revisions, portal-source resolution, a trusted endpoint/claim carrier or approval of a successor contract version.

Those missing capabilities remain in `COMMON-SEAM-GAPS.md` under the Central Control Tower Supplier Seam Owner. The module lanes provide consumer requirements only. Neither lane created a separate Supplier amendment or producer design.

## Acceptance and evidence boundary

Each module maps AC-01 through AC-27 exactly once, including explicit delegated/not-applicable rows. Prospective executable tests, static checks, declarations and simulated fixture checks are labeled separately. `supplier-exact-policy-fixture/1` remains a future test-only proposal; fixture results cannot establish a live Supplier, security, Metric or Risk producer.

No runtime/build/HTTP/JWT/Mongo test was performed because no implementation changed. This output is specification preparation, not Phase 1.5, runtime acceptance, pack promotion, E5/G5 or rollout.

## Remaining gates

1. Enterprise/domain concurrence and separately authorized governance alignment.
2. MOD-0140 live identity/status and Tenant+LE eligibility disposition.
3. Platform security binding, trusted LE, current membership/permission and revocation-fence disposition.
4. Metric and Risk owner artifacts for immutable revisions, policy/taxonomy and source validation.
5. One CT contract writer's exact successor/version/compatibility package, if the above decisions require wire changes.
6. Separate module-owner pack amendments, technical Phase 1.5, explicit promotion and runtime authority.

Until these gates close, both module dispatches remain HELD.
