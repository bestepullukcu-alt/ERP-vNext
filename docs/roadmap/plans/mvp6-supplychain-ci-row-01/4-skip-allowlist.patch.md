# `scripts/ci-skip-allowlist.tsv` — one line

Append (tab-separated: `suite`, `CODE`, `max`, `reason`):

```
supplychain-tests	CLAIMS_RESTART_MODE	1	WP-LANE-MAIN-SYNC-05 (owner decision 2026-10-16): owner SCE / MVP-6; Receipt_TwoIndependentTestProcesses_DurableRecovery is a TWO-PROCESS restart test and the author excluded it from the ordinary suite in its own words; it runs with CLAIMS_RESTART_MODE=write|read and CLAIMS_RESTART_TENANT; this project only
```

**max is 1, measured:** exactly one test carries the code
(`Claims/ClaimReplayTests.Receipt_TwoIndependentTestProcesses_DurableRecovery`). The file's own header
sets the rule — *"more than max skips with one code is red"* — so a second test acquiring the code is
red until the line is edited, which is the intended behaviour.

The TRX reason must START with the code for the allow-list to match it, which is why the attribute in
file 5 formats its `Skip` as `"CLAIMS_RESTART_MODE: …"`.
