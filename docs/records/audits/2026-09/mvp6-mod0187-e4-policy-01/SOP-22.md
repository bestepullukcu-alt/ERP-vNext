# SOP §22 — MVP6-MOD0187-E4-POLICY-01

Date: 2026-09-21  
Role: Claims evidence DEV  
Verdict: **FAIL (2 exact product/contract defects; 16 PASS rows; R08 PARTIAL)**

## Bound inputs

| Input | SHA-256 / result |
|---|---|
| AUTH-05 immutable source archive | `eaf786e647ef019f862c7dcc4f0b2ab29b2b5a36ff1bee98d2cca8d2863cc766` |
| AUTH-05 source manifest | `4ae0f286479761512f50216e5bd8be721e48ae4839c062b57cfdd29696e2daa8`; 97/97 matched before and after |
| Program.cs in snapshot | `a72a05a5e185e04d70ba221f086baacb8c8fca11ea061f289cb4b54fd430254c` |
| Published OpenAPI | `5dfe7c1bba32551bd8d4b532243878684e69a6d9560e667a4183bfd516b9d21c` |
| Published Claims annex | `16e65c26faeb53887607dd16de0de34bad61dcc89d3beb7f6b8adca0ec4eeb63` |
| Independent R01–R30 authority | `b3eb448d923eeb5d6b8eca3c6c4b51c10e963cc89d639d1e74175c3af182135e` |

Fresh restore/build exited 0. The resulting API DLL is
`d9449651bbdda031dc403d1932beef0927f9e09641e63194900e912b8effcfcd` and the test DLL is
`76a00ee6713de3ad2040d2fc8d33907934d35966d6e78f20e029665634e0facb`, matching the AUTH-05 writer handoff.

## Result

The controlled policy matrix ran 172 named observations. Supplemental probes covered real Shipment/Carrier uptake,
connection refusal, Mongo 500/503 shapes, ASCII header limits and previously missing omission/null/fingerprint/foreign-scope
cases. R01–R07, R09–R13, R15–R18 passed at the stated E4 boundary. R08 remains PARTIAL because two-process persistence,
capacity and unknown-commit evidence belongs to Lane C. R14 and R21 fail for the exact findings in `FINDINGS.md`.

Controlled dependency faults and real producer uptake are separated in every result JSON. A mock success is not used as
producer uptake. The real producer run passed non-nil root, missing-root handling and actual Carrier list consumption, then
exposed the nil-root incompatibility.

R19–R20 and R22–R26 were not assessed. No blanket PASS is issued. R27–R30 were not freshly rerun.

## Isolation and no-change

- API ports: 5072 controlled fixture, 5071 self-hosted producer; dependency fixture 18087.
- DB-010 test Mongo: 27931, replica set `claims_e4_policy`, database `diten_claims_e4_policy`; operational 27017 untouched.
- Product source, Program.cs, Returns, canonical contract, guard and git state were not modified by this lane.
- The only repository writes are this owned audit directory. All product execution occurred in `/private/tmp/mvp6-mod0187-e4-policy-01-iCdUbt`.
- Final cleanup shows all four listeners closed and the ephemeral signing secret removed.

Writer status: **complete for evidence; rework required for F-E4-01 and F-E4-02.**
