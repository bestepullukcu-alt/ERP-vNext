---
decision_id: MVP6-EVIDENCE-KIT-V1-1-ADOPTION-OWNER-DECISION-01
status: NOT APPROVED — prepared text only
prepared_by: AL-MVP6-KIT-V11-01 (chat lane, proposal only), queue Q57
prepared_at_local: 2026-09-26 (Europe/Istanbul)
decides: repository owner (Natig Yusubov, Chief Executive Officer), on CT presentation
basis: docs/records/decisions/2026-09/mvp6-evidence-kit-v1-1-revision-owner-decision-01.md (v1.1 needs its own approval)
record_on_approval: docs/records/decisions/2026-09/mvp6-evidence-kit-v1-1-adoption-owner-decision-01.md
---

# Owner decision needed to install evidence kit v1.1

**NOT APPROVED — prepared text only.** Nothing in this folder is active until the owner records this decision.

## 1. Question

Should the v1.1 files in `docs/roadmap/plans/mvp6-evidence-kit-proposal-02/proposed/` replace the v1.0 bytes as the files
installed under decision A1 (pilot, validated first)?

| Option | Effect |
|---|---|
| **V1 — Install v1.1 under A1's terms (recommended)** | The v1.1 bytes in §3 supersede the v1.0 bytes for installation. All other A1 terms stay. The validation run (Q24) runs on v1.1. |
| V2 — Revise again | Nothing is installed; CT names what to change, and a proposal-03 follows. |
| V3 — Keep v1.0 bytes | Install v1.0 as approved in A1, with the four known defects (the A12 run needed manual workarounds for all four). |

## 2. Exact decision text for V1 (to be recorded only if chosen)

> "The owner approves the MVP6 evidence kit v1.1 for installation. The environment owner installs the files listed in
> `docs/roadmap/plans/mvp6-evidence-kit-proposal-02/proposed/PLACEMENT.tsv` at the listed paths, byte-identical to the
> SHA-256 values in §3 of `docs/roadmap/plans/mvp6-evidence-kit-proposal-02/ADOPTION-DECISION-v1.1.md`. These bytes
> supersede the v1.0 bytes listed in decision A1 (`mvp6-evidence-kit-adoption-owner-decision-01.md`) for installation;
> the v1.0 bytes are not installed. All other terms of A1 stay in force unchanged: pilot status; the one line added to §5 of
> `docs/guides/operations/mvp6-development-process-v1.0.md` exactly as quoted in A1 (the guide keeps the file name
> `mvp6-evidence-kit-v1.0.md`); the identity method (inside a lane's own isolated Auth database only, never port 27017, the
> password hash of each named seeded user is replaced with a bcrypt(12) hash of a locally generated password, which is
> destroyed with the database at cleanup — in v1.1 that password exists only in the memory of the lane supervisor and of
> the processes it starts); no change to product code, `.antigravity`, gateway configuration, contracts, packs, guards or
> existing evidence; no reinterpretation of any existing approval, acceptance or exact-hash boundary; each lane still sets
> its own security switches in its work package; and durable PNG remains a separate decision. The kit becomes the
> required environment method for new MVP6 runtime lanes after one validation run on the Mac (Q24) has executed K00, K02,
> K05, K06, K07, K09, K11 and the final seal on the v1.1 bytes and CT has reviewed that evidence."

**What V1 does not decide:** which seeded users and legal entities a lane uses (the work package decides), PNG (decision B),
and any item outside D1–D4 and G1–G6 (`CHANGES.tsv`).

## 3. v1.1 file hashes bound by this decision

`sha256  path` (relative to `docs/roadmap/plans/mvp6-evidence-kit-proposal-02/`). Any byte change needs a new proposal
version. The 7 files marked `=v1.0` are byte-identical to the A1 list.

```
d973c37afbe0d36b238fe314ed50d74897b6f6c0910cb0457985ce323ad55300  proposed/PLACEMENT.tsv
5f80abe6795373d2414f7ccda11167c2176898e455db1494c1265503ba6bcbf5  proposed/guide/mvp6-evidence-kit-v1.0.md
b224250c5c6d0743dbdcb4f9ef8c94df7837aca97174975782e8463aac4480f5  proposed/kit/ek_lib.sh
d8e7b649f13a8170e05c798cb0b7b374461cf28373720329e6f15ce6e6375cbe  proposed/kit/k00_ctl.py
287681cdc102c01374a5707f1b46001c1caffcaefa6d778a3b43cde82979152a  proposed/kit/k00_supervisor.py
ca1949233e15b583a505b1e9acdaee77b3873fef07380ff0338735d490d38dac  proposed/kit/k01_source.py
5c36e940302233c89948c824eee03928537432c05bedbc3f5d1c50c8617324f4  proposed/kit/k02_runtime.sh   =v1.0
dbc04012a18642d8a7b0048dd2c58a41454538eef52f296bfde46b80031c8c27  proposed/kit/k03_ports.py   =v1.0
9372feff443ce14c9ec241a915cb7af9bb20920dfbb421beab9f4b984ac38833  proposed/kit/k04_config.py
2f628842f13f1f00b90020a0587edb40d1c3336fb242fe421ffe283a467cca16  proposed/kit/k04b_gateway_routes.py   =v1.0
2b8f71bb53be6ffd1284994350a939d3e50e03b64903901862899a93cb3b5904  proposed/kit/k05_mongo.sh   =v1.0
1628d6edb9f4e0ff595d1e0dec8db954828b9ca4e231ab0546b1aa943ee6c35c  proposed/kit/k06_build_launch.sh
2ce7c22c4d15d37e244ff639cc48a3281bf6af498d940ecc132f7287dfcab8b1  proposed/kit/k07_identity.py
0109f68d72e5a49d27b4261c730625df7a46194dc08f73ca4815e7ebccee2fb9  proposed/kit/k08_redact_scan.py
578b96c85e40bf0f36e93c83f838de7e7843f92dc52481044faefc99ed5cf227  proposed/kit/k09_binding.py
43ddf48a730fe663ef161f12afacc7bf4952bbf8bfe4dedecd694433e480580a  proposed/kit/k10_db_diff.py
dbcd4d8821e4299b8411f1d9a2ff14c9d069e4bc2cbe03fbadf8475fdb19b16c  proposed/kit/k10_db_snapshot.js
c0400c28b1e8714d8023d0d29f3674e58f2ecc643767a2101f18c89ee3211045  proposed/kit/k10_snap.sh
7a273dddeaefc79e53e596bb9398a5124166d76d269297d0ec25c6bc588330f9  proposed/kit/k11_cleanup.sh
0b98dc922d314df87873c37aa38ea817f5f2e1dd21e742d879031f1021918167  proposed/kit/lane.env.example
1cae7d7b13cd2522b0e9c4fd922e0f05ed2e6aad777f78993170daa1600a402a  proposed/kit/run-kit.sh
d41b3d28c7945da89e582315f47f90334e26028890de46801cdfc00ab595d15e  proposed/kit/service-map.tsv
8843c0c209ba2a08ffdbbbae413fd79a7951ff86543b823d46a3fed9f8cb9fba  proposed/templates/actors.tsv   =v1.0
3a5da74b97202c3eca79ca7bef285be2dee430a0700065269f1fc369fb84d27e  proposed/templates/overlays.tsv   =v1.0
bb493d327e88bfef406ab0e6009b2d901b2959b5c9ff0c3f9ac83b03bb45caf7  proposed/templates/overrides.tsv   =v1.0
9aaed0843deb0308eb0ae8c9af411b85cbab29e8d4f5055da1f9989e546cf7fb  proposed/templates/served-asset-hash.js
```

26 files: 15 changed, 4 new (`k00_supervisor.py`, `k00_ctl.py`, `k10_snap.sh`, `templates/served-asset-hash.js`),
7 unchanged. Install modes are listed in `proposed/PLACEMENT.tsv`.

## 4. Validation run Q24 (plan, not authorized until V1 is recorded)

This is A1 §2 on v1.1: environment owner, slot 8, suffix `EvidenceKitValidation01`, sources `templates/overlays.tsv`,
all six services, and one seeded T1/LE-A actor.

Pass conditions:
- every phase PASS;
- no `#pairing` row `FAIL`;
- `SECRET-SCAN-FINAL-aN.txt` PASS, written after ARTIFACTS.sha256 and carrying its hash;
- `CLEANUP.tsv` has no FAIL;
- `SOURCE-BINARY-PROCESS.tsv` all match/listening;
- one `k06 test` run, with TRX inside the evidence folder.

No acceptance row is opened or closed by this run.
