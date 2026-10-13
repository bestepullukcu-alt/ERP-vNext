# Q201 — Integration dry-run (read-only) · SOP §22 structured report

| Field | Value |
|---|---|
| Work Package / Lane / Evidence | Q201 · `AL-SCM-INTEGRATION-DRYRUN` (INT) · required E1 — **reached: E1** (static, hash-measured; nothing built, nothing run) |
| CT-QUEUE row | `docs/roadmap/plans/mvp6-process-pilot-01/CT-QUEUE.tsv:299` |
| Authority | OD-INTEGRATE-CURRENT — `docs/records/audits/2026-10/mvp6-ct-verdicts-q198-2026-10-02.md:47` |
| Placement | Claude app → Code tab → Local, `uname -s` = Darwin. **G2 placement waiver applied — Cowork withdrawn by owner.** |
| Agents | Phase A `read-only-auditor` (mode **worktree-read-only**, no fixes) → Phase B `documentation-writer` (this folder only) |
| Base stack | BASE-STACK v2 — `docs/records/audits/2026-09/mvp6-base-stack-v2/BASE-STACK-v2.md` sha256 `ce8d60ab959ec7018c255ab1152983cfd8e301390f54b3d0ba23961bf2c4d882` (BASE a8a236de → Q117 83e6322c → Q121 93bf1c07 → Q131 4a4a0860). Not edited (K4). |
| Start / End (Europe/Istanbul) | 2026-10-02 17:30:42 +03 / see `ARTIFACTS.sha256` header (last write) |

```text
Agent Verdict:        DRY-RUN COMPLETE — list only. Agent PASS ≠ CT ACCEPTED (K13).
Branch / HEAD:        feature/mvp6-logistics @ 4a8d4d4b339528a88e6220fb8402e5a2c771136c (matched)
Worktree status:      git status --porcelain: 32 " M" · 533 "??" · 0 staged = 565 (matches the G7 baseline;
                      508 "??" under docs/, 20 under services/). No .git/index.lock. No git diff was run.
Changed files:        none outside docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/
Golden/Contract flow: n/a (no code)
Sub-flows:            n/a
Failure paths:        n/a
Tests:                NOT RUN (forbidden by the WP)
Persistence evidence: n/a
Security/RBAC/Tenant: n/a
Audit/Evidence:       CHECKS.tsv (15 checks, all PASS) · the TSVs below · tools/q201_dryrun.py (re-runnable, K7)
Observability:        n/a
Migration/Rollback:   n/a — nothing was applied
Decisions:            none taken; open decisions are listed in §6
Blockers:             none for the dry-run itself
Known gaps:           §7
Out-of-scope changes: none
```

## 1. Method

`tools/q201_dryrun.py` (in this folder; the exact program that produced every TSV here).

- Every archive was read with Python `tarfile` **in memory**. Nothing was extracted into the repo or `~/mvp6-env`.
- The stack was rebuilt from the manifests only: `BASE-MANIFEST.tsv` (14,566 rows) → Q117 `OVERLAY-MANIFEST.tsv` →
  Q121 `README.md` table → Q131 `OVERLAY-MANIFEST.tsv`. For each overlay: archive members = manifest rows,
  member sha256 = postimage, preimage = the stack below it. Result 14,572 paths. All PASS (`CHECKS.tsv`).
- Each stack path was compared with the sha256 of the working-tree file.
- Git: `ls-tree`, `status --porcelain`, `cat-file blob` only (HEAD blobs of the 32 ` M` paths). `GIT_OPTIONAL_LOCKS=0`.
- Classes (as defined in the Q201 prompt):
  - **ADD** — in the stack or a module draft, absent from the working tree.
  - **OVERWRITE** — in both, differs, and the working-tree copy equals a recorded preimage
    (the HEAD blob, or the postimage of a lower layer).
  - **CONFLICT** — in both, differs, and the working-tree copy equals neither preimage nor postimage.
  - IDENTICAL — working tree already equals the postimage (not an action; listed in `IDENTICAL-ROWS.tsv`).
- Shared-seam paths (Program.cs, DI registration, permission catalog/seed, `ocelot.json`, canonical SANDOP/DEMAND)
  are **not** in `ADD-OVERWRITE-CONFLICT.tsv`. They are in `SHARED-SEAM-ROWS.tsv` and `SHARED-SEAM-PATCH-NEEDS.md`.

## 2. Primary question — has the Q131 layer been partially applied by hand?

**No. Q131 was never applied. What is in the working tree is the UC-01 candidate — the hand edit Q131 was derived
from.** Seven of its files happen to be byte-equal to the Q131 postimage because Q131a kept those hunks unchanged;
five are not. Full table: `Q131-APPLICATION-STATE.tsv` (20 rows).

| State | Rows | Paths |
|---|---:|---|
| **(a)** working tree == Q131 postimage | 7 | `Diten.Platform.Application.Tests/` — 4 × `BusinessReferenceData*MongoTests.cs`, `Persistence/MongoIntegrationHarness.cs`, `Schema/PlatformSchemaContractMongoTests.cs`, `Workflow/WorkflowTransitionGateMongoRepositoryTests.cs` |
| **(b)** working tree == Q131 preimage | 0 | — |
| **(c)** working tree == neither → **CONFLICT** | 5 | see below |
| absent from the working tree | 8 | 5 CapacityPlans test files (their preimage comes from BASE L2-A12-360 / Q117, which the tree does not have) + 3 NEW files |

The five case-(c) files, and the third edit in each:

| Path | Git | Working tree | Q131 postimage | Third edit |
|---|---|---|---|---|
| `…/Diten.Platform.BackgroundJobs.Tests/PlatformContainerValidationTests.cs` | ` M` | `cc27c477…` | `57a0c812…` | UC-01 candidate |
| `…/Diten.Platform.Eventing.Tests/RabbitMqEventingIntegrationTests.cs` | ` M` | `8407a309…` | `1f16c875…` | UC-01 candidate |
| `…/Diten.Platform.Eventing.Tests/TenantLifecycleRabbitMqIntegrationTests.cs` | ` M` | `755d3904…` | `104ba909…` | UC-01 candidate |
| `…/Diten.Platform.Application.Tests/Persistence/PlatformMongoTestConnection.cs` | `??` | `826d24fc…` | `6d28ba03…` | UC-01 candidate |
| `…/Diten.Platform.Application.Tests/Persistence/PlatformMongoTestConnectionTests.cs` | `??` | `a707303e…` | `3d87f8a0…` | UC-01 candidate |

- **Identification is by hash, not by inference.** Each of the five working-tree files has the same sha256 as its
  copy in `docs/records/audits/2026-09/mvp6-q152-uc01-lock-investigation-01/working-copy/`.
- Origin of the edit: a Codex Desktop thread, 27 Sep 17:43–17:51, direct writes to the working tree, 10 modified +
  2 untracked, unbuilt — `docs/records/audits/2026-09/mvp6-ct-verdicts-q150-q143-q152-uc01-2026-09-27.md:42`.
- What differs from Q131 in each file: `docs/records/audits/2026-09/mvp6-q131a-port-uri-overlay-01/CANDIDATE-REVIEW.tsv`
  rows marked `FIX` (lines 15, 18, 19, 22, 23). The candidate's guard refused only protected ports and still read the
  old variables; Q131a rewrote the guard and the two new files.
- **Authoritative side: the Q131 archive.** Decided by record, not by this lane:
  - `BASE-STACK-v2.md:139-144` — the UC-01 files are in no layer; "Never copy them from the working tree into a
    stacked tree"; "Equality does not make them a source".
  - OD-UC01 — `mvp6-ct-verdicts-q150-q143-q152-uc01-2026-09-27.md:47`.
- For the 10 tracked UC-01 paths, the Q131 preimage equals the HEAD blob (`Q131-APPLICATION-STATE.tsv`,
  columns `preimage_sha256` / `head_blob_sha256`). The other 2 are untracked and have no preimage.

## 3. The five NEW files Q131 adds (`NEW-FILES-STATUS.tsv`)

| Path | On disk | Note |
|---|---|---|
| `scripts/test-env/mvp6-test-mongo-env.sh` (`6c936b4a84bb…`, mode 0755) | **ABSENT** | **Q205 depends on this file. It does not exist in the working tree.** The folder `scripts/test-env/` does not exist either. |
| `…/Application.Tests/Persistence/PlatformMongoTestConnection.cs` | present, **differs** | UC-01 candidate copy (§2), untracked |
| `…/Application.Tests/Persistence/PlatformMongoTestConnectionTests.cs` | present, **differs** | UC-01 candidate copy (§2), untracked |
| `…/SupplyChainService.Tests/CapacityPlans/CapacityTestMongo.cs` | absent | |
| `…/SupplyChainService.Tests/CapacityPlans/CapacityTestMongoTests.cs` | absent | |

## 4. The three lists (`ADD-OVERWRITE-CONFLICT.tsv`, 445 rows, shared seams excluded)

| Source | ADD | OVERWRITE | CONFLICT | Acceptance state (ledger line in `CT-QUEUE.tsv`) |
|---|---:|---:|---:|---|
| BASE-STACK v2 (BASE + Q117 + Q121 + Q131) | 231 | 62 | 21 | CT ACCEPTED ×4 — `mvp6-base-stack-v2/LAYERS.tsv:2-5` |
| S&OP UI draft v3 `716e7c4c…` | 34 | 0 | 0 | `:273` Q183 DONE (CT ACCEPTED); `:282` Q185 Mac re-test DONE (CT ACCEPTED) |
| Capacity UI draft v3 `5c0a3b61…` | 34 | 0 | 0 | `:273` CT ACCEPTED; **v4 ordered and not yet on disk** (`:286` Q192 READY) |
| Returns backend guard `058a7b95…` | 5 | 0 | 0 | `:283` Q187 DONE (CT ACCEPTED; writer); its VER Q189 is **CT NOT ACCEPTED** (`:291`) |
| Returns UI draft v3 `50724097…` | 23 | 0 | 0 | `:284` Q188 DONE (CT ACCEPTED; writer); VER Q189 **CT NOT ACCEPTED: F-Q189-1** (`:291`); **v3a ordered, not on disk** (`:293`) |
| Claims UI draft v4 `2b34741a…` | 23 | 0 | 0 | `:149` Q64f DONE (writer; Claims v4 INTEGRATION_READY); `:146` Q129 Mac build DONE (CT ACCEPTED) |
| Loads uptake draft 01 `e0583eef…` | 5 | 7 | 0 | `:80` Q77a DONE (CT ACCEPTED as DRAFT); **Mac build Q77b never run** (`:83` READY) |
| **Total** | **355** | **69** | **21** | |

Every row cites a manifest line or an archive member in its `evidence` column.

- **ADD, v2 stack (231):** 185 under `services/Diten.SupplyChainService`, 25 `frontend/Diten.Web`, 6 `frontend/Diten.Web.Tests`,
  6 `services/Diten.Platform`, 4 `services/Diten.MdmService`, 2 `services/Diten.AuthService`, 2 `gateway/Diten.ApiGateway.Tests`,
  1 `scripts/test-env`.
- **OVERWRITE, v2 stack (62):** in every row the working-tree file is either the clean HEAD blob or the BASE
  postimage below Q117/Q121. 10 of these are files shared across modules but outside the Q201 seam list
  (7 `SharedResource.{lang}.resx`, 3 `.csproj`); they stay in the bulk list and carry a value in `shared_seam`.
- **Module drafts:** all UI-draft files are ADD. No path appears in two drafts (`MODULE-OVERLAPS.tsv` is empty).
  The drafts' 54 `_shared-integration/` members and 2 lane-only Claims members are not repo files; they are listed
  in `SHARED-INTEGRATION-MEMBERS.tsv` and described in `SHARED-SEAM-PATCH-NEEDS.md`.
- **Already identical:** 14,192 non-seam stack paths equal the working tree. 3,441 of them are *not* plain HEAD
  files (BC-SOURCE / A12-360 / Auth-22 content already on disk) — `IDENTICAL-ROWS.tsv`.

### The 21 CONFLICT rows

| Kind | Rows | Authoritative side | Deciding record |
|---|---:|---|---|
| THIRD-EDIT on a Q131 path (UC-01 candidate) | 5 | Q131 archive postimage | `BASE-STACK-v2.md:139-144`; OD-UC01 `…q150-q143-q152-uc01-2026-09-27.md:47` |
| LOCAL-EDIT, stack unchanged (stack postimage = HEAD blob) | 15 | **insufficient evidence** | no record found |
| THREE-WAY (layer changes HEAD; working tree has a different edit) | 1 | **insufficient evidence** | no record found |

- **LOCAL-EDIT (15).** For these paths the stack carries exactly the HEAD blob — it changes nothing. The working
  tree carries an uncommitted edit. Copying the stack file over it would discard that edit. The paths:
  - 6 module packs `MOD-0184 / 0185 / 0186 / 0187 / 0190 / 0192` and `DCP-009-supply-chain-inventory.md`
  - `docs/guides/operations/control-tower-sop.md` (the SOP v2.5 text this WP runs under)
  - `docs/analysis/contracts/shipment-bundle.openapi.yaml`, `docs/roadmap/backlog/product-backlog.md`
  - `.antigravity/agents/frontend-ui-ux.md`, `.antigravity/rules/docs-organization.md`, `.antigravity/workflows/test.md`
  - `.claude/settings.local.json`, `tests/architecture/…/DocsPathGuardTests.cs`
- **THREE-WAY (1).** `execution/domains/supply-chain-execution/module-packs/MOD-0183-shipment-tracking-pod.md`:
  BASE L2-A12-360 changes it, and the working tree holds a different edit (unchanged since Q103, 26 Sep).
- The nearest record, `BASE-STACK-v2.md:145`, says the stack is defined by the manifests and not by the checkout.
  It does not say what happens to a local edit when the stack is written into this checkout.

## 5. Findings

| ID | Severity | Finding | Evidence |
|---|---|---|---|
| **F-Q201-0** | 🟠 High | The CT-QUEUE Q201 row names owner `LANE 3 (integration-agent)`. Agent/scope mismatch: that agent's §17.4 mandatory fields are route family, downstream port and ocelot file; none exist in this WP and gateway work is forbidden here. Noted only; the old owner was not acted on. | `CT-QUEUE.tsv:299`; `control-tower-sop.md:950` (§17.4 row) |
| **F-Q201-1** | 🔴 Blocker for Q202 | Q131 is not applied. The working tree holds the UC-01 candidate: 7 files (a), 0 (b), **5 (c)**, 8 absent. A writer that treats "modified and equal in 7 files" as "Q131 is in" would leave 5 pre-Q131 files and miss 8. | §2; `Q131-APPLICATION-STATE.tsv` |
| **F-Q201-2** | 🟠 High | `scripts/test-env/mvp6-test-mongo-env.sh` is absent from the working tree. Q205 depends on it. | §3; `NEW-FILES-STATUS.tsv:2` |
| **F-Q201-3** | 🔴 Blocker for Q202 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/Program.cs` is a three-way conflict on a shared seam: HEAD `74d2d200…`, working tree `7fdb5ef0…`, BASE `33027bcd…`. Reported separately. | `SHARED-SEAM-PATCH-NEEDS.md` §1 |
| **F-Q201-4** | 🔴 Blocker for Q202 | 16 CONFLICT rows have no deciding record (15 LOCAL-EDIT + 1 THREE-WAY). A plain copy of the stack would silently revert 7 module/capability packs and the SOP. | §4; `ADD-OVERWRITE-CONFLICT.tsv` rows with `authoritative_side = insufficient evidence` |
| **F-Q201-5** | 🟠 High | Two owner decisions point at different integration targets. OD-UC01 and OD-Q15: restore the working tree to HEAD, integrate "in a new registered checkout". OD-INTEGRATE-CURRENT: integrate into the current working tree. The newer record says it supersedes the "finish modules first, integrate last" plan; it does not name OD-Q15 or OD-UC01. Whether "restore to HEAD first" still holds decides F-Q201-4. | `…uc01-2026-09-27.md:47`; `docs/records/decisions/2026-09/mvp6-owner-decisions-q19-q15-q03b-q03c-q21-2026-09-27.md:53`; `…q198-2026-10-02.md:47` |
| **F-Q201-6** | 🟠 High | "Accepted module drafts" is not one clean set. Only S&OP v3 and Claims v4 have a closed chain. Capacity v3 and Returns UI v3 each have a successor ordered that is not on disk; the Returns VER is CT NOT ACCEPTED; Loads has no Mac build. All six are listed; which ones Q202 writes is a CT decision. | §4 table |
| **F-Q201-7** | 🟡 Medium | Loads draft, `services/Diten.SupplyChainService/tests/loads/runtime_probe.py`: the draft's recorded preimage is `e5ee0aed…` (BASE), but Q117 rewrites this file to `71fd51b7…`. The draft ships it as a patch, not a file. The patch was not written against the v2 stack. | `mvp6-loads-uptake-draft-01/SOURCE-MANIFEST.tsv:12`; `mvp6-q117-test-guard-fixes-01/OVERLAY-MANIFEST.tsv:10` |
| **F-Q201-8** | 🟡 Medium | Gateway route-count guard: the Claims hand-off patch moves `supplyChainRoutes` 6 → 8 and counts Claims only. The Returns fragment adds 2 more `/api/shipment-bundle/` routes and carries no count patch. Whether the guard counts them: insufficient evidence (the test file was not analysed). | `SHARED-SEAM-PATCH-NEEDS.md` §4 |
| **F-Q201-9** | 🟡 Medium | The CT integration finding says "≈3,700 files not in the source tree". Measured: 231 absent + 70 differing-with-known-preimage + 23 conflicting = **324** v2-stack paths; 14,248 are already byte-identical. | `…q191-q189-2026-10-02.md:39`; `CHECKS.tsv`; this file §4 |
| **F-Q201-10** | ⚪ Low | 36 untracked non-docs files are in no layer and no draft (26 `scripts/evidence-kit/`, 7 `TestResults/`, `.antigravity/workflows/dispatch-wp.md`, `.claude/settings.local.json.bak-20260926`, `rework-results.json`). The stack does not touch them. | `TREE-ONLY-UNTRACKED.tsv` |
| **F-Q201-11** | ⚪ Low | `read-only-auditor.md:50-61` prescribes `git diff --name-only` / `git diff --check` for the no-change block; the Q201 prompt forbids `git diff`. The prompt was followed; the no-change proof is porcelain-based (§8). | `.antigravity/agents/read-only-auditor.md:50-61` |

| **F-Q201-12** | 🟡 Medium | Another process built the SupplyChain test project in the repo working tree during this run: `services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/{bin,obj}/Debug/net8.0/MvcTestingAppManifest.json`, mtime 2026-10-02 17:38:55 +03. This lane ran no build. Both files are git-ignored, so the porcelain is unchanged; no `dotnet` process was running when checked afterwards. Which session did it: insufficient evidence. | `find -newermt "2026-10-02 17:30:42"` outside this folder returned exactly these two files |

## 6. Decisions CT / owner must take before Q202 (none taken here)

1. F-Q201-5 → does "restore the working tree to HEAD" (OD-UC01) still apply under OD-INTEGRATE-CURRENT?
2. F-Q201-4 → fate of the 16 local edits with no deciding record.
3. F-Q201-6 → which module drafts are in scope for the write, and at which version.
4. F-Q201-3 and the other seams → a single integration-agent lane (SR-D4); see `SHARED-SEAM-PATCH-NEEDS.md`.

## 7. Known gaps

- Comparison is byte-level (sha256). No semantic merge analysis was made, except the line-level summaries in
  `SHARED-SEAM-PATCH-NEEDS.md`.
- Shared-seam paths were found by path pattern (`tools/q201_dryrun.py`, `SEAM`). A shared file with an unusual name
  would stay in the bulk list.
- "Shared permission catalog or seed": no differing row matched. Whether a Supply Chain role seed exists outside
  the scanned patterns: insufficient evidence.
- Module drafts without their own preimage manifest (the four UI drafts) were compared against the v2 stack.
- No build, no test, no runtime: nothing here says the integrated result compiles.

## 8. No-change verification

Stated in `ARTIFACTS.sha256` (header): branch, HEAD, porcelain line counts before and after, and the only new
paths — this folder. `git diff` was not run (prompt rule).

## 9. Refused / not done

- Nothing was applied, extracted, resolved, built or tested.
- The CT-QUEUE row (F-Q201-0) was not corrected — ledger work belongs to Q203.
- No ledger, module pack, code or `.antigravity` file was written.

## 10. Files in this folder

`SOP-22.md` · `ADD-OVERWRITE-CONFLICT.tsv` · `Q131-APPLICATION-STATE.tsv` · `NEW-FILES-STATUS.tsv` ·
`SHARED-SEAM-PATCH-NEEDS.md` · `ARTIFACTS.sha256` — required by the WP.
Supporting: `SHARED-SEAM-ROWS.tsv` · `SHARED-INTEGRATION-MEMBERS.tsv` · `IDENTICAL-ROWS.tsv` · `CHECKS.tsv` ·
`MODULE-OVERLAPS.tsv` · `TREE-ONLY-UNTRACKED.tsv` · `tools/q201_dryrun.py`.
