# MVP6 UI scope drafts — S&OP (MOD-0190) and Capacity (MOD-0192) (Q78, WP MVP6-WP-190-192-UI-SCOPE-01)

🤖 Applying knowledge of @module-pack-author + @frontend-ui-ux + @business-analyst.

- **Lane:** AL-MVP6-UISCOPE-190-192-01 (INS/DEV, pack-scope proposal), a chat lane on the linked Mac folder.
- **Repo:** `feature/mvp6-logistics` @ `4a8d4d4b339528a88e6220fb8402e5a2c771136c`.
- **Start:** 2026-09-26T16:18:00+03:00.
- **Scope:** text only. No pack, contract or code edit; no git writes.

**Everything here is a DRAFT — NOT APPROVED.** CT presents `DECISION-TEXT.md` to the owner, one module at a time.

## Summary

| | MOD-0190 S&OP | MOD-0192 Capacity |
|---|---|---|
| Pack (now) | `ready-for-dev`, `shell: none`, sha256 `56fb8e7d…` | `ready-for-dev`, `shell: none`, sha256 `9b8b90f1…` |
| Published operations used | 6 (create, get, capture snapshot, list snapshots, record sign-off, list sign-offs) | 6 (create plan, get plan, create scenario, get scenario, evaluate, get evaluation) |
| Main finding | **F190-LIST: no list-plans operation** | **F192-LIST: no list operation at any level** |
| Other findings | F190-DEMAND (no lookup for demand references), F190-PIN (UI contract version), F190-ENT (backend entity not on EntityBase) | F192-DEMAND (free-text provenance/constraints), F192-POLL (manual refresh only) |
| Golden reference / fields | Slim, `form_field_count: 5` | Slim, `form_field_count: 7` |
| Recommended option | **A — bounded UI on the published operations** | **A — bounded UI on the published operations** |
| Estimate A (O/M/P h) | 52 / 86 / 148 | 58 / 98 / 168 |
| Estimate B (UI only) | 58 / 96 / 164 + contract/backend UNESTIMATED | 66 / 110 / 188 + contract/backend UNESTIMATED |

Every UI action maps to a published `operationId`, a controller action and an existing permission constant, each cited by
line. Nothing was invented: where the UI would need an operation that does not exist (lists, lookups), it is written as
a FINDING, not added.

## Files

| File | Content |
|---|---|
| `UI-SCOPE-0190.md`, `UI-SCOPE-0192.md` | Identity, operations ↔ controller ↔ permission (cited), screens, fields, errors, OUT rows, L10n keys, self-registration outline, open points |
| `PH15-UI-190.md`, `PH15-UI-192.md` | add-module Phase 1.5 rows 1–9 ("Plan: …"), NOT APPROVED |
| `ESTIMATE.tsv` | O/M/P by category and option, with confidence and the ledger rows each would replace |
| `DECISION-TEXT.md` | One owner decision per module, options A/B/C, recommended A, exact texts |
| `SHA256SUMS` | Paths relative to this folder |

Step A records (separate paths): `docs/records/audits/2026-09/mvp6-ct-verdicts-q74-q76-2026-09-26.md`; ledger edits in
`docs/roadmap/plans/mvp6-process-pilot-01/CT-QUEUE.tsv` and `MILESTONE-EVENTS.tsv`.

## ASSUMPTIONS

1. **UI revision inside the existing packs** (no FU or new ID), following the Returns/Claims precedent. The UI adds no
   object or operation.
2. **Transport:** same-origin MVC adapter (`proxy-profile`), as decided for Shipment (PC-28). Tenant/legal entity come
   from the server session only.
3. **Slim** because the create forms have 5 and 7 fields (add-module row 6, ≤ 8). The missing main list is recorded as a
   deviation (row 6), not solved by choosing Compact.
4. **Repeaters** (supply input refs, constraint refs, adjustments) are counted as array editors, not as extra top-level
   form fields. `form_field_count` is the create-plan form.
5. **Contract version:** S&OP operations are cited from the canonical 3.0.0 file; they are unchanged since 2.0.0, to
   which MOD-0190 is still pinned (F190-PIN).
6. **Estimates** are calibrated on the Returns scope estimate (list+create+transition M 100 h). They are proposed
   replacements of the 0190/0192 REMAINING rows, not additions; the ledger is not edited. The contract/backend part of
   option B is UNESTIMATED.
7. **L10n keys** are names only; values in 7 languages come with the UI draft. Nav keys follow the self-registration
   naming (`Nav.Module.<CODE>`, `Nav.Page.<PAGECODE>`).
8. **Automatic polling** of evaluations is not proposed (F192-POLL); a manual Refresh is.
9. **Source reading:** the accepted backend archives were read in the VM's `/tmp` only (read-only) and removed at the end.

Uncommitted — to be committed by a Mac Terminal session (chat lanes cannot commit).
