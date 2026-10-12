# MVP6 parallel-wave plan — "modules first, integrate last" (proposal, NOT APPROVED)

🤖 Applying knowledge of @read-only-auditor + @product-manager.

Basis: owner decisions 2026-09-26 (`docs/records/audits/2026-09/mvp6-ct-owner-decisions-modules-first-2026-09-26.md`), process v1.0 §8 pilot limit,
MODULE-READINESS.tsv, BLOCKING-DECISIONS.tsv. Nothing here is dispatched; CT writes each prompt.

## Rules applied

| Rule | How the waves respect it |
|---|---|
| ≤ 2 product lanes + 1 environment lane | Each wave names exactly P1, P2 and ENV. Lane-3 (independent verifier) and Lane-4 (owner decisions) are not product lanes. |
| One writer per shared seam | Waves 1–3 **edit no shared file** (`Program.cs`, `gateway/**/ocelot.json`, nav/menu, `SharedResource.*.resx`, Auth, packs, contracts). Each module writes its shared changes as a **per-module overlay package** in its own output folder. The single integration owner applies and reconciles all of them once, in Wave 4. |
| One writer per ledger | `CT-QUEUE.tsv`, `MILESTONE-EVENTS.tsv`, `BLOCKERS.tsv`, effort files: written only by the step CT names for recording (today: the lane after each CT verdict; Q57 lane now). No wave lane writes a ledger. |
| Runtime only in Mac Terminal | Every build, test, MongoDB or browser step below is marked **Mac**. Chat lanes do documents only. |
| Mac concurrency | P2 and ENV can both be Mac sessions only with separate ports and Mongo replica sets (kit lane ports, as in A12 VER-02); otherwise CT runs them one after the other (ASSUMPTION A3). |
| Isolated environment | `git archive 4a8d4d4b` + that module's accepted overlays (recipe: `mvp6-shipment-a12-runtime-independent-ver-02/BUILD-INPUT-MANIFEST.tsv`, 0 missing). Never the dirty common checkout. |

## Wave 1 — start now (no owner decision needed to start)

| Lane | Run where | Work package | Owned paths (new files only) | Depends on |
|---|---|---|---|---|
| P1 | Chat | **WP-DECISION-PREP-01**: (a) Phase 1.5 9-row tables for Returns and Claims UI; (b) SR-D4 decision text; (c) retroactive UI Phase 1.5 tables for Shipment and Carrier; (d) LOADS-ROOT options (RS-05) | `docs/roadmap/plans/mvp6-decision-prep-01/` | packs 0183–0187 as they are |
| P2 | **Mac** | **WP-183-ISO-FINAL-VER**: fresh rerun of Shipment rows A01, A02, A03-missing-read, A04–A07, A15 on HEAD + A12 360 + Auth 22 overlays, with PNG (Q56 method). A10/A13/A14 stay open (decisions). | `docs/records/audits/2026-09/mvp6-shipment-iso-final-ver-01/` | EVIDENCE-REUSE.tsv; Q58 optional |
| ENV | **Mac** | **E1 WP-ENV-CARRIER-SRC**: archive the accepted Carrier v3 UI (21 paths) into the repo, byte-checked against manifest `3b7086f0…`; if the bytes are gone, STOP and report. Then **E2 WP-ENV-ISO-RECIPE**: one isolated-environment recipe per module (0185, 0186, 0187, 0190, 0192) from BC-SOURCE `ebd5d80c…` + Auth `f50350b8…`, dry-run build only. | `docs/records/audits/2026-09/mvp6-carrier-ui-source-archive-01/`, `docs/roadmap/plans/mvp6-iso-env-recipes-01/` | none |
| Lane-3 | Chat | Independent check of WP-DECISION-PREP-01 (text) | its own folder | P1 |
| Lane-4 | Owner | Decisions in the order of PLAN-v9.3-DRAFT §5 | — | P1 texts |

After P2: **WP-184-ISO-FINAL-VER** (Carrier final isolated UI VER + durable PNG) on HEAD + Auth 22 + archived Carrier UI (needs E1).

## Wave 2 — after SR-D4 and PH15-UI-186/187 (blocked only by owner decisions)

| Lane | Run where | Work package | Owned paths | Depends on |
|---|---|---|---|---|
| P1 | **Mac** | **WP-186-UI-BUILD**: Returns UI per pack §32 in isolated env; early vertical slice first; self-registration provider + tests + nav-key values as overlay (if SR-D4 = A) | owned UI paths in pack §32 + `…/mvp6-returns-ui-dev-01/` (incl. `SHARED-OVERLAY/`) | PH15-UI-186, SR-D4, E2 |
| P2 | **Mac** | **WP-187-UI-BUILD**: Claims UI per pack §32, same pattern | pack §32 paths + `…/mvp6-claims-ui-dev-01/` | PH15-UI-187, SR-D4, E2 |
| ENV | **Mac** | **E3 Q24** evidence kit v1.1 validation; real-Auth fixture identities for Returns/Claims (3 identities, G-SHIPREAD grants) in the isolated env | kit folders | Q58 |
| Lane-3 | **Mac** (different session) | Independent UI VER of each build on its frozen source | own VER folders | P1/P2 writer complete |
| Lane-4 | Owner | Q11 → Q10 → Q12 (Shipment), Q27, Q28, Q09 | — | — |

## Wave 3 — Loads, S&OP/Capacity, self-registration catch-up, Supplier start

| Lane | Run where | Work package | Depends on | Blocked only by decision? |
|---|---|---|---|---|
| P1 | Mac | **WP-185-UPTAKE** (producer uptake), then root read (RS-05), then **WP-185-UI-BUILD** (list/create + transition) | Q09, LOADS-ROOT, LOADS-UI-SCOPE, RS-06/07 | **yes** |
| P2 | Chat → Mac | **WP-190/192-UI-SCOPE** (chat), then UI builds (Mac); **WP-183/184-SR-OVERLAY** (provider overlays for the built UIs) | Q27, Q28, UI-SCOPE-190/192, SR-D4 | **yes** |
| ENV | Mac | Isolated envs for Loads (live producer) and S&OP/Capacity; closure of shared health 503 (B08) observation | — | no |
| (queued) | Chat | **Supplier 0147/0148** pack completion — starts in the first free product slot after Q19 | Q19 DC-01..05 | **yes** |

Supplier is the long pole (480 h M remaining, reserve-based). If Q19 is decided early, CT should give Supplier a product slot in Wave 3 ahead of S&OP/Capacity UI.

## Wave 4 — final integration (single integration owner, Mac)

1. Q14 refresh: new selection from BC-SOURCE `ebd5d80c…` + A12 360 + Auth 22 + Carrier UI + each module's accepted UI source (supersedes the stale 2026-09-23 selection `cdc6228a…`).
2. Q15 owner decision → one registered integration checkout; **one writer** applies every per-module shared overlay (Program.cs, ocelot, nav, resx, Auth) and the DCP-009 §21 foundation, in a fixed order.
3. Golden-flow vertical slice; self-registration reconcile tests R-01…R-04; Phase 4.5 runtime smoke per module in the integrated target; golden flow S5; E5/G5.
4. Independent integration VER (Mac, different session) → CT.
5. Phase 6 closure docs per module (chat lanes, two at a time) → Q42; release checklist; one push at the end of MVP6 (working-mode record).

## Chat vs Mac summary

| Runs in chat lanes | Runs in Mac Terminal |
|---|---|
| Decision texts, Phase 1.5 tables, UI scopes, pack patches, overlay registers, Phase 6 docs, text VERs | Builds, tests, MongoDB, browser/PNG, UI builds, runtime VERs, env recipes, integration, commits |
