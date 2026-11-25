# Phase1 attempt2 — actual CI success, same H82 source

Run45141578014 **attempt2 SUCCESS**, job137587863018 completed2026-11-23T09:17:12Z. Official `gh run view --attempt 2` metadata and full raw log were retrieved read-only after completion. No rerun, test execution, code/environment/CI or root metadata change was made by this reviewer. Requested inherited/high; actual model/effort verilemiyor.

| Actual suite | Passed | Failed | Skipped | Total |
|---|---:|---:|---:|---:|
| TenantArchitecture.TenancyTests | 3 | 0 | 0 | 3 |
| TenantArchitecture.ArchitectureTests | 231 | 0 | 0 | 231 |
| Diten.Web.Tests | 4585 | 0 | 0 | 4585 |

Raw log lines2786/2794/2841 contain these complete summaries; line2842 says `[phase1] gates passed`. Metadata run+job+RunPhase1Gates step are completed/success. These are full suite summaries, not inferred from absence of logged failure lines. Individual passed-test names are not printed in this log; no named per-test result artifact is claimed.

Source: pull_request event metadata head H `82bc773938baa6689f0089b89c765b60f4411932`. Checkout log line178 prints full actual C `d4aff06421eaf9e3a64f8d462b671cee40c1f88e`; line175 describes its merge of H into B `58eb6fa901683296a0049a06910dc4b12e970950`. Independently read actual local Git commit object: C has direct parents B and H; `tree(C)==tree(H)==2b1bf19163ba07c304dd214ef22d91ab03bb20a3`. The result is therefore bound to the actual H82 source, beyond headSha metadata alone. No new current-worktree owner decision/uncommitted content was part of that checkout.

Raw attempt2 log `phase1-attempt2-log.stdout.txt`:726057bytes, SHA256 `7fa38652a5359d76ce81ff49af359557379a54c7e0bdb1c1fd9590adea64b731`. Complete metadata, source/summary rows, hashes and parent/tree measurements are in `phase1-attempt2-review.json`.

The original attempt1 is retained unchanged: `phase1-failed-steps.stdout.txt`, SHA256 `0f0fe48862a37f1ad72df07870450da87731a817ea6d003ee3db6a71158128ce`, Web4583PASS/2FAIL/0skip/4585total. Inventory actually failed during host startup with inotify128/openFDlimit IOException; Sandop actually lacked expected empty `data-legal-entity-id`, cause unproven from its truncated response. The unchanged-source retry's success is consistent with a nondeterministic environment or interaction issue. It does not identify or fix a cause, prove PVG innocent, prove baseline had the same failures, or retroactively erase attempt1. Existing Phase1 classification remains relevant historical evidence.

Root's separate `SOURCE-BOUND-PVG-CI.json` contains API456/RegPvBase211/TRX/source binding. This review did not rerun those services or modify that record. Phase1 is not a substitute for AC45 target/full TRX binding, JS/UI success, operational E4/E5 or independent human merge. Any new final H requires the new final-source obligations; this H82 success cannot be transferred to the proposed revision22 product/source tree.
