# SOP §22 — MOD-0186 Returns UI-REVISION-01 (CT queue Q35)

**Verdict: BLOCKED — no UI revision patch produced.** There is no valid alignment preimage. By owner choice in this lane
("Claims now, Returns waits"), Returns waits for a proper alignment package. The shared pack is unchanged (`07a8a015…b7`, `draft`).

## What was checked

| Item | Result |
|---|---|
| Named alignment input | `docs/roadmap/plans/mvp6-final-pack-delta-01/` — SHA256SUMS 6/6 OK (`84321a1f…5412`) |
| Its `proposed-pack.patch` (`731622d2e6104ebd1e161082481860a9ea45ea0d39b25ef06bb226c4dbf2ab7d`) | **Not an applicable patch.** It has bare `@@` pseudo-hunks with no file headers or line numbers and **declares no target hash**. `git apply --check` → "unrecognized input"; `patch --fuzz=0` → "Only garbage was found". The same defect is already recorded in `mod-0186-runtime-dispatch-01/SOP-22-PACK-ACTIVATE.md` (20 Sep: exit 128) |
| Its status stance | Keeps `status: draft` ("NOT READY-FOR-DEV"), which conflicts with the brief's "keep status ready-for-dev" |
| Recorded replacement | `mvp6-mod0186-pack-patch-repair-01/proposed-pack-v2.patch` (`2a1843eb…3709`): real unified patch, shared `07a8a015…` → `745e9cc7…`, `status: draft`; replacement approval handled in `mvp6-mod0186-patch-disposition-02` and applied in an isolated checkout per `mvp6-mod0186-pack-activate-01` |
| Owner-promoted `ready-for-dev` pack | `1c80cca7…` (cited by `mvp6-mod0186-wp-acceptance-01`). No shared alignment patch that reproduces it (the equivalent of Claims `alignment-02`) exists in `docs/roadmap/plans/` |
| DCP-002 | MOD-0186 already `OK` exit 0 in the draft lane (2026-09-26); no new ID |

## Why no patch

A UI revision needs an exact, verifiable preimage. The named alignment cannot produce one. The repaired v2 patch produces a
`draft` pack that differs from the accepted `ready-for-dev` pack. Stacking the UI section on it would either contradict the
brief (status) or need a rebase once a real Returns alignment exists. The owner chose to wait instead of producing a patch that must be redone.

## What unblocks it

CT dispatches a **Returns pack-alignment-02** (the pattern of `mvp6-pack-alignment-02-claims`). It turns the shared pack `07a8a015…` into the owner-promoted
`1c80cca7…` plus the accepted-scope binding (`mvp6-mod0186-wp-acceptance-01`), declares preimage/target hashes, and passes `git apply --check`
and `patch --fuzz=0`. The Returns UI revision (§ equivalent to Claims §32, content from the approved `mvp6-ui-pack-drafts-01/returns/`) is then prepared on that target in a
follow-up lane. Estimated effort for that follow-up is within the drafts' "pack revision 4/8/16 h" row.

## Returns-specific gaps to carry into that lane

The Claims gaps G-MODAL, G-ICONMAP, G-DATETIME and the verifier tension apply equally to Returns. In addition:
`dispositionCode`/`inventoryTransactionReferenceId` conditional fields; "Received (manual assertion)" wording; the
`X-Tenant-Id`/`X-Legal-Entity-Id` headers **required** by the Returns family (from signed claims only); `RETURN_SOURCE_CHANGED`; and 43 accepted Returns paths absent from the common checkout.
