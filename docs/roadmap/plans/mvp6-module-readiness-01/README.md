# MVP6 module readiness under "modules first, integrate last" (Q59)

🤖 Applying knowledge of @read-only-auditor + @product-manager. Lane AL-MVP6-READINESS-01 (INS), chat lane on the linked Mac folder.
Repo `feature/mvp6-logistics` @ `4a8d4d4b339528a88e6220fb8402e5a2c771136c`. Start 2026-09-26T11:54:07+03:00. Read-only analysis; new files only.

**PROPOSAL — NOT APPROVED.** Nothing here changes a pack status, ledger, plan or record.

## Summary

| Module | Pack | Remaining (M h / % of ledger total) | Latest accepted source | Next work package | Main blocker |
|---|---|---|---|---|---|
| 0183 Shipment | ready-for-dev | 46 / 15.6 % | HEAD + A12 360 `7b6a0d1a…` + Auth 22 `f50350b8…` | WP-183-ISO-FINAL-VER (Mac) | Q11/Q10/Q12 for A14/A10/A13 |
| 0184 Carrier | ready-for-dev | 28 / 13.5 % | backend in BC 422 `ebd5d80c…`; **UI v3 not archived** (manifest `3b7086f0…` only) | E1 archive UI source, then WP-184-ISO-FINAL-VER + PNG (Mac) | none for isolation; UI source at risk |
| 0185 Loads | ready-for-dev (backend) | 172 / 55.8 % + unestimated root/detail/lookup | 47 paths in 379 `8fa00d40…` / BC 422; contracts 3.1.0 in working tree | WP-185-UPTAKE (Mac) | Q09, LOADS-ROOT, LOADS-UI-SCOPE |
| 0186 Returns | ready-for-dev (UI §32) | 106 / 38.1 % (+26 Q33, not in ledger) | normal archive `edb759a0…` | WP-PH15-186 → WP-186-UI-BUILD (Mac) | PH15-UI-186, SR-D4 |
| 0187 Claims | ready-for-dev (UI §32) | 114 / 36.3 % (+16 Q33) | normal archive `edb759a0…` | WP-PH15-187 → WP-187-UI-BUILD (Mac) | PH15-UI-187, SR-D4 |
| 0190 S&OP | draft (isolated pack promoted) | 136 / 47.9 % | 379 `8fa00d40…` | Q27, then UI scope (chat) | Q27, UI-SCOPE-190 |
| 0192 Capacity | draft (isolated pack promoted) | 144 / 39.6 % | BC 422 `ebd5d80c…` (manifest `dec28b6a…`) | Q28, then UI scope (chat) | Q28, UI-SCOPE-192 |
| 0147 Supplier Perf. | draft | 248 / 92.5 % | NOT FOUND (no source) | Q19, then pack completion | Q19 DC-01..05 |
| 0148 Supplier Portal | draft | 232 / 92.1 % | NOT FOUND | Q19, then pack completion | Q19 DC-01..05 |
| SHARED | DCP-009 approved | 164 / 60.3 % (+self-reg shared part) | Program.cs patch `78cc0fa6…`; selection `cdc6228a…` stale | E2 isolated-env recipes; final integration | SR-D4; Q58; Q15 at the end |

Portfolio (ledger): 1,390 of 2,842 M hours remaining (48.9 %). No module has passed Phase 1.5 for UI, Phase 4.5 in an integrated target or Phase 6.

## Key findings (in scope)

1. **D4 conflict:** the self-registration rule "provider ships with the module UI in the integrated target" cannot hold when integration is last. Decision SR-D4 is the first item in the queue.
2. **Carrier v3 UI source is not in the repository** — only its 21-path manifest and a 2-file delta. The accepted bytes exist only in a Mac temp snapshot. Archive them before anything else (E1).
3. **The 2026-09-23 integration selection is stale.** The latest bounded-accepted backend composite is BC-SOURCE `ebd5d80c…` (422 rows, manifest `dec28b6a…`), which also serves as the isolated-environment base for 0184–0192.
4. **Wave 1 can start without any decision:** decision prep (chat), Shipment final isolated VER (Mac), Carrier source archive + recipes (Mac env). Waves 2–3 are blocked only by owner decisions.
5. S&OP and Capacity have no UI scope and no self-registration section; Supplier has no source at all and is the long pole (480 h M).

## ASSUMPTIONS

- **A1** "% remaining" = ledger remaining M ÷ total M from `mvp6-effort-shipment-ct-update-07/MODULE-SUMMARY.tsv`. Estimates not in the ledger (Q33 UI, self-registration) are listed separately (Q51 pending). Nothing new was estimated; missing items are "UNESTIMATED".
- **A2** Latest accepted backend source for 0183–0192 = BC-SOURCE 422 `ebd5d80c…`, the newest bounded-accepted composite (`mvp6-bc-successor-ct-handoff-01/README.md`); Shipment UI/backend successor = A12 360 overlay.
- **A3** The pilot limit counts lanes, not machines. Two Mac runtime sessions may run together only with separate ports and Mongo replica sets; otherwise CT serializes them.
- **A4** Q03 is still DECISION-REQUIRED in CT-QUEUE, but `mvp6-ct-working-mode-2026-09-26.md` records owner permission for local add/commit from Mac sessions; treated as partially decided.
- **A5** Returns/Claims UI effort uses Q33 Scope A, because Q34 approved the full scope.
- **A6** Shipment and Carrier UIs were built before the Phase 1.5 gate was enforced; a retroactive UI table (or a recorded waiver) is assumed needed to close them.
- **A7** "Finished in isolation" is defined in PLAN-v9.3-DRAFT §2 from the consequences CT stated in decision 1.
- **A8** The Claims pack shows an isolated DEV GO (§30) but no explicit Phase 1.5 approval record; reported as NOT FOUND in the pack.
- **A9** Manifest `92879d20…` for the normal archive is taken from SOURCE-SELECTION.md, not re-hashed (the manifest file was not located in this lane).
- **A10** The Carrier v2 snapshot under `/private/tmp` is known only from records; this lane cannot see it.

## Files

| File | Purpose |
|---|---|
| `MODULE-READINESS.tsv` | One row per module: pack status, gates, scope, O/M/P, source, blockers, runtime, seams, next WPs, evidence |
| `SOURCE-LOCATIONS.tsv` | Where the latest accepted source lives (path + sha256), common-checkout presence, flags |
| `BLOCKING-DECISIONS.tsv` | Decisions in proposed order, with what each blocks and prepared text |
| `WAVES.md` | Waves 1–4 within the pilot limits; chat vs Mac |
| `PLAN-v9.3-DRAFT.md` | Successor text to plan v9.2 — DRAFT, not approved |
| `SHA256SUMS` | Checksums (relative paths) |

Step A record (separate path): `docs/records/audits/2026-09/mvp6-ct-owner-decisions-modules-first-2026-09-26.md`.
