# MOD-0176 F2-FIX1 — evidence (WP-0176-F2-FIX1, CT-MVP1-0176-F2-FIX1 v1) — 2026-10-13

> **Status: agent result, pending CT** (F2 + FIX1 accepted together). Work branch `feature/sce/mod-0176-expiry-fefo`, base `120cf7666`.
> Isolated worktree `C:\Users\Ali\erp-vnext-wt-0176`. No push, no PR, no `main` merge (origin/main is ahead — not synced, per the WP),
> nothing on the MVP-1 branch. The owner's checkout and fleet were not touched. Contracts unchanged (INVENTORY 1.3.0, TRACE 1.4.0, FEFO 1.0.1).

## 1. Commits

| Commit | Content |
|---|---|
| `bc7f520e9` | fix: `ExpiryHookScope.For` — `sourceModule` never exempts; tests 2, 3(a)–(d); the F2 test that expected the exemption corrected |
| `476f4f6e2` | test: p95 hook measurement interleaved + non-parallel collection (§6, open point O-FIX1-2) |
| (this commit) | evidence + pack §8.3 / §8.3.2 / §8.3.4 text + §19.11 note |

## 2. The fix

`services/Diten.InventoryService/src/Diten.InventoryService.Domain/Expiry/Rules/FefoRules.cs` — the only production file changed:

```diff
-    public const string QuarantineModule = "MOD-0175";
-        if (string.Equals(sourceModule, QuarantineModule, StringComparison.Ordinal)) return ExpiryHookKind.None;
```

The `sourceModule` parameter stays (both callers in `ExpiryPostingPolicy.cs:58,75` unchanged) and is documented as not entering the decision.
`QuarantineModule` had no other user (measured).

## 3. MOD-0175 is exempt by TYPE (WP item 2) — measured

| Fact | Where |
|---|---|
| 0175 reaches the ledger only through `QuarantineMovementPoster.PostAsync` → `InternalMovementPostingService` | `QuarantineMovementPoster.cs:43,103`; no other user of `InternalMovementPostingService` outside `Features/Inventory` / `Program.cs` |
| Callers of the poster (2) | `QuarantineApplyEngine.cs:111` (decisions: `QuarantineStockEffects.Plan`), `StockActionHandlers.cs:153` (no-workflow hold / block) |
| Movements are built in | `QuarantineStockEffects.cs` (`new PlannedMovement` + `StatusTransfer` helper), `StockActionHandlers.cs:150` |
| Types | `STATUS_TRANSFER` (release / reject / block lines, unblock, rework, manual hold / block), `SCRAP` (Destroy), `SUPPLIER_RETURN`, `LOCATION_TRANSFER` (Sample) — all in the type exemption |
| `REVERSAL` | **not posted by 0175** (the CT finding listed it; `QuarantineIntake.cs:44` only observes reversals). No uncovered type → no BLOCKED condition |

Tests (`Expiry/ExpiryQuarantineTypeExemptionTests.cs`):
- `Every_movement_MOD_0175_plans_is_exempt_by_its_type` — runs the PRODUCTION planner over every `DecisionKind`, `LineDecision` × hold status
  and `DispositionKind` (+ the two manual helpers and the no-workflow STATUS_TRANSFER) and asserts `None` for the planned statuses AND for the
  issue shape (AVAILABLE → none) and the receipt shape (none → AVAILABLE), i.e. by type alone.
- `The_MOD_0175_posting_sites_and_types_are_the_measured_ones` — completeness: the poster call sites are exactly the two `path:line` above,
  the poster is the only internal-posting user, `new PlannedMovement(` appears only in the two builder files, and every `MovementType.*`
  named there is exempt by type. A new call site, builder or type turns it red.

## 4. New / changed tests

| Test | WP item | Result |
|---|---|---|
| `ExpiryIssueHookTests.FIX1_a_an_issue_claiming_sourceModule_MOD_0175_still_needs_a_reason` | 3(a): 422 `FEFO_BYPASS_REASON_REQUIRED`, no movement / snapshot, refusal audited | PASS |
| `ExpiryIssueHookTests.FIX1_b_the_same_issue_with_a_reason_posts_with_exception_and_snapshot` | 3(b): 201, `FEFO_BYPASS` (origin = poster, source module recorded), snapshot | PASS |
| `ExpiryReceiptHookTests.FIX1_c_a_receipt_claiming_sourceModule_MOD_0175_is_checked_like_any_other` | 3(c): 422 `LOT_BELOW_MIN_REMAINING_SHELF_LIFE` + `requiredToStockStatus = QUARANTINE`, nothing posted | PASS |
| `ExpiryQuarantineFlowRegressionTests.FIX1_d_blocking_and_destroying_a_later_lot_passes_without_a_FEFO_check` | 3(d): real 0175 flow on `QuarantineApiFactory` (hooks on): plain issue of the later lot → 422 (proves the hook is live), 0175 block (STATUS_TRANSFER AVAILABLE → BLOCKED) 201, approved Destroy (SCRAP from BLOCKED) Applied; no snapshot, no exception | PASS |
| `ExpiryQuarantineTypeExemptionTests` (2 facts) | 2 | PASS |
| `ExpiryF2RulesTests.The_hook_scope` | + `GOODS_RECEIPT_PO` with `MOD-0175` → Receipt; `GOODS_ISSUE_SALES` with `MOD-0175` → **Issue** (was None) | PASS |
| `ExpiryIssueHookTests.Transfers_and_quarantine_movements_are_exempt` → `A_location_transfer_of_a_later_lot_is_exempt` | the F2 test encoded the exemption (MOD-0175 issue → 201); now only the LOCATION_TRANSFER part, the MOD-0175 part is 3(a) | PASS |

## 5. Suites

| Suite | Before (`120cf7666`) | After (`476f4f6e2`) |
|---|---|---|
| `Diten.InventoryService.Tests` | 972 / 972 | **979 / 979** (+7) — see §6 for the p95 test |
| `TenantArchitecture.ArchitectureTests` | 69 / 77 | **69 / 77**, the same 8 reds (name-for-name diff empty); audit list 100 (unchanged — no new command) |
| `gateway/Diten.ApiGateway.Tests` | 134 / 135 | **134 / 135** (known port red) |
| Build (`--no-incremental`) | 0 warnings / 0 errors | **0 warnings / 0 errors** |

## 6. Sabotage

| | Sabotage | Guarding tests | Sabotaged | Restored | `git diff` |
|---|---|---|---|---|---|
| s1 | put `if (sourceModule == "MOD-0175") return None;` back | `FIX1_a_…`, `FIX1_c_…` | **2 red** (both) | 2 green | empty |
| s2 | drop `SCRAP` from the exempt types | `ExpiryQuarantineTypeExemptionTests` (2), `FIX1_d_…` | **2 red** (`Every_movement_…`, `The_MOD_0175_posting_sites_…`); `FIX1_d` **green** | 3 green | empty |

**Deviation (s2 / 3(d)):** the WP expected 3(d) red under s2. Measured: it stays green, because 0175's SCRAP leaves `BLOCKED` and the
issue rule only fires for AVAILABLE → none, so the status rule is a second, independent barrier. No real 0175 movement is AVAILABLE → none,
so no HTTP flow can show the type exemption alone; item 2's tests show it (issue shape) and go red. Runner: `sabotage.sh` (§8).

## 7. Open points

| ID | Point |
|---|---|
| O-FIX1-1 | (closed in text) pack §8.3, §8.3.2, §8.3.4 aligned to O-F2-2 and CT-F1. |
| **O-FIX1-2** | **The F2 p95 test (`The_hooks_add_at_most_50_ms_p95_with_200_lots_per_SKU`) is unstable in the full suite on this machine.** At `bc7f520e9` (FIX1 does not touch the hook cost path — one string compare removed): full-suite runs red / red / green / green / red / green (3 of 6), alone green. Diagnostics (3 runs, temporary, not committed): compliant-issue p50 with hooks 35.8 / **81.4** / 34.9 ms, without 16.6 / 15.5 / 15.6; the candidate read alone p95 10–12 ms in all three; in the slow run all 60 samples of that phase were > 50 ms (a whole-phase shift, not outliers). `476f4f6e2` interleaves the samples and runs the class alone: 3 runs → +20.1 / +22.1 / +33.5 (compliant), +27.5 / +23.0 / **+59.7** (bypass), +0.6 / +1.5 / +0.9 (receipt) — still 1 of 3 red. The typical added cost today is ~20–30 ms (F2 measured +9.4 / +9.7 on a quieter run). Budget not raised, test not skipped. CT to decide: keep in the default suite, move to a performance category run on a quiet host, or measure server-side time. |
| O-F2-1, O-F2-3, O-F2-5 | unchanged from F2. |

## 8. Scripts

```bash
unset INVENTORY_TEST_MONGO
dotnet build services/Diten.InventoryService/tests/Diten.InventoryService.Tests -c Debug --no-incremental
dotnet test services/Diten.InventoryService/tests/Diten.InventoryService.Tests --no-build --logger "console;verbosity=detailed"
dotnet test tests/architecture/TenantArchitecture.ArchitectureTests
dotnet test gateway/Diten.ApiGateway.Tests
```

`sabotage.sh` (run with the pack edit set aside so that `git diff` measures the restore only):

```bash
#!/usr/bin/env bash
set -u
REPO=/c/Users/Ali/erp-vnext-wt-0176
OUT=<scratchpad>/fix1/sabotage
mkdir -p "$OUT"; cd "$REPO"; unset INVENTORY_TEST_MONGO
P=services/Diten.InventoryService
T=$P/tests/Diten.InventoryService.Tests/Diten.InventoryService.Tests.csproj
RULES=$P/src/Diten.InventoryService.Domain/Expiry/Rules/FefoRules.cs
run() { dotnet build "$T" -c Debug > "$OUT/$1-build.log" 2>&1 || { echo "BUILD FAILED"; return; }
        dotnet test "$T" -c Debug --no-build --filter "$2" > "$OUT/$1.log" 2>&1
        grep -E "Başarılı!|Başarısız!" "$OUT/$1.log" | tail -1; grep -E "^\s+Başarısız " "$OUT/$1.log"; }
sabotage() { perl -0pi -e "$5" "$4"; git diff --numstat -- "$4"; run "$1-red" "$3"; git checkout -- "$4"
             git diff --quiet && echo "git diff empty" || echo "DIFF NOT EMPTY"; run "$1-green" "$3"; }
sabotage s1 "sourceModule exemption back" "FullyQualifiedName~FIX1_a_|FullyQualifiedName~FIX1_c_" $RULES \
  's/(if \(!hasRegisteredLot \|\| Exempt\.Contains\(type\)\) return ExpiryHookKind\.None;)/$1\n        if (string.Equals(sourceModule, "MOD-0175", StringComparison.Ordinal)) return ExpiryHookKind.None;/'
sabotage s2 "drop SCRAP from the exempt types" "FullyQualifiedName~ExpiryQuarantineTypeExemptionTests|FullyQualifiedName~FIX1_d_" $RULES \
  's/MovementType\.Reversal, MovementType\.Scrap, /MovementType.Reversal, /'
```

## 9. Diff (`git diff --stat 120cf7666..HEAD`)

`FefoRules.cs` (production, 1 file), tests (`ExpiryF2RulesTests.cs`, `ExpiryIssueHookTests.cs`, `ExpiryReceiptHookTests.cs`,
`ExpiryQuarantineTypeExemptionTests.cs` new, `ExpiryHookPerformanceTests.cs`), the pack, this evidence. No contract, no MOD-0175 code, no Web,
no gateway.
