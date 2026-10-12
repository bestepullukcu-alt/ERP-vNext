# MVP6 — `.antigravity` compliance check (CT, 2026-09-26 02:00 Istanbul)

Requested by the owner: "check antigravity rules and continue them". Static, read-only check of MVP6 work and of tonight's
CT work against `AGENTS.md` and `.antigravity/` (orchestrator, `/add-module`, GEMINI.md, git-safety, docs-organization,
module-self-registration-standard). No product, pack, rule, backlog or Git change was made by this check; this file is the only write.

Read in full: `.antigravity/agents/orchestrator.md`, `.antigravity/workflows/add-module.md`, `.antigravity/rules/GEMINI.md`,
`.antigravity/rules/git-safety.md`; relevant parts of `module-self-registration-standard.md` and `docs-organization.md`.

## Findings

| ID | Severity | Rule | Finding | Action |
|---|---|---|---|---|
| AG-01 | **Blocker (module close)** | `module-self-registration-standard.md` §§1–4, 8; add-module "SELF-REGISTRATION ZORUNLU" | `services/Diten.SupplyChainService` has **no `ModuleManifestProvider`** (only DevEnablement has one). No MVP6 pack, UI draft or UI revision covers the manifest (pages/actions mirroring the real UI, RoutePath scope, completeness tests both directions, reconcile-state test) or the tenant nav keys `Nav.Domain/Module/Page.*` in 7 languages. Shipment and Carrier UI already exist without it. No MVP6 module can close until this exists. | Prepare it once for the Supply Chain service as a cross-cutting item (orchestrator rule 9: multi-module → capability-level preparation), then per-module manifest sections in each pack. Queue Q40. |
| AG-02 | High | orchestrator rule 10 (product backlog gate) | `docs/roadmap/backlog/product-backlog.md` has **no MVP6 entries**. Items deliberately deferred tonight and earlier are not recorded: Returns/Claims detail page (no single-record read), Claims approved amount in list, Loads detail and searchable lookup, bulk/edit/delete/import-export/server paging for Shipment/Returns/Claims UI, durable PNG evidence, A10 fault-proxy verification. | Add backlog items (what / why deferred / build trigger / module). Changes a shared tracked file — owner decision Q41. |
| AG-03 | Medium (CT process) | docs-organization K4 (records are not corrected after writing; ADRs are superseded, not edited) | CT appended addenda to its own disposition records (`mvp6-ct-disposition-lane2-lane3-2026-09-25.md`, `mvp6-ct-disposition-q08-q17-2026-09-26.md`) and corrected a timestamp inside decision record `mvp6-returns-claims-ui-scope-owner-decision-01.md` (r1). | Corrective rule from now: every CT disposition and every correction is a **new** dated record; existing records are not edited. The r1 timestamp correction is disclosed here as a deviation; it is not reverted (a revert would be a second edit). |
| AG-04 | Gate reminder | add-module Phase 1.5 | The architecture table (9 rows) with explicit owner approval via the question tool is required **before** Phase 2 of any code lane. Pack revisions do not replace it. | Mandatory first step of every UI/backend code dispatch. |
| AG-05 | Gate reminder | add-module Phase 4.5 | Runtime smoke: Channel A (MCP browser) is available through the built-in browser; B (Playwright) through local Claude Code; C (owner check) as fallback. "Done" may not be claimed without one. | Named in every UI dispatch. |
| AG-06 | Gate reminder | add-module Phase 6; orchestrator report | API narrative (`docs/reference/architecture/api/`), illustrated user manual (`docs/guides/<module>/index.html`), per-module audit report (`docs/records/audits/2026-09/`), and backlog closure records (⚠️ partial until live verification) are required to close a module. None exist yet for MVP6 modules. | Added to plan as module-closure work. |
| AG-07 | Gate reminder | orchestrator rule 3; `quality-gate-datatable.md` | `verify_datatable_page.py --reference slim` PASS is required before UI delivery; it conflicts with the bounded OUT rows (edit/delete/bulk/QuickView). | Resolved only through DN-02 and the scope-change records; no silent waiver. |
| AG-08 | Low | GEMINI.md response format | Lanes do not announce the agent role they apply. | Lane prompts now start with the role line, e.g. "🤖 Applying knowledge of @module-pack-author". |
| AG-09 | Low (pre-existing) | AGENTS.md §9 branch naming | Work is on `feature/mvp6-logistics`, not `feature/{domain}/{module-id}-{slug}`. | Decide with the commit strategy (Q03). |
| AG-10 | Low (pre-existing) | docs-organization K2/K3 | `docs/` has 7 top-level folders; `docs/roadmap/plans/` holds more than 20 files. | Record only; any move follows the §4 protocol and needs owner approval. |

## Compliant (checked)

git-safety: no commit, push, stash, `git add -A/.`, reset, clean or force operation; backups follow GIT-001 §A. Module pack gate:
no code written from a draft pack. DCP-002: identity checks run for Returns and Claims. Protected paths (`.antigravity`, gateway,
frozen layouts) untouched. Docs placement: audits and decisions under `docs/records/`, plans under `docs/roadmap/`, process guide under `docs/guides/operations/`.
