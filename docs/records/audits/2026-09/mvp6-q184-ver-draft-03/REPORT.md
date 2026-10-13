# Q184 — independent static VER of Q183 (S&OP v3 + Capacity v3 nav page key rename) — SOP §37

| Field | Value |
|---|---|
| WP | Q184 / v1 (T3 v1, SOP v2.5 §17/§36.2) · CT-QUEUE `mvp6-process-pilot-01/CT-QUEUE.tsv:268` (READY) · read-only-auditor |
| Where | LANE 4, Cowork Linux VM (`uname -s` = `Linux`). Not the Q183 writer (LANE 2) |
| Repo | `feature/mvp6-logistics` @ `4a8d4d4b339528a88e6220fb8402e5a2c771136c`; `GIT_OPTIONAL_LOCKS=0`; `git status --porcelain` only; no `.git/index.lock` at start or end |
| Base Stack | BASE-STACK v2 `ce8d60ab…` (unchanged; static check, nothing built) |
| Baseline | 563 porcelain lines at 20:32Z (32 ` M` + 2 untracked UC-01 + `dispatch-wp.md` + record folders) |
| Time | 2026-09-30 23:32 → 23:35 +03:00 (UTC 20:32 → 20:35) |
| Temp | `/tmp/q184` in the VM (outside the repo): v2/v3 extracts of both drafts, check scripts |
| Authority | F-Q84b-1, OD-F-Q84b-1, OD-NAVKEY-CAP (`mvp6-ct-verdicts-q84b-2026-09-30.md` `f57ee43c…`); key rule `NavNameLocalizer.cs` `e8299a5c…` |

## 0. Verification report

```text
VERIFICATION REPORT

WP ID:               Q184 (independent static VER of Q183, both v3 overlays)
Verifier:            LANE 4 (Cowork, Linux VM) — read-only-auditor; independent of the Q183 writer (LANE 2)
Verification date:   2026-09-30
Branch/HEAD:         feature/mvp6-logistics @ 4a8d4d4b… (unchanged start → end)

Agent Verdict:       Q183 "DRAFT v3 written; CHECK-RUN all PASS" (both drafts)
Verification Verdict: PASS — P1–P5 PASS; V1–V8 PASS for S&OP and for Capacity; V4 negative control fails on v2 as expected
CT Status:           pending (Agent PASS ≠ CT ACCEPTED)

Evidence level achieved: E1 (hashes, extracts, byte diffs, XML parse, independent Normalize, tar listings)
Required evidence level: E1 (static); the C# guard test runs at the Mac build

Failed criteria:     none
Rework required:     no
```

## 1. Preflight

| # | Result | Evidence |
|---|---|---|
| P1 | PASS | no `.git/index.lock`; porcelain only; no `git diff` |
| P2 | PASS | HEAD `4a8d4d4b339528a88e6220fb8402e5a2c771136c` |
| P3 | PASS | S&OP v2 SHA256SUMS `0bbb22a8…` / tar `7a9666c7…`; S&OP v3 `11782b43…` / `716e7c4c…`; Capacity v2 `6aba548d…` / `48b60b78…`; Capacity v3 `997d8f9b…` / `5c0a3b61…`; each `sha256sum -c` 4/4 OK; `NavNameLocalizer.cs` `e8299a5c…`; CT verdict `f57ee43c…` |
| P4 | PASS | `mvp6-q184-ver-draft-03/` absent at start |
| P5 | PASS | Q183 written by LANE 2; this chat wrote no Q183 file |

## 2. Checks (S&OP | Capacity)

| # | Check | S&OP | Capacity | Evidence |
|---|---|---|---|---|
| V1 | v2/v3 file lists identical | **PASS** | **PASS** | 47 = 47 files each; path lists equal |
| V2 | exactly 8 files differ | **PASS** | **PASS** | `diff -rq`: the 7 `SharedResource.{ar,en,es,fr,ru,tr,zh}.resx.fragment.xml` + `platform-registration.md`; 39 byte-identical |
| V3 | key name only; values byte-identical; XML well-formed | **PASS** | **PASS** | For each fragment, v2 bytes with `Nav.Page.SANDOP_PLANS"` → `Nav.Page.SANDOPPLANS"` (resp. `CAPACITY_PLANS` → `CAPACITYPLANS`), one occurrence, equal the v3 bytes (7/7 each; each file 1 byte shorter). All 14 v3 fragments parse (root `<fragment>`) |
| V4 | independent Normalize; keys in 7 fragments; negative control | **PASS** | **PASS** | Normalize re-implemented from `NavNameLocalizer.cs:66-78` (keep letters/digits, upper-invariant; self-tests from the guard: `TASK_RECURRENCE_RULES` → `TASKRECURRENCERULES`, `work-aggregation` → `WORKAGGREGATION`). S&OP provider: module `sop-workflow-signoffs` → `Nav.Module.SOPWORKFLOWSIGNOFFS`; nav-visible page (ParentPageCode null) `SANDOP_PLANS` → `Nav.Page.SANDOPPLANS`. Capacity: `capacity-planning` → `Nav.Module.CAPACITYPLANNING`; `CAPACITY_PLANS` → `Nav.Page.CAPACITYPLANS`. v3: both keys in 7/7 fragments. **Negative control on v2: FAIL as expected** — the page key is missing in 7/7 fragments of each v2 |
| V5 | codes, providers, routes, permissions, views, tests unchanged | **PASS** | **PASS** | covered by V2: the manifest providers, controllers, `Views/`, `wwwroot/`, `Resources/` and tests are byte-identical to v2; PageCodes `SANDOP_PLANS` / `CAPACITY_PLANS` unchanged |
| V6 | `platform-registration.md` item 5 | **PASS** | **PASS** | S&OP: line 14 key → `Nav.Page.SANDOPPLANS`, one added line 16 inside item 5 "Key = Nav.Page. + NavNameLocalizer.Normalize(PageCode)". Capacity: line 16 → `Nav.Page.CAPACITYPLANS`, added line 18 (same text). No other line changed |
| V7 | tar internal paths, modes, owners = v2 | **PASS** | **PASS** | `tar -tzv` mode/owner/path listing identical, 80 entries each (S&OP 755/644 0/0; Capacity 700/600 0/0, each as its own v2) |
| V8 | v3 FILE-PLAN / CHECK-RUN / CHANGES agree | **PASS** | **PASS** | FILE-PLAN: 8 rows, all 16 v2/v3 hashes per draft match the extracts; CHECK-RUN 14 PASS rows match V2–V6; CHANGES (C1–C2 / C3–C4, 39/47 unchanged, same paths/modes/owner, negative control) as found |

**Overall: PASS.** Both v3 overlays change only the nav page key (7 fragments) and item 5 of `platform-registration.md`; the new keys equal `Nav.Page.` + Normalize(PageCode), which v2 did not.

## 3. Findings

| # | Severity | Finding |
|---|---|---|
| I-1 | INFO | Capacity `overlay/_shared-integration/README.md:11` still names `Nav.Page.CAPACITY_PLANS`. Confirmed a description line only (file-table "what" column), not a key; the build reads the fragments. Owner decided 30 Sep to fix at integration (Q183 O-1). The S&OP README (:12) names no key |

No other findings.

## 4. ASSUMPTIONs

- **A1:** "nav-visible page" = a manifest page with `ParentPageCode: null`; in both providers that page is also the only one with `IsNavigationVisible: true` (:35), the detail page has a parent and `false` (:47–48).
- **A2:** Python `str.isalnum()` / `upper()` stands in for `char.IsLetterOrDigit` / `ToUpperInvariant`; all codes here are ASCII.

## 5. No-change verification

- `git status --porcelain` at the end = start + only `?? docs/records/audits/2026-09/mvp6-q184-ver-draft-03/`.
- No `.git/index.lock`, no git write, no `git diff`, no rm in the repo; no edit to the drafts, `NavNameLocalizer.cs`, tests or ledgers.

## 6. Files

`REPORT.md` · `VER.tsv` · `SHA256SUMS`

Agent PASS ≠ CT ACCEPTED — returning to CT.
