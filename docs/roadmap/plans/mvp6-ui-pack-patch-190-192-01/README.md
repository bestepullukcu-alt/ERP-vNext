# MVP6 — MOD-0190 / MOD-0192 UI pack revision patches (Q79 Step C) — DRAFT, NOT APPLIED

2026-09-26 (+03:00) · Q79 chat lane (single ledger writer + single pack-patch author, Linux VM bridge). Measured on
`feature/mvp6-logistics` @ `4a8d4d4b339528a88e6220fb8402e5a2c771136c`. **Nothing here is applied or approved.** The packs are
unchanged; the owner decides in Q80 (`SIGN-OFF.md`); Q81 applies at exact hashes and an independent VER follows.

## Contents

| File | Purpose |
|---|---|
| `patches/01-MOD-0190-ui-revision.patch` | Unified diff for MOD-0190: frontmatter `shell: tenant`, `golden_reference: slim`, `form_field_count: 5`; appends §23 Tenant UI scope and §24 Self-registration (+285 / −3 lines) |
| `patches/02-MOD-0192-ui-revision.patch` | Unified diff for MOD-0192: frontmatter `shell: tenant`, `golden_reference: slim`, `form_field_count: 7`; appends §23 and §24 (+286 / −3 lines) |
| `SIGN-OFF.md` | Q80 decision text, one decision per module, recommended = approve (patch + Phase 1.5 table) |
| `SHA256SUMS` | Folder-relative checksums of the three files above plus this README |

## Hashes and apply check

| Pack | Preimage (required prefix) | Patch | Expected postimage |
|---|---|---|---|
| MOD-0190 | `56fb8e7d19daa36370747f391ac92c05eca1b51c84d643fbdf7ace687b82ac41` (`56fb8e7d` ✔) | `eea740986b0058b06a43bb198b9a446f9a1cc7dde41f445661b7bd8f49db8225` | `2bdd533f7e558fc7bd18670d23db1bdf51aedd61d5dfa28e8dbd73b20757017f` (552 lines) |
| MOD-0192 | `9b8b90f1def56be975d63c456a6fb61ba574fec8e2060457d53c72c839fdf813` (`9b8b90f1` ✔) | `a9199cf1a9d7f6d2960171fd1c300fa7b523fcc134d40ae1861356bd83306bfb` | `7c3678bc5cbd646c8c2b6f9b9e99089cba9e03b34aed4e74359d52078975171f` (562 lines) |

Method: each preimage was copied byte-for-byte into a throwaway directory under `/tmp` (with `git init` there only), never the
repository. Output:

```
$ git apply --check -v ../patches/01-MOD-0190-ui-revision.patch
Checking patch execution/domains/supply-chain-execution/module-packs/MOD-0190-sop-workflow-signoffs.md...
exit=0
$ git apply ../patches/01-MOD-0190-ui-revision.patch && sha256sum …MOD-0190-sop-workflow-signoffs.md
2bdd533f7e558fc7bd18670d23db1bdf51aedd61d5dfa28e8dbd73b20757017f   (identical to the built postimage; git apply -R --check OK)
$ git apply --check -v ../patches/02-MOD-0192-ui-revision.patch
Checking patch execution/domains/supply-chain-execution/module-packs/MOD-0192-capacity-planning.md...
exit=0
$ git apply ../patches/02-MOD-0192-ui-revision.patch && sha256sum …MOD-0192-capacity-planning.md
7c3678bc5cbd646c8c2b6f9b9e99089cba9e03b34aed4e74359d52078975171f   (identical to the built postimage; git apply -R --check OK)
```

Identity gate observed 2026-09-26 ~17:38 +03:00 (read-only): `verify_module_id.py … --check-id MOD-0190 --name "S&OP Workflow & Sign-offs"`
→ `OK`, exit 0; `… --check-id MOD-0192 --name "Capacity Planning"` → `OK`, exit 0.

## Sources (read only)

`docs/roadmap/plans/mvp6-ui-scope-190-192-01/` (SHA256SUMS `83c6b7e9…e985`, 7/7): `UI-SCOPE-0190.md` `2447669e…cb82`,
`UI-SCOPE-0192.md` `656ef770…c9b3`, `PH15-UI-190.md` `fb6ad9d8…45cf8`, `PH15-UI-192.md` `5dee65d1…f4116`, `ESTIMATE.tsv` `03669f33…09e3`;
scope record `mvp6-sop-capacity-ui-scope-owner-decision-01.md` `1814672b…1553`; model structure MOD-0187 §32/§33 (pack
`8ed42fad…94a2`); contract `sandop-capacity.openapi.yaml` 3.0.0 `5213b535…adab`; accepted archives `8fa00d40…b745` (S&OP) and
`ebd5d80c…7064` (Capacity), from which `SandopPermissions` (Read, Create, Capture, SignOff) and `CapacityPermissions` (Read, Create,
ScenarioCreate, Evaluate) were read; `mvp6-self-registration-prep-01/MANIFESTS.md` (SortOrder sequence 390…430).

## ASSUMPTIONs

1. **Numbering:** the new sections are §23/§24 (next free numbers after §22), modelled on the structure of MOD-0187 §32/§33, not its numbers.
2. **Final headings + `Approved:` lines:** no "PATCH PROPOSAL / NOT APPROVED" labels in pack text (lesson of Q73/Q75, which needed a cleanup patch). Each new section carries `Approved: docs/records/decisions/2026-09/mvp6-sop-capacity-ui-pack-signoff-owner-decision-01.md`; that record does not exist yet and must be created by the Q80 recorder at exactly this path. The patch is applied only after it exists (Q81 HELD on Q80).
3. **Owned UI paths (32 per module):** derived from the MOD-0187 §32.10 convention (controller, view model, views + marker classes, JS + l10n JS, 7-language RESX per marker, three test files), extended to two pages (entry + workspace) and three offcanvas partials per module. UI-SCOPE named only the folders.
4. **Adapter routes:** a separate GET adapter per read operation (plan, scenario, evaluation) and per list query was added, derived from the operation table; UI-SCOPE named only the page and POST routes.
5. **Action placement** on the workspace page is `Toolbar` (only `Toolbar`/`RowAction` are used in MOD-0183…0187 manifests).
6. **Self-registration identity:** ModuleName PascalCase and provider names follow the 0187 pattern (`SopWorkflowSignoffs`, `CapacityPlanning`); SortOrder/Icon 440 `bx-check-double` and 450 `bx-bar-chart-alt-2` are SOFT proposals continuing 390…430; detail pages get Sort 11 with the entry page as parent (M-07).
7. **Acceptance rows** SU-VS1…SU-20 / CP-VS1…CP-20 and readiness labels were derived from UI-SCOPE §§3–6, pack §13/§16 and the MOD-0187 CU-matrix analogues (DN-01, PRES-183-04, gateway, target). No row has run.
8. **Contract binding:** MOD-0190 binds the six operations by `operationId` (identical in 2.0.0 and 3.0.0) and leaves F190-PIN open; MOD-0192 binds 3.0.0 per its own §22 contract-precedence clause.
9. **Frontmatter:** only the three UI lines change; `status` and `status_note` stay as they are (MOD-0187 kept its `status_note` when §32 was applied; "UI … remain open" stays true until UI is built).
10. **Authorization wording:** §23.14 follows CT verdict Q64a F2 (owner decisions "modules first" and draft overlays take precedence) instead of copying the MOD-0187 §32.14 sentence that F2 flags for correction.
11. **Effort:** option A figures from `ESTIMATE.tsv` only; the non-UI remainder of `0190/0192-5/6-REMAINING` is not recomputed here (effort-ledger lane). `ESTIMATE.tsv` gives no 0192-5/6 totals, so none are stated.
12. **Phase 1.5 tables** are bound by hash as they stand; their "NOT APPROVED" headings are superseded only by the future Q80 record (K4: sources not edited).

## Findings (in scope, not fixed)

- **F-Q79-01 (medium):** the owner decisions "modules first" and "draft overlays" (~16:17, 2026-09-26) have no standalone record; they are referenced only in the Q79 CT-verdicts record and the Claims PH15 record. §23.14 of both patches cites them through CT verdict Q64a F2. A small record would close this.
- **F-Q79-02 (low):** §22 of both packs still says "It is a proposal until the owner decision … is recorded" next to an `Approved:` line — label residue of the Q73/Q75 kind. Not changed (outside this patch; would need its own signed patch).
- **F-Q79-03 (medium):** DCP-009 §21.1 and `mvp6-self-registration-prep-01/MANIFESTS.md` line 141 exclude MOD-0190/0192 ("no UI scope"). After Q80/Q81 a DCP-009 patch by its owner is needed before the providers can be registered; listed in §23.10/§24 open gaps.
- **F-Q79-04 (low):** MOD-0190 §16/§18 still name SANDOP-CAPACITY 2.0.0 as the acceptance contract while the canonical file is 3.0.0 (F190-PIN, carried). MOD-0192 has a precedence clause; MOD-0190 does not.
- **F-Q79-05 (low, Capacity UX):** with no list operations, scenario and evaluation IDs created in a session are lost on reload unless copied (recorded in MOD-0192 §23.13).
