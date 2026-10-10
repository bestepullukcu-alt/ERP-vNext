# O/M/P effort by delivery (person-hours)

Estimates, not commitments. Calibration: the MDM and DevEnablement providers and the unapproved Carrier candidate (374-line patch)
already exist, so the foundation is mostly adaptation. The integrated target and a native executor are prerequisites and are **not** counted.

| Delivery | O | M | P | Note |
|---|---:|---:|---:|---|
| Pack/DCP sections after approval (DCP-009 follow-up + five pack sections) | 3 | 5 | 8 | documents |
| Foundation: interface, options, hosted service, csproj/Program.cs/appsettings diffs, F-01…F-06 | 4 | 8 | 14 | D2=A |
| Providers + M-01…M-08: Shipment (3 pages, 4 actions) | 4 | 6 | 10 | |
| Carrier (adapt candidate, add both-direction tests) | 2 | 4 | 7 | |
| Loads (1 page, 1 action) | 2 | 3 | 6 | only after the Loads UI exists |
| Returns (1 page, 8 actions, D5) | 3 | 5 | 9 | |
| Claims (1 page, 7 actions) | 3 | 5 | 9 | |
| Frontend guards W-01, W-02, W-04 | 4 | 8 | 14 | source-parsing like the nav guard |
| Nav keys: 11 keys × 7 languages (3 already drafted) + l10n review | 2 | 4 | 8 | |
| Reconcile-state R-01 (Platform fixture test) | 3 | 6 | 12 | |
| Runtime R-02…R-04 (isolated Platform + SupplyChain, restart, nav smoke) | 4 | 8 | 16 | native executor |
| Independent VER | 4 | 8 | 14 | |
| **Total (D2=A)** | **38** | **70** | **127** | 4.8 / 8.8 / 15.9 person-days |
| Extra if D2=B (Platform credential + allow-list + tests) | 4 | 8 | 14 | Platform owner |
| Extra if D5=B (six constants in `ReturnPermissions.cs` + Returns regression) | 1 | 2 | 4 | Returns owner |

These rows are **not** yet in `mvp6-effort-shipment-ct-update-07/EFFORT.tsv`, and no existing REMAINING row names self-registration. CT decides whether
to add them as new rows or to fold them into each module's "5-REMAINING integration" rows; this package does not edit that file. The work is delivered
in waves with each module's UI: the foundation, guard, reconcile and VER rows land with the first module; later modules add only their provider row, their key share and a
VER re-run.
