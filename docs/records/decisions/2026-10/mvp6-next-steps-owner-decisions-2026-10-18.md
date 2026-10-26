# MVP-6 next-steps — four owner decisions (2026-10-18)

- owner: **ny@gmgroup.ch** — relayed by the Control Tower, 2026-10-18 (all four CT recommendations accepted)

| # | item | decision |
|---|---|---|
| 1 | **Q151 integrated PASS** | CT brings the lane stack up itself, **guarded**: lane mongo replica set on a non-27017 port; every service's mongo URI overridden by env var; CT refuses to start any service still pointing at `localhost:27017` (the operational DB — MDM and Auth default to it). Run the five golden flows **through the gateway (5000)**; stop and report if any guard trips. |
| 2 | **Merge to main** | Open the PR **after** the Q151 integrated PASS is recorded, so main receives the finished core with its strongest evidence in one merge. |
| 3 | **Wave 6.1 G4 fixture seed** | **Config-driven, non-production only**: seed the known demand fixture (dp-2027 / v3) from configuration, enabled only in Development/Staging. Production composes but seeds nothing until DEMAND v2 (G3). No test GUIDs hard-coded in `Program.cs`. Resolves G4-COMPOSITION-SPEC item A. |
| 4 | **G2 SUPPLY-CONSTRAINTS producer** | **MOD-0193 BOM & Routings** (`Diten.ManufacturingService`, supply-chain-execution) — routings define work-center/resource operations, the natural source of available capacity per resource/period. Sets the draft's `x-owner`; freeze still needs the canonical-checksum spec + test vector. |

BL-652 remains parked (owner-applied-later; the permission-template file is not writable from this session; not on the Q151 critical path — the test JWT carries `mdm.legal-entities.read`).
