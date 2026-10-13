# SOP §22 — MVP6-BACKLOG-ENTRIES-APPLY-01 (CT queue Q41)

**Verdict: APPENDED — BL-372…BL-384 added to `docs/roadmap/backlog/product-backlog.md`; no existing line changed.** Single writer, append-only. Role applied: @product-owner (orchestrator rule 10).

- Repo / branch / HEAD: `/Users/natig/Projects/ERP-vNext-recovery`, `feature/mvp6-logistics`, `4a8d4d4b339528a88e6220fb8402e5a2c771136c` (matched at start and end).
- Start 2026-09-26T08:56:00+03:00; append at 08:59:33 +03:00; end in final report (Europe/Istanbul).
- Authority: `docs/records/decisions/2026-09/mvp6-backlog-entries-owner-decision-01.md`, SHA-256 `33fe73a6ee421ac8b90d46ca6ee86e5b4ba2e8b013dae131af2b90a68d6e3aea` (MVP6-BACKLOG-ENTRIES-OWNER-DECISION-01, approved).
- Format rule: `.antigravity/agents/orchestrator.md` rule 10 (what / why deferred / build trigger / module; status stays open until delivered; measurement command instead of numbers). Entry structure copied from BL-367…BL-371 (`### BL-NNN`, bold title, `DURUM · SAHİP · KARAR · KAYIT` line, body, **Ölçüm komutu** block). Status written as `ERTELENDİ`, never `TESLİM`.

## Preconditions and post-checks

| Check | Result |
|---|---|
| Decision hash prefix `33fe73a6ee42` | PASS |
| `git --no-optional-locks status --porcelain -- <backlog>` empty before | PASS |
| Backlog SHA-256 before | `91c6d7dccbbfa0fda21d8b0f5ad717155005eb5873345345761a280ec67f8028` (prefix `91c6d7dccbbfa0fd` PASS) |
| Last entry before | `### BL-371` PASS |
| Backlog SHA-256 after | `3b39aa0fa823a83b49c02e1b75c3a513c785fc633e73481fb8a0f789b9ff6304` |
| `git --no-optional-locks diff --stat -- <backlog>` | `1 file changed, 187 insertions(+)` |
| Removed lines in `git diff -U0` | none |
| Original file is a byte-identical prefix of the new file (`cmp -n <old size>`) | PASS |
| New IDs each occur exactly once; file ends with BL-384 | PASS |

## Entries added and their sources

| Entry | Module | Item | Source(s) cited in the entry |
|---|---|---|---|
| BL-372 | MOD-0186 | Returns detail page (no by-ID read) | `mvp6-ui-pack-drafts-01/returns/SCOPE.md` §2, §10; UI scope decision; AG-02 |
| BL-373 | MOD-0187 | Claims detail page (no by-ID read) | `mvp6-ui-pack-drafts-01/claims/SCOPE.md` §2, §10; UI scope decision; AG-02 |
| BL-374 | MOD-0187 | `approvedAmount` not in `ClaimSummary` list | claims `SCOPE.md` §5; `mvp6-ui-pack-revisions-01/claims/SOP-22-PACK-REVISION.md` gap 1; UI scope decision |
| BL-375 | MOD-0185 | Loads independent detail | `mvp6-effort-shipment-ct-update-07/UNESTIMATED-SCOPE.tsv` 0185-RS-06 |
| BL-376 | MOD-0185 | Loads searchable lookup | same file, 0185-RS-07 |
| BL-377 | MOD-0185 | Loads producer root read (mandatory, unestimated) | same file, 0185-RS-05; `mvp6-ct-disposition-q08-q17-2026-09-26.md` Q08 |
| BL-378 | MOD-0183 | Shipment UI: edit/delete/bulk/QuickView/import-export/SaveView/column visibility/ShowAll/generic statuses (server paging is IN) | `mvp6-shipment-acceptance-reconcile-01/SCOPE-CHANGE-RECORD.tsv`, `ACCEPTANCE-MATRIX.tsv` (A14, A14-GENERIC-GATE); DN-02 |
| BL-379 | MOD-0186 | Returns UI bulk/edit/delete/import-export/server paging/multi-status | returns `ACCEPTANCE.md` RU-SCR-01…06; UI scope decision |
| BL-380 | MOD-0187 | Claims UI same set | claims `ACCEPTANCE.md` CU-SCR-01…06; revision gap 5; `mvp6-claims-pack-apply-01` |
| BL-381 | MOD-0183/0184 | Durable PNG evidence | `mvp6-carrier-real-auth-ct-review-01/PNG-DISPOSITION.md`; ACCEPTANCE-MATRIX PRES-183-04; CT-QUEUE Q13 |
| BL-382 | MOD-0183 | UI183-A10 fault-proxy verification | `mvp6-shipment-remaining-acceptance-disposition-01/DECISION-NEEDS.md` DN-01, its `SOP-22.md`; CT-QUEUE Q10 |
| BL-383 | MOD-0186 | Further deferral found: remaining-entitlement display, reason/disposition catalogues, link from Shipment detail | returns `SCOPE.md` §2, §6 step 4, §10 |
| BL-384 | MOD-0187 | Further deferral found: evidence upload/verification, carrier picker, approved-amount history, links from Shipment/Carrier UIs | claims `SCOPE.md` §1, §6 step 3, §10 |

Grouping: bulk/edit/delete/import-export/paging is one entry per module (BL-378/379/380), because each module has its own OUT record and trigger.

## Considered and not added (not deferred features)

- Claims UI-revision gaps G-MODAL, G-ICONMAP, G-DATETIME and "alignment not yet approved" (`SOP-22-PACK-REVISION.md` gaps 2–4, 6): open items for the UI writer and integration owner inside the approved scope, not deferred features. The owner's sign-off decision keeps them as open items.
- DN-02 / PC-02/03/04/28 (scope-aware DataTable profile): an open owner decision (CT-QUEUE Q11/Q12), not a deferred feature; referenced in BL-378/379/380.
- Finance/AP/AR/payment and currency FX (claims `SCOPE.md` §10): outside the Claims module's domain, not a deferred Claims feature; noted in BL-384 as excluded.
- Integration-owner work (gateway, permissions, navigation, root uptake): tracked in CT-QUEUE Q14/Q15, not backlog items.

## Changed files

Only `docs/roadmap/backlog/product-backlog.md` (append) and this evidence folder. No other file; no commit, push, stash or `git add`.
