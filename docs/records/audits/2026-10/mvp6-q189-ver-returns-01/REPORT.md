# Q189 — independent static VER of Q187 (Returns backend guard) + Q188 (Returns UI v3) — SOP §37

| Field | Value |
|---|---|
| WP | Q189 / v1 (T3 v1, SOP v2.5 §17/§36.2) · CT-QUEUE `mvp6-process-pilot-01/CT-QUEUE.tsv:279` (READY) · read-only-auditor |
| Where | LANE 4, Cowork Linux VM (`uname -s` = `Linux`). Not LANE 2 / LANE 3 (writers of Q188 / Q187); see D-1 |
| Repo | `feature/mvp6-logistics` @ `4a8d4d4b339528a88e6220fb8402e5a2c771136c`; `GIT_OPTIONAL_LOCKS=0`; `git status --porcelain` only; no `.git/index.lock` at start or end |
| Base Stack | BASE-STACK v2 `ce8d60ab959ec7018c255ab1152983cfd8e301390f54b3d0ba23961bf2c4d882` (matches; static only) |
| Baseline | 565 porcelain lines at 16:06Z (32 ` M` + 2 untracked UC-01 + `dispatch-wp.md` + record folders) |
| Time | 2026-10-01 19:06 → 19:12 +03:00 (UTC 16:06 → 16:12) |
| Temp | `/tmp/q189` in the VM (outside the repo): BC-SOURCE pre tree, Q187 post tree, v2/v3 extracts, composed tree, tree-sitter venv |
| Authority | F-Q65b-1/2, OD-F-Q65b-1/2 (`mvp6-ct-verdicts-q183-q184-q65b-2026-10-01.md` `b226a4f4…`, lines 40–41, 48–49); pack MOD-0186 D186-05 (`933e8926…`) |

## 0. Verification report

```text
VERIFICATION REPORT

WP ID:               Q189 (independent static VER of Q187 + Q188)
Verifier:            LANE 4 (Cowork, Linux VM) — read-only-auditor; independent of LANE 3 (Q187) and LANE 2 (Q188)
Verification date:   2026-10-01
Branch/HEAD:         feature/mvp6-logistics @ 4a8d4d4b… (unchanged start → end)

Agent Verdicts:      Q187 "13 PASS, 1 N/A"; Q188 "13 checks all PASS"
Verification Verdict: FAIL (1 check) — P1–P5 PASS · B1–B7 PASS · U1–U5 PASS · X2 PASS · X1 FAIL (F-Q189-1)
CT Status:           pending (Agent PASS/FAIL ≠ CT ACCEPTED)

Evidence level achieved: E1 (hashes, extracts, byte diffs, composed tree, tree-sitter parse, node --check, XML parse, code-path review)
Required evidence level: E1 (static); compile and tests → Q190 (Mac)

Failed criteria:     X1 — manifest test M-02 still expects 9 enforced keys; after Q187 there are 8
Rework required:     small (Q188 overlay test file: M-02 expected count and its comment)
```

## 1. Preflight

| # | Result | Evidence |
|---|---|---|
| P1 | PASS | no `.git/index.lock`; porcelain only; no `git diff` |
| P2 | PASS | HEAD `4a8d4d4b…` |
| P3 | PASS | all 11 input hashes match; `sha256sum -c`: guard 5/5, v3 4/4, v2 4/4 |
| P4 | PASS | `docs/records/audits/2026-10/mvp6-q189-ver-returns-01/` absent at start |
| P5 | PASS | Q187 = LANE 3, Q188 = LANE 2; this chat wrote neither |

## 2. Checks (details in `VER.tsv`)

| # | Scope | Check | Result |
|---|---|---|---|
| B1 | Q187 | 5 members = OVERLAY-MANIFEST; 0644, 0/0, no AppleDouble; postimage hashes/sizes match | **PASS** |
| B2 | Q187 | 4 preimages = BASE-MANIFEST rows = BC-SOURCE files; new test absent from both; 0/33 Q117/Q121/Q131 overlap | **PASS** |
| B3 | Q187 | only 4 Returns backend files + 1 new test differ; route, wire format, error envelope, lifecycle unchanged | **PASS** |
| B4 | Q187 | no guard/filter uses `supplychain.returns.transition`; constants 3 → 2; `ForTarget` unchanged; no new key | **PASS** |
| B5 | Q187 | logic review (line numbers below) | **PASS** |
| B6 | Q187 | tests a–e present; each fails on the pre-image (b only by its new assertion — O-2) | **PASS** |
| B7 | Q187 | tree-sitter-c-sharp parse of 5 post + 4 pre files: 0 errors; compile N/A (Q190) | **PASS** |
| U1 | Q188 | 36 = 36 files; exactly the 10 FILE-PLAN files differ (hashes match); 26 byte-identical; tar paths/modes/owner/mtime equal | **PASS** |
| U2 | Q188 | R1 `#returnsTitle` `tabindex="-1"` (once); R2 fallback on create 403 and transition 403/404; toast and Add-disabled unchanged | **PASS** |
| U3 | Q188 | 0 hits for the generic key or any `Transition` constant reference in v3 | **PASS** |
| U4 | Q188 | Web controller: only the generic check removed (:164–168); ReturnFormContractTests: permission fact without Transition + title fact; nothing else | **PASS** |
| U5 | Q188 | routes, keys, page code, nav key, resx unchanged; 14 XML/resx parse; `node --check` OK | **PASS** |
| X1 | combined | composed BC-SOURCE + Q187 + v3: no code reference to the removed constants; M-03 consistent — **but M-02 is not** | **FAIL** |
| X2 | combined | UI row action → target key = backend `ForTarget`, 7/7 | **PASS** |

**B5 code path (post-Q187).**
- The middleware selects Returns endpoints by the attribute, so the keyless attribute still runs the pipeline (`ReturnContextMiddleware.cs:50–51`).
- The base key is checked only when the endpoint has one (`:64–65`).
- A target outside `ReturnStatus` gets 400 first (`:91`; `ReturnModels.cs:23`, ordinal `Enum.GetNames`).
- `grant = ForTarget(target)` (`:96`). If `grant` is null (`Requested`) or the caller doesn't hold it → 403 (`:97`). Otherwise the verified key is stored in `HttpContext.Items` (`:98`).
- The MVC filter requires `Permission ?? Items[TargetGrantItem]` and fails closed when the caller is unauthenticated, the key is null or not held (`ReturnPermissionAttribute.cs:15–17`).
- Result: target key only → pass; no target key → 403; only the old generic key → 403 (not a `ForTarget` value); `Requested` → 403 even with every key; non-status target → 400.

## 3. Findings

| # | Severity | Finding | Evidence |
|---|---|---|---|
| **F-Q189-1** | **MEDIUM (blocks the Q190 test run)** | `ReverseLogisticsManifestProviderTests.M02_…` still asserts `Assert.Equal(9, EnforcedPermissions.Count); // 3 constants + 6 target keys`. `EnforcedPermissions` reflects the public string constants of `ReturnPermissions` plus the `ForTarget` values. After Q187 removes `Transition`, that is 2 + 6 = **8** (emulated on the composed tree; 9 on BC-SOURCE). M-02 will fail at Q190. The comment on line 20 still lists "Read, Create, Transition". Q188 updated M-03 but not M-02 in the same file; the Q188 note ("M-03 passes only with Q187") covers M-03 only | v3 `overlay/services/…/ModuleRegistration/ReverseLogisticsManifestProviderTests.cs:20, :66` (unchanged from v2 :20, :68) |
| O-1 | LOW | Two v3 comments still say a keyless target "is left to the backend (422 / 400)" and cite `ReturnContextMiddleware.cs:92-95`. After Q187 the backend answers **403** for `Requested`. Comment text only; the adapter behaviour is unchanged and safe | v3 `Controllers/SupplyChainReturnsController.cs:178–179`; `Models/…/ReturnViewModels.cs:32–33` |
| O-2 | LOW | Q187 test (b) `Transition_WithoutTargetKey_Is403ForEachTarget` was already 403 on the pre-image. It fails there only through its new `TargetGrantItem` assertion, so it is a regression guard, not a defect detector. Tests (a), (c)-Requested, (d) and (e) detect the defect | `ReturnTransitionGuardTests.cs:50, :61–63` |
| O-3 | INFO | Pack MOD-0186 still describes the removed generic key: line 637 (annex key list), line 848 (M-03 allow-list), line 865 (row actions need `.transition`). Already reported by Q187 (F-1); after OD-F-Q65b-1 these lines and the M-02 count are out of date together | pack `933e8926…` :637, :848, :865 |
| O-4 | INFO | Q187 D-1 confirmed: for the transition endpoint a caller without the target key now gets the 400/404/415 checks before the 403 (target key needs the parsed body). Read/Create precedence unchanged | `ReturnContextMiddleware.cs:63–97` |

## 4. Deviations

| # | Level | Deviation |
|---|---|---|
| D-1 | INFO | This LANE 4 chat wrote the Returns UI **v2** (Q160), the base of Q188's v3. Q187 and Q188 themselves were written by LANE 3 and LANE 2, so P5 holds; U1–U5 judge only the v2 → v3 delta |

## 5. ASSUMPTIONs

- **A1:** The composed tree for X1 is BC-SOURCE + the Q187 overlay + the v3 `overlay/` contents copied to repo-relative paths; the Q117/Q121/Q131 layers do not touch Returns files (B2), so they do not change X1.
- **A2:** M-02's count is evaluated by emulating its reflection (public literal string fields + distinct non-null `ForTarget` values over `ReturnStatus`) on the post-Q187 `ReturnPermissions.cs`.

## 6. No-change verification

- `git status --porcelain` at the end is identical to the start (565 lines): the whole `docs/records/audits/2026-10/` folder was already one untracked entry (line 400), so the new folder does not add a line. `git status --porcelain --untracked-files=all` on the new folder lists only its 3 files. One other file appeared in `2026-10/` during the run from another lane: `mvp6-ct-verdicts-q185-q187-q188-q88b-2026-10-01.md` (mtime 16:08:09Z). Not read or used; the Q187/Q188 folders were unchanged (`sha256sum -c` at the end).
- No `.git/index.lock`, no git write, no `git diff`, no rm in the repo; no edit to the overlays, BC-SOURCE, pack or ledgers.

## 7. Files

`REPORT.md` · `VER.tsv` · `SHA256SUMS`

Agent verdict ≠ CT ACCEPTED — returning to CT.
