# SOP §22 — MVP6-PACK-ALIGNMENT-03-RETURNS / MOD-0186 Reverse Logistics (CT queue Q36)

**Verdict: PREPARED FOR OWNER REVIEW — NOT APPROVED, NOT APPLIED.** The shared pack stays `draft` and unchanged
(`07a8a015…b7`) until the decision in [SIGN-OFF-DECISION.md](SIGN-OFF-DECISION.md) is recorded. The old package
`docs/roadmap/plans/mvp6-final-pack-delta-01/` is unchanged (SHA256SUMS 6/6 OK). No business decision is reopened.

## Problem

The shared pack says `status: draft`, "amendment and Phase1.5 OPEN; DEV/VER HELD". Meanwhile the owner granted conditional promotion
(2026-09-20) and the exact replacement (2026-09-21), Phase 1.5 was closed for the isolated core, an isolated copy was promoted to
`ready-for-dev` (`1c80cca7…`), and CT accepted the bounded Returns work package (`mvp6-mod0186-wp-acceptance-01`). The
recorded alignment `731622d2…` is a pseudo-patch that `git apply` and `patch` reject, and its replacement `2a1843eb…` alone yields a
`draft` pack (`745e9cc7…`). No real patch took the shared pack to the promoted one.

## Lineage (all hashes rechecked 2026-09-26)

| Step | Identity | Source |
|---|---|---|
| Shared pack today | `07a8a0159b5ab02fecc7b1aa6314106d81b1584150169f457cb9a395614a04b7`, `draft` | common checkout |
| Historical pseudo-patch (not used) | `731622d2e6104ebd1e161082481860a9ea45ea0d39b25ef06bb226c4dbf2ab7d` | `mvp6-final-pack-delta-01/` (unchanged) |
| Conditional owner grant, 2026-09-20T11:44:45Z | `authority-source.md` `447fb83b…667d` | `docs/records/audits/2026-09/mvp6-mod0186-patch-disposition-02/` |
| Exact replacement grant, 2026-09-21 | `authority-source.md` item 2, `a57efad0…f979` | `docs/records/audits/2026-09/mvp6-mod0186-http-composition-01/` |
| Approved replacement delta | `2a1843eb52b8fd0b61a553c06a75d07b32dda57bf020734d946c004b82893709` → `745e9cc7…` (draft) | `mvp6-mod0186-pack-patch-repair-01/`; applied in isolation per `mvp6-mod0186-pack-activate-01` |
| Phase 1.5 closure (copy) | `PHASE15.md` `c921c423e6c354753b550e4520940479d260d8705b7f937210abc91f228ef0a7` (the hash CT cites) | `docs/records/audits/2026-09/mvp6-mod0186-http-01/authority/` |
| Owner-promoted isolated pack (`ready-for-dev`) | `1c80cca70ef774d18f5d30174566e68c9b6332b61dd1f564d54de3429886482f` | three byte-identical archived copies: `mvp6-mod0186-http-01/authority/`, `mvp6-mod0186-r01-rework-01/authority/`, `mvp6-mod0186-r01-independent-ver-01/authority/` (all listed in their evidence SHA256SUMS; the last verifies 165/165) |
| CT acceptance | `SOP-22.md` `3a61b6e5…0e`, matrix `55733e99…610f` (SHA256SUMS 2/2 OK) | `docs/records/audits/2026-09/mvp6-mod0186-wp-acceptance-01/` |
| Accepted source/evidence | manifest `60ab3d68…1052`, patch `0bb36d02…e82c`, archive `1191b5d9…7473`, binary `daeefa6c…49b7`, 46 paths `returns46.json` `96f43bd7…5969` | `mvp6-mod0186-r01-independent-ver-01/` |
| **Proposed aligned target** | `f4396e8a03f663e34526e3bdaa4c85a4f3c96683ae73bae608f2981e87b33b1f`, `ready-for-dev` | [alignment.patch](alignment.patch) `8af3287ce76a363bbe5bf9c7c2bea0b2250d7ca3bdc88f30603f339551c68c3b` |
| **Proposed UI target** | `6c8fbe28f0b058cacf08aaf3941e82845b0842db3f8b8f295a753e746a65a5a0`, `ready-for-dev` | [ui-revision.patch](ui-revision.patch) `62c7b16417bb63a35589f790336f649d332bf0e97095aca05d0d910a156b9d63` |

`1c80cca7…` differs from the shared pack in exactly: `status: draft` → `ready-for-dev`; its `status_note`; the one §28 published-contract
binding line from `2a1843eb…`; and the new §30 PHASE15-CLOSE-01.

## What alignment.patch does

It turns the shared pack into the owner-promoted pack **byte for byte** (proved: removing the three changes below from the target gives
`1c80cca7…` exactly), then makes three further changes, mirroring `mvp6-pack-alignment-02-claims`:

1. `status_note` points to the accepted work package and its open boundaries. `status` stays `ready-for-dev`, never `done`.
2. §6 Protected Paths gains one line naming the published SHIPMENT-BUNDLE YAML and the Returns/root annexes as read-only.
3. A new §31 "Accepted bounded scope binding" records the owner authorities, Phase 1.5 record, CT acceptance, R01–R11 matrix, source/evidence
   and contract hashes, the 46 owned paths and the open gates. It includes a precedence note: the stale "draft / HELD / 2.0.0 / promotion NOT
   AUTHORIZED / candidate" wording in §§28–29 and §30's "not current PASS" are historical.

No acceptance row, lifecycle, quantity/UoM, permission, error, replay, root or owned-path rule is edited.

## What ui-revision.patch does

It stacks on the aligned target, in the same shape as the Claims UI revision (`mvp6-ui-pack-revisions-01/claims/`): header fields
`shell: none`→`tenant`, `golden_reference: none`→`slim`, `form_field_count: 0`→`6`, plus a new §32 with the approved Returns UI scope
(identity; layout/shell; bound operations; screens/routes/permissions incl. `supplychain.shipments.read` and the Returns-required
X-Tenant/LE headers from signed claims; bounded DataTables v2 profile; 6-field create offcanvas with the Shipment-line picker; the 7-arrow
transition map with manual-assertion Received and conditional disposition fields; error/replay/concurrency; 7 languages, UAS-001,
safe-not-found; 21 owned UI paths, protected paths, single integration-owner handoff with the gateway routes **listed**; the single acceptance
matrix RU-VS1 first, RU-01…29, OUT rows RU-SCR-01…06; test expectations; gaps; O/M/P and the authorization boundary). Aligned lines 17–590 are unchanged; `status` stays `ready-for-dev`.

## Checks (scratch copies outside the repo; full output reproduced below)

| Check | Result |
|---|---|
| Repo / branch / HEAD | `/Users/natig/Projects/ERP-vNext-recovery`, `feature/mvp6-logistics`, `4a8d4d4b339528a88e6220fb8402e5a2c771136c` |
| Shared pack hash | `07a8a015…b7` — matches the brief |
| DCP-002 `verify_module_id.py . --check-id MOD-0186 --name "Reverse Logistics"` | `OK`, exit 0 |
| Owner UI-scope decision | `mvp6-returns-claims-ui-scope-owner-decision-01.md` now `2ce9aa7d2f9db4e4d9440ae767f6f9d0e695f81d84b16f61c1832007c0d7df10` (administrative fix r1: `decided_at_local` 01:24); its bound hashes still hold: drafts SHA256SUMS `b568c94f…` (13/13 OK), `returns/APPROVAL-DECISION.md` `edb2b06d…` |
| alignment.patch on shared pack | `git apply --check` OK; `git apply` and `patch --fuzz=0 -p1` → `f4396e8a…`, byte-equal |
| Target minus the three additions | byte-equal to `1c80cca7…` |
| ui-revision.patch on aligned target | `git apply --check` OK; `git apply` and `patch --fuzz=0 -p1` → `6c8fbe28…`, byte-equal |
| Order A→B from the shared pack | Both tools → `6c8fbe28…`, identical to stepwise |
| Negative: UI patch on the shared pack | Fails (git apply: "patch does not apply"; patch: 2 of 3 hunks FAILED) |

```
## A. alignment.patch on shared pack
preimage 07a8a0159b5ab02fecc7b1aa6314106d81b1584150169f457cb9a395614a04b7
git apply --check: OK
git apply: OK -> f4396e8a03f663e34526e3bdaa4c85a4f3c96683ae73bae608f2981e87b33b1f
patch --fuzz=0: OK -> f4396e8a03f663e34526e3bdaa4c85a4f3c96683ae73bae608f2981e87b33b1f
BYTE-EQUAL
## A2. Target minus status_note/§6 line/§31 equals owner-promoted 1c80cca7
reduced 1c80cca70ef774d18f5d30174566e68c9b6332b61dd1f564d54de3429886482f promoted 1c80cca7…482f EQUAL
## B. ui-revision.patch on aligned preimage
git apply --check: OK
git apply: OK -> 6c8fbe28f0b058cacf08aaf3941e82845b0842db3f8b8f295a753e746a65a5a0
patch --fuzz=0: OK -> 6c8fbe28f0b058cacf08aaf3941e82845b0842db3f8b8f295a753e746a65a5a0
BYTE-EQUAL
## C. Combined order A->B from shared pack
git apply A then B: 6c8fbe28…a5a0 · patch A then B: 6c8fbe28…a5a0 · combined == stepwise: YES
## D. Negative: B directly on shared pack
error: … patch does not apply · Hunk #1 FAILED at 3. Hunk #3 FAILED at 588. 2 out of 3 hunks FAILED
```

## Remaining gaps (honest list)

1. **Missing original Phase 1.5 package:** §30 of the promoted pack cites `docs/records/audits/2026-09/mvp6-mod0186-phase15-close-01/authority.md`, which is **not** in the common checkout. The Phase 1.5 content is bound here through the byte-identical copy `mvp6-mod0186-http-01/authority/PHASE15.md` (`c921c423…`, the hash CT cites) and the owner grants through the two `authority-source.md` records. The promotion step itself (from `745e9cc7…` to `1c80cca7…`) is therefore evidenced by its output bytes and the CT acceptance, not by a promotion record in this checkout. CT may want that package imported or a successor record.
2. **Owned-path list source:** the controlling `owned-paths.txt` of PHASE15-CLOSE-01 is also absent; `returns46.json` (46 paths, no worker, no `Program.cs`) from the R01 independent VER is used instead. Its correspondence to the original allowlist is asserted by CT acceptance, not re-derived here.
3. **Stale body text kept:** §§28–30 retain historical wording; §31 states precedence rather than rewriting owner-promoted text (same choice as Claims option A).
4. **UI gaps shared with Claims:** G-MODAL (`window.showConfirm` hosting transition inputs); G-ICONMAP (shared icon test file); G-DATETIME (`occurredAt` with offset); the verifier PASS expectation vs the approved OUT rows. Returns-specific: the line picker inside a Slim offcanvas must pass the 390/768 gate; the full 422 code set is not named in the annex and must be bound from the accepted backend at Phase 1.5. The Claims approved-amount list gap has **no** Returns equivalent (`ReturnSummary` carries no amount).
5. **Integration target:** 43 accepted Returns paths are absent from the common checkout; gateway routes, permissions, navigation, shared L10n, personalization codes and producer root emission are integration-owner work (Q14/Q15).
6. **Inherited open items:** DN-01 retry policy; PNG (PRES-183-04); Loads 3.1.0 forward drift of the `5dfe7c1b…` pin; `module-implementation-status.md` update (separate path); verified inbound receiving and Inventory reconciliation stay outside the bounded slice.
7. **Cross-package observation (Q35, not edited):** the Claims UI patch `mvp6-ui-pack-revisions-01/claims/ui-revision.patch` §32 cites the owner decision record by its pre-correction hash `30931917…`; after the CT administrative fix the record is `2ce9aa7d…`. The drafts' bound hashes are unaffected. CT may want the Claims patch re-issued or the citation read as historical.

## Changed files

Only this package directory. The pack, contracts, product code, `.antigravity`, gateway, existing records and packages (including
`mvp6-final-pack-delta-01/` and `mvp6-ui-pack-revisions-01/`) were not changed. Git use was limited to `rev-parse` with `GIT_OPTIONAL_LOCKS=0`. No commit, push or stash.
