# LOADS-ROOT (RS-05) — options for the Load-root read

🤖 Applying knowledge of @orchestrator + @module-pack-author + @read-only-auditor.

**NOT APPROVED — prepared text only.**

## Where the decision stands (from the records)

| Step | Record | sha256 | State |
|---|---|---|---|
| Root acquisition design D185-ROOT-ACQ-01: extend `queryLoads` — each `LoadSummary` gets optional/nullable `lifecycleCorrelationId`, sourced only from persisted `LoadPlan.CorrelationRoot`; missing/null/malformed → transition unavailable (fail-closed); no derivation, no backfill | `docs/records/audits/2026-09/mvp6-loads-root-acquisition-disposition-01/OWNER-DECISION-PACK.md`; `SOP-22.md` | `ce972cb2…`; `74f39739…` | design adopted into the 3.1.0 candidate |
| A — select 3.1.0 / wire v1, consumer consent, external inventory = none | `docs/records/decisions/2026-09/mvp6-loads-root-amendment-owner-decision-a-01.md` | `e12d5975…` | DONE (Q07) |
| B — canonical publication | `docs/records/decisions/2026-09/mvp6-loads-root-amendment-owner-decision-b-01.md` | `36fe774a…` | DONE; published and CT ACCEPTED 2026-09-26 (Q25/Q32) — annex `docs/analysis/contracts/loads-semantics-v3.1.0.md` `9d8a3706…` line 207 |
| C — producer uptake (emit the root) | `docs/records/audits/2026-09/mvp6-loads-root-amendment-release-prep-01/OWNER-DECISION-PACK.md` §C | `51f7be10…` | **open (Q09)** |
| RS-05 in the ledger | `docs/roadmap/plans/mvp6-effort-shipment-ct-update-07/UNESTIMATED-SCOPE.tsv` | `735b63d4…` | MANDATORY_UNESTIMATED |

The contract shape is therefore already decided and published. What RS-05 still needs is the **producer implementation** and its scope. `LoadPlan.CorrelationRoot` already exists as a persisted field (`services/Diten.SupplyChainService/src/Diten.SupplyChainService.Domain/Features/Loads/LoadPlan.cs`). The transition UI (mandatory, `mvp6-loads-remaining-scope-estimate-01/REPORT.md` `c580f62b…`) depends on it; list/create UI does not.

## Options

| | A — Producer uptake as published (grant C) | B — Uptake + backfill of missing roots | C — New Loads detail/root endpoint |
|---|---|---|---|
| What | One Loads producer WP in the Loads isolated env: `queryLoads` emits `LoadPlan.CorrelationRoot` as `lifecycleCorrelationId` under the approved missing/null/malformed/valid/nil policy; rows without a valid root show no transition action | A, plus a one-time repair that writes roots for legacy `LoadPlan` rows lacking one | A new by-ID read (contract 3.2.0) returning the root; could also serve the open detail-page question (RS-06) |
| Contract change | none (3.1.0 already published) | none on the wire, but contradicts "no backfill" in D185-ROOT-ACQ-01 → new owner decision | new candidate, VER, consumer consent, publication, guard binding |
| Pros | Smallest step; uses decisions A/B; unblocks the transition UI; no data mutation | Old rows also become transitionable | Cleaner long-term read model; one call per row |
| Cons | Legacy rows without a valid root cannot transition (fail-closed, by design) | Data migration in a module still mock-first; no evidence that such rows exist; needs its own approval and VER | Repeats the whole contract cycle just finished; delays Loads by that cycle; overlaps an unanswered scope question |
| Effort | **UNESTIMATED** (RS-05); the uptake WP preflight must give O/M/P before DEV. Existing reserves `0185-2-REMAINING` 4.8/8/12.8 and `0185-3-REMAINING` 9.6/16/25.6 may partly cover it — not assumed | UNESTIMATED (A + migration) | UNESTIMATED (contract cycle + producer + consumer) |
| Runs where | Mac Terminal (build, Mongo, restart, independent runtime VER) | Mac | Chat (contract prep) + Mac |

**Recommended: A.** It is the path the owner already set up with D185-ROOT-ACQ-01, A and B; B and C reopen decisions without evidence that they are needed. Sequencing under "modules first": the Loads list/create UI can be built before the uptake; the transition UI waits for the uptake VER.

## Owner question (one line)

For the Load-root read (RS-05), do you grant the producer uptake as published in 3.1.0 (option A, decision C), without backfill or a new endpoint?

**Recommended answer: Yes (option A).**

## Exact decision text (NOT APPROVED — prepared text only)

> For RS-05 I choose option A in `docs/roadmap/plans/mvp6-decision-prep-01/LOADS-ROOT-OPTIONS.md` and grant decision C of `docs/records/audits/2026-09/mvp6-loads-root-amendment-release-prep-01/OWNER-DECISION-PACK.md` (sha256 `51f7be10befc6deb1d76b5a80a3df0e194157fa24a9380fa16be6d4f1513018a`): a separately scoped Loads producer uptake package may emit the persisted `LoadPlan.CorrelationRoot` through `LoadSummary.lifecycleCorrelationId` under the approved missing/null/malformed/valid/nil policy, with no derivation and no backfill, in the Loads isolated environment. The package must name its exact owned source/test paths and give an O/M/P estimate in its preflight, and it requires independent runtime verification on the Mac. This does not authorize the Loads UI, gateway, shared files, rollout, pack promotion, a new endpoint, backfill, commit or push.
