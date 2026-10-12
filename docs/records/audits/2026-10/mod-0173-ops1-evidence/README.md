# WP-0173-OPS1 — evidence index

Run scripts (`run_all.sh`, `run-diten.sh`, `run_watch.sh`, `watch-diten.ps1`) gain two entries:
Prism PRODUCT-MASTER mock on 4010 (FU-PM-01, temporary) and InventoryService on 5066 with
`Inventory__Upstream__ProductMasterBaseUrl=http://127.0.0.1:4010/` for that process only.

Measured base: `8fe909947` (INT-2 evidence commit; the WP expected `de976722b` or INT-2's last HEAD).
None of the four scripts changed between `de976722b` and `8fe909947`. While OPS1 was running, the CT committed
`3a0a21ccb` (INT-2 CT-ACCEPTED: module pack + implementation-status registry only); OPS1 is committed on top of it and
the scripts are identical in `8fe909947` and `3a0a21ccb`, so D1 against `8fe909947` is the script diff.

| file | check |
|---|---|
| `d1-diff.log` | D1 — numstat + every deleted line + full diff vs `8fe909947` |
| `d2-bash-n.log` | D2 — `bash -n` on the three staged `.sh` blobs |
| `d2-ps-parse.log` | D2 — Windows PowerShell 5.1 `Parser::ParseFile`: before 1 error, BOM-stripped control 1 error, after 0 |
| `d3-whatif-run_all.log` · `d3-whatif-run_watch.log` · `d3-whatif-run-diten.log` | D3 — command lines each `.sh` would run, via echo-only shims behind a guard |
| `d3-whatif-watch-diten.log` | D3 — command lines `watch-diten.ps1` would run, via stub functions behind a guard |
| `d3b-prism-argform-4099.log` | D3b — the scripts' Prism argument form served on a free port (4099) for 8 s |
| `d4-git.log` | D4 — commit stat, paths, status |
| `missing-services.md` | services absent from the scripts (report only) |
| `incident-unguarded-whatif.md` | first D3 attempt ran real commands — what ran, what it touched, clean-up |
