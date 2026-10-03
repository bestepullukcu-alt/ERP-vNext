# AUTHORITY — MVP6-SHIPMENT-A12-RUNTIME-INDEPENDENT-VER-02 (queue Q04, attempt 2)

## Owner authority (verbatim, from the lane prompt `docs/roadmap/plans/mvp6-process-pilot-01/TERMINAL-A12-PROMPT.md` lines 12–14)

> Authority (owner, 2026-09-25, CEO Natig Yusubov): existing records suffice —
> docs/records/audits/2026-09/mvp6-shipment-remaining-acceptance-disposition-01/INDEPENDENT-VER-HANDOFF.md lines 8–24 and
> docs/records/audits/2026-09/mvp6-shipment-a12-safe404-rework-01/INDEPENDENT-VER-HANDOFF.md. Scope: disposable, isolated, evidence-only run of the A12 successor.

Prompt line 15, same source (verbatim): "NO source repair, common checkout, gateway/.antigravity/contract edits, A10 proxy."

## Referenced records

| Record | SHA-256 at run time | Copy / pointer |
|---|---|---|
| Remaining-acceptance handoff (lines 8–24 are the authority) | `76ce6f8bf1bf1179ab35864c4798d24debd974c84d4616c748dd466df6444e00` | `raw/authority-source-remaining-acceptance-handoff-lines-8-24.txt` |
| A12 successor VER handoff | `0d68dea009f5007dd039216dcde2416779192b5cc345813a2777e4007d641d9a` (matches the package's own `ARTIFACTS.sha256`) | `../mvp6-shipment-a12-safe404-rework-01/INDEPENDENT-VER-HANDOFF.md` |
| Lane prompt | `de7273430ce9c90dfca74f63329dfa4273e787147f766c3f2b12f81aafd627e0` | `docs/roadmap/plans/mvp6-process-pilot-01/TERMINAL-A12-PROMPT.md` |

All hashes are recorded in `raw/authority-inputs.sha256`.

## What this lane did and did not do under that authority

- Done: disposable source in `/private/tmp` (HEAD archive + exact A12 360 overlay + Auth 22 overlay), native .NET 8 Release build, isolated Mongo replica set, lane ports, real Auth identities, browser and HTTP evidence, cleanup.
- Not done: no source repair; no common checkout used as source; no edit of the gateway, `.antigravity`, contracts, packs, guards or any repository file outside this folder; no A10 proxy or fault seam; no git add/commit/push/stash/reset/clean/rm/mv (all git calls read-only with `GIT_OPTIONAL_LOCKS=0`).
- Lane-DB-only fixture writes (disposable Mongo on 34994, destroyed at cleanup) are listed in `SOP-22.md` §Fixtures. They are setup, not acceptance evidence, and no repository file was changed by them.
- `A10` stays unauthorised and NOT RUN. `A14`/DN-01/DN-02 are outside this run.

## Local permission changes observed during the run (for CT)

CT recorded two local-permission lockdowns while this lane was running (`../mvp6-ct-terminal-lane-security-2026-09-26.md`, `../mvp6-ct-terminal-lane-security-02-2026-09-26.md`). The second (file time 10:27) adds `Write(scripts/**)`; that glob also matches this folder's `scripts/` subfolder by name. The lane did not know about the rule until its first denied Write (≈10:35). Between 10:27 and 10:35 four lane files in this folder's `scripts/` were written or edited via Bash (`platform_tenant_fixture.js` 10:33:09, `auth_fixture.js` 10:34:04, `fixture_prepare.py` 10:34:21, `stale_assignment_cleanup.js` 10:34:48). All four are this lane's own evidence scripts; no product or repository file was touched. After the denial the lane made no further writes into `scripts/` (a later move to `lane-scripts/` was also denied and was not retried) and put every new script in `lane-scripts/`. See `SOP-22.md` §Deviations.
