<!-- verify from: this folder -->
# Q435 — MVP-6 integrated acceptance gate: merge `origin/main`

    lane      : Q435 (ledger Q432/Q433/Q434) — MVP-6 INTEGRATED ACCEPTANCE GATE
    agent     : integration-agent
    authority : owner decision 2026-10-05 (merge-origin-main) > AGENTS.md > .antigravity/
    branch    : feature/mvp6-logistics
    HEAD      : cea01354e (2026-10-04)
    measured  : 2026-10-05
    verdict   : BLOCKED — the merge was NOT performed
    integrated PASS : NOT CLAIMED (K18). Gate steps 1–6 did not run.

## The stop, in one paragraph

`git merge origin/main` cannot run against this working tree. **Eight of the twenty-eight
overlapping files carry uncommitted local modifications, and all eight differ between `HEAD`
and `origin/main`** — so the merge must write every one of them, and git refuses to overwrite
uncommitted changes. GIT-002 forbids stash, reset and clean, and the owner decision does not
authorise them, so STEP 0's stop condition is met and I stopped there.

This is not incidental dirt. **The uncommitted content is precisely the content the contract
requires to survive the merge**: K22 and K23 in `control-tower-sop.md` (+62 lines) and the five
modules' navigation keys in all seven `SharedResource.*.resx` (+4 keys each). Had anyone cleared
the tree to make the merge run, the merge's own survival requirements would have been destroyed
first. Nothing was staged, committed, reset, cleaned or stashed.

## STEP 0 — the working tree as I found it

| | measured | contract said |
|---|---|---|
| modified (tracked) | **21** | 21 ✓ |
| untracked, porcelain entries | **40** | — |
| untracked, files recursive (`-uall`) | **172** | "171" — matches the recursive reading ±1 file, not the entry count |
| staged | **0** | — |
| merge/rebase in progress | none | — |
| total porcelain entries | **61** | — |

The 21 modified files are yesterday's and today's lanes: the K5/K6 and K22/K23 standards work,
the Q372 observability record, the process-pilot plan set, two SupplyChain module packs, the
seven `SharedResource` nav-key files, `Shipments/create.js` (the Q419 intent-root fix), three
SupplyChain service files and one SupplyChain test. The 40 untracked entries are the R4a/R4b/R4c
UI deliveries, three owner decisions and four audit records including Q425.

**All of it was already there when this lane opened. This lane added nothing but this record.**

## Geometry — CT's four figures, all verified exact

| | CT | measured |
|---|---|---|
| merge-base | `bc109afa4`, 2026-09-15 | `bc109afa4c0168…`, 2026-09-15 (PR #111) ✓ |
| main since base | 845 commits / 2297 files | 845 / 2297 ✓ |
| us since base | 18 commits / 8330 files | 18 / 8330 ✓ |
| overlap | 28 files | **28** ✓ |
| `merge-tree` reports conflicts | yes | yes, exit 1 ✓ |
| main's touches to `services/Diten.SupplyChainService` | 0 | **0** ✓ |
| main's touches to `Views/SupplyChain` · `Controllers/SupplyChain*` | 0 · 0 | **0** · **0** ✓ |

`origin/main` = `6c038399a` (2026-10-02, PR #133).

**Ref currency verified.** No `git fetch` was run; the already-present local ref was used. It was
then confirmed current against the live remote with `git ls-remote origin refs/heads/main`
(read-only, updates no local ref): remote tip and local ref are byte-identical at
`6c038399a1dfdc82128e7a0a03b690647e96eb5a`. Main's objects are complete locally — `merge-tree
--write-tree` built a full merged tree and 46 `appsettings*.json` blobs were read directly from
`origin/main`. **The geometry above therefore describes current main, not a stale snapshot.**
(`.git/FETCH_HEAD` is dated 2026-09-18 and is misleading: an earlier operation advanced the ref
without writing it.)

## BLOCKER 1 — the eight files

Measured per file: locally modified **and** `HEAD` ≠ `origin/main`, therefore the merge must
write it, therefore git refuses.

| file | local delta | HEAD vs main | merge-tree |
|---|---|---|---|
| `docs/guides/operations/control-tower-sop.md` | **+62 −0** (K22, K23) | DIFFERS | auto-merge |
| `frontend/Diten.Web/Resources/SharedResource.ar.resx` | +4 −0 | DIFFERS | **CONFLICT** |
| `…/SharedResource.en.resx` | +4 −0 | DIFFERS | **CONFLICT** |
| `…/SharedResource.es.resx` | +4 −0 | DIFFERS | **CONFLICT** |
| `…/SharedResource.fr.resx` | +4 −0 | DIFFERS | **CONFLICT** |
| `…/SharedResource.ru.resx` | +4 −0 | DIFFERS | **CONFLICT** |
| `…/SharedResource.tr.resx` | +4 −0 | DIFFERS | **CONFLICT** |
| `…/SharedResource.zh.resx` | +4 −0 | DIFFERS | **CONFLICT** |

The four uncommitted resx keys are `Nav.Module.ROUTINGLOADPLANNING`, `Nav.Page.LOADS`,
`Nav.Module.CLAIMSMANAGEMENT`, `Nav.Page.CLAIMS` — added symmetrically to all seven locales.
These are Bucket 3's "five modules' nav keys meet upstream's additions". They exist **only in
the working tree**.

`control-tower-sop.md` is the subtler case: it **auto-merges** cleanly, so it is not a conflict —
it is blocked purely because it is dirty. Its uncommitted +62 lines are K22 ("Lane closure,
workspace'i de kapsar") and K23 ("Implemented ≠ wired ≠ active ≠ evidenced").

`.antigravity/rules/docs-organization.md` is **not** in the overlap — the contract said so and it
is confirmed. It carries uncommitted K5/K6 and is therefore untouched by the merge; it survives
by not being in main's change set. It does not block.

### Unblock path — owner action, no history rewrite, no stash

    1. owner commits the 8 blocking files on feature/mvp6-logistics
       (or all 21 modified, which is cleaner and loses nothing)
    2. re-dispatch Q435 against the committed tree
    3. the merge then proceeds with 16 conflicted paths, resolved per the three buckets

Committing first is also the safer order on the merits: once K22/K23 and the nav keys are
commits, a conflicted resolution can be compared against a known base instead of against
working-tree bytes that no commit records.

## The 28, bucketed — and what `merge-tree` says will actually happen

Read-only `git merge-tree --write-tree --name-only HEAD origin/main` → exit 1, **16 conflicted
paths**. Index and worktree untouched: no `MERGE_HEAD`, 0 staged, 61 entries before and after.

### Bucket 2 — local corrective delta must survive · VERIFIED PRE-MERGE, AND THE RISK IS SILENCE

| | measured |
|---|---|
| `IInternalScopeResolutionContext` registrations on `origin/main` | **0** — the Q363 defect is live upstream, confirmed |
| registrations at `HEAD` | **1** · `Platform.Infrastructure/DependencyInjection.cs` `AddScoped<IInternalScopeResolutionContext, InternalScopeResolutionContext>()` |
| other Q363 lines at `HEAD` | `AddSingleton<IMdmServiceIdentityTokenProvider, …>` (Platform DI) · `AddHttpClient<ITenantLegalEntityScopeClient, PlatformTenantLegalEntityScopeClient>` (Auth DI) |
| Q302 `HumanCapitalService/Program.cs` | main **+2 −1**, ours **+20 −0** |
| Q302 `TalentEcosystemService/Program.cs` | main **+2 −1**, ours **+20 −0** |
| our eager validation shape | `ValidateRequiredJwtSetting(jwtSecret, "JwtSettings:Secret", minimumUtf8Bytes: 32)` + Issuer + Audience, each throwing `InvalidOperationException` |

**All four Bucket 2 files AUTO-MERGE. None of them conflicts.** That is the finding, and it
inverts the usual risk: git will produce a result silently, with no conflict marker to force a
human decision. The contract's warning — that taking main's side in the two `Program.cs` files
removes the eager validation and makes `8f60dc6d3`'s secret removal unsafe — describes an outcome
that would arrive **without any prompt**. Acceptance must therefore be a positive runtime proof
after the merge (the registration resolves, the validation throws on a missing secret), never an
inspection of whether the lines are still present.

### Bucket 3 — composite configuration

| file | merge-tree | note |
|---|---|---|
| the 7 `SharedResource.*.resx` | **CONFLICT** ×7 | also Blocker 1; semantic union required, never ours/theirs |
| `gateway/Diten.ApiGateway/ocelot.json` | auto-merge | silent result; upstream routes + shipment-bundle, then duplicate/ordering/auth-policy check and gateway smoke |
| `frontend/Diten.Web/wwwroot/assets/js/dt-defaults.js` | auto-merge | silent result; needs review, not acceptance by default |

### Bucket 1 — upstream-authoritative, with four of ours that must survive

| file | merge-tree | local | disposition |
|---|---|---|---|
| `AGENTS.md` | **CONFLICT** | clean | ours must survive — resolve, do not take upstream wholesale |
| `docs/guides/operations/control-tower-sop.md` | auto-merge | **dirty** | ours must survive (K22, K23) — **uncommitted**, Blocker 1 |
| `.antigravity/agents/frontend-ui-ux.md` | **CONFLICT** | clean | ours must survive |
| `.antigravity/rules/docs-organization.md` | n/a | dirty | not in overlap — survives untouched, confirmed |
| `docs/roadmap/backlog/product-backlog.md` | **CONFLICT** | clean | take upstream |
| `CrmService/…/KnowledgeContentsController.cs` | auto-merge | clean | take upstream |
| 7 × `Platform.Application.Tests` Mongo tests | **CONFLICT** ×5, auto-merge ×2 | clean | take upstream |
| 2 × `.csproj` (HumanCapital, TalentEcosystem Api) | auto-merge | clean | take upstream |

### Two paths the contract's bucketing did not name

**1 · `services/Diten.CrmService/src/Diten.CrmService.Api/appsettings.Development.json` —
`CONFLICT (modify/delete)`: deleted in HEAD, modified in `origin/main`, and merge-tree reports
"Version origin/main … left in tree."** This is a secret-gate landmine sitting inside the conflict
set: the file carries the class-B digest, we deleted it, and the default resolution leaves
upstream's version — reinstating the secret. It must be resolved to our deletion.

**2 · `services/Diten.Platform/src/Diten.Platform.API/appsettings.Development.example.json`** —
auto-merges although it is not among the 28. Benign on its face; recorded so the resolution set
is complete.

## SECRET GATE — measured on `origin/main`, no value printed anywhere

Values were never emitted. Every figure below is a count or a truncated SHA-256 of a value read
in-process.

    tracked appsettings*.json on origin/main      : 46
    carrying a JwtSettings.Secret                 : 45
      digest ff4555d13ae1 …………………………………………………… 22 files
      digest d8b34f9078d0 ……………………………………………………  2 files
      empty value ……………………………………………………………………… 21 files

**CT's count is correct for source configs, and I reproduced it exactly:**

| class | decision | measured |
|---|---|---|
| **A** — digest `d8b34f90`, 2 base configs we sanitised | our sanitised version survives | **2** — `HumanCapitalService/appsettings.json`, `TalentEcosystemService/appsettings.json`, both still carrying the secret on `origin/main` ✓ |
| **B** — digest `ff4555d1`, upstream Development configs | sanitised in the result, upstream provenance recorded | **12** real-source Development configs ✓ (13 exist; 1 has no secret) |
| **A + B** | "14 tracked appsettings*.json with a JwtSettings.Secret" | **14** ✓ exact |

### The gate as written cannot be satisfied by sanitising those 14 — finding B′

Ten **further** files carry the identical `ff4555d1` digest, in committed build output:

    frontend/Diten.Web/.tmp-fu07w/Debug/net8.0/appsettings.Development.json
    frontend/Diten.Web/.tmp-fu11/Debug/net8.0/appsettings.Development.json
    services/Diten.CrmService/src/Diten.CrmService.Api/.tmp-ctcap/Debug/net8.0/…
    services/Diten.CrmService/src/Diten.CrmService.Api/.tmp-fu10t/Debug/net8.0/…
    services/Diten.CrmService/src/Diten.CrmService.Api/.tmp-fu11/Debug/net8.0/…
    services/Diten.CrmService/src/Diten.CrmService.Api/.tmp-fu11c/Debug/net8.0/…
    services/Diten.CrmService/src/Diten.CrmService.Api/.tmp-fu11d/Debug/net8.0/…
    services/Diten.CrmService/src/Diten.CrmService.Api/.tmp-fu11t/Debug/net8.0/…
    services/Diten.CrmService/tests/Diten.CrmService.Application.Tests/.tmp-fu10t/…
    services/Diten.CrmService/tests/Diten.CrmService.Application.Tests/.tmp-fu11t/…

These are **pre-existing in both parents** — 797 tracked `.tmp-*` files at `HEAD` and 797 on
`origin/main`. The merge does not introduce them, and they are not an upstream import. But the
gate's instruction is "**scan the RESULTING TREE, not your intention — zero known secret
material, measured**", and the resulting tree will contain ten copies of the class-B secret no
matter how the twelve named configs are handled. **Sanitising only the 14 leaves the gate
unsatisfied on its own terms.** Either the ten are included in the sanitation scope, or the gate's
scope is amended by an explicit owner decision. This is a scope question, not a defect I may fix
in a merge lane.

### Class C — one item, recorded, not a blocker

`services/Diten.AuthService/src/Diten.AuthService.Api/appsettings.Development.json` on
`origin/main` carries two 45-character values — `ApiKey` and `InternalApiKey`, same digest
`e96ab27d` — alongside an empty `ApiKey`. I cannot classify them.

They do **not** enter the merge result: the file is absent at `HEAD` (we deleted it after the
base), `origin/main` did not modify it after the base, and merge-tree reports no conflict for it,
so our deletion stands. Recorded because the exclusion is incidental — a different resolution of
that path reinstates them.

Other credential-shaped keys on `origin/main`, source json only, values never printed:
`ClientSecret` 0 · `AccessKey` 0 · `PrivateKey` 0 · `BearerToken` 0 · `SasToken` 0 ·
`Password` 5 files, **zero non-empty** · `ConnectionStrings` 3 files, non-empty values of 11 and
25 characters with no embedded credential shape.

### Sanitation is not rotation

If the twelve class-B secrets are live, deleting them from the tree **revokes nothing**. They
remain valid wherever they are trusted, and they remain recoverable from 845 commits of history.
Rotation is a separate operation with a separate record, and this lane neither performs nor
implies it.

## Acceptance gate — steps 1 to 6

**None ran. The merge did not happen, so there is no integrated tree to measure.**

| # | step | status |
|---|---|---|
| 1 | build: every affected service compiles | NOT RUN |
| 2 | Platform + Auth targeted regression (578 / 133 files moved) | NOT RUN |
| 3 | gateway validation + runtime smoke | NOT RUN |
| 4 | frontend baseline (390/0/390 pre-merge) | NOT RUN |
| 5 | the five golden flows, reload after each step | NOT RUN |
| 6 | SupplyChain suite (432/1/433 pre-merge, read as a range per Q361) | NOT RUN |

**No integrated PASS is claimed (K18).** Per the owner's evidence lifecycle, Q372-R2 row 13, the
Q425 suite baseline and the five golden flows remain `VALID_PRE_MERGE / INTEGRATION_STALE` —
diagnostic and reference only, neither wrong nor discarded.

## Findings, ranked

1. **Merge mechanically impossible** against this tree — 8 overlap files uncommitted. Owner commits, then re-dispatch.
2. **The must-survive content is uncommitted** — K22/K23 and the five modules' nav keys exist only in the working tree. Any tree-clearing shortcut destroys exactly what the merge is meant to preserve.
3. **Secret gate scope is short by 10 files** — the resulting tree will carry 10 pre-existing copies of the class-B digest in committed `.tmp-*` build output. The gate's own wording fails unless scope is extended or amended.
4. **All four Bucket 2 files auto-merge** — the Q302/Q363 loss the contract warns about would occur silently, with no conflict to force a decision. Runtime proof is mandatory, not line inspection.
5. **A modify/delete conflict reinstates a class-B secret by default** — CRM `appsettings.Development.json`, upstream version left in tree. Must resolve to our deletion.
6. **Three Bucket 3 files auto-merge** (`ocelot.json`, `dt-defaults.js`, and `control-tower-sop.md` in Bucket 1). Auto-merge is not acceptance; each needs semantic review.
7. CT's untracked figure (171) counts files recursively, not porcelain entries (40). Cosmetic.

## Read-only compliance

    worktree entries before : 61
    worktree entries after  : 62   (= 61 + this record directory, and nothing else)

No merge was started. No commit, rebase, history rewrite, stash, reset or clean. Nothing staged —
`git diff --cached` is empty. No product defect was fixed. No conflict was resolved, because none
was created. No secret value was printed, logged or written to any artifact in this record.
`git merge-tree --write-tree` writes objects to the object database and touches neither index nor
worktree; verified after the fact — no `MERGE_HEAD`, 0 staged, entry count unchanged at 61.

`SOURCE-AS-MEASURED.sha256` records the 28 overlap paths as they stood at measurement. The merge
touched none of them; for the 8 blockers the hash is of working-tree bytes that no commit yet
records, which is the whole reason this lane stopped.

**Verdict: BLOCKED.** Resolvable by one owner commit, after which this lane can run end to end.
