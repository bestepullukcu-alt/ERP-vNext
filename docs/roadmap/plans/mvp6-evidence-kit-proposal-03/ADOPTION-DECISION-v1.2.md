---
decision_id: MVP6-EVIDENCE-KIT-V1-2-ADOPTION-OWNER-DECISION-01
status: NOT APPROVED — prepared text only
prepared_by: AL-MVP6-KIT-V12-01 (chat lane, proposal only), queue Q66
prepared_at_local: 2026-09-26 (Europe/Istanbul)
decides: repository owner (Natig Yusubov, Chief Executive Officer), on CT presentation
basis: owner decision V2 (revise), docs/records/audits/2026-09/mvp6-ct-verdict-q63-kit-v2-2026-09-26.md
precondition: a narrow independent re-check of the 16 changed/new files (§4) is CT ACCEPTED before this is put to the owner
record_on_approval: docs/records/decisions/2026-09/mvp6-evidence-kit-v1-2-adoption-owner-decision-01.md
---

# Owner decision needed to install evidence kit v1.2

**NOT APPROVED — prepared text only.** Nothing in this folder is active until the owner records this decision.

## 1. Question

Should the v1.2 files in `docs/roadmap/plans/mvp6-evidence-kit-proposal-03/proposed/` replace the v1.0 and v1.1 bytes as the
files installed under decision A1 (pilot, validated first)?

| Option | Effect |
|---|---|
| **V1 — Install v1.2 under A1's terms (recommended, after the narrow re-check)** | The v1.2 bytes in §3 supersede the v1.0 bytes (A1) and the v1.1 bytes (proposal-02) for installation. All other A1 terms stay. The validation run Q24 runs on v1.2. |
| V2 — Revise again | Nothing is installed; CT names what to change; a proposal-04 follows. |
| V3 — Keep an older version | Not recommended: v1.0 and v1.1 both carry the F1 pairing defect (MDM registration rejected, MDM audit 401). |

## 2. Exact decision text for V1 (to be recorded only if chosen)

> "The owner approves the MVP6 evidence kit v1.2 for installation. The environment owner installs the files listed in
> `docs/roadmap/plans/mvp6-evidence-kit-proposal-03/proposed/PLACEMENT.tsv` at the listed paths, byte-identical to the
> SHA-256 values in §3 of `docs/roadmap/plans/mvp6-evidence-kit-proposal-03/ADOPTION-DECISION-v1.2.md`. These bytes
> supersede, for installation, the v1.0 bytes listed in decision A1 (`mvp6-evidence-kit-adoption-owner-decision-01.md`)
> and the v1.1 bytes of `mvp6-evidence-kit-proposal-02`; neither older set is installed. All other terms of A1 stay in
> force unchanged: pilot status; the one line added to §5 of `docs/guides/operations/mvp6-development-process-v1.0.md`
> exactly as quoted in A1 (the guide keeps the file name `mvp6-evidence-kit-v1.0.md`); the identity method (inside a
> lane's own isolated Auth database only, never port 27017, the password hash of each named seeded user is replaced with a
> bcrypt(12) hash of a locally generated password, which is destroyed with the database at cleanup — in v1.2 that
> password exists only in the memory of the lane supervisor and of the kit tasks it starts, and is shown only on the
> requesting user's own terminal); no change to product code, `.antigravity`, gateway configuration, contracts, packs,
> guards or existing evidence; no reinterpretation of any existing approval, acceptance or exact-hash boundary; each lane
> still sets its own security switches in its work package; and durable PNG remains a separate decision. The kit becomes
> the required environment method for new MVP6 runtime lanes only after one validation run on the Mac (Q24) has executed
> K00, K01, K02, K04, K05, K06, K07, K09, K11 and the final seal on the v1.2 bytes, meeting the pass conditions in §5 of
> this file, and CT has reviewed that evidence."

**What V1 does not decide:** which seeded users and legal entities a lane uses (the work package decides), PNG (decision B),
and any item outside F1–F14 (`CHANGES.tsv`).

## 3. v1.2 file hashes bound by this decision

`sha256  path` (relative to `docs/roadmap/plans/mvp6-evidence-kit-proposal-03/`). Any byte change needs a new proposal
version. Files marked `=v1.1` are byte-identical to proposal-02.

```
c32bae367a8b9c2a984a96293df9bd5ea1e5f3d058e5382449702f7750882b0a  proposed/PLACEMENT.tsv
e22fcae422e2511800fca9c454302d9be029c03dc5d443ad3d72e0db9862108c  proposed/guide/mvp6-evidence-kit-v1.0.md
33ea4ad97f4a6ee17d8e63d3859fc80c1d0a578142dbb0c2d309847c930b2478  proposed/kit/ek_lib.sh
5d81a37beb07f2fde3dcfde1ec7134a0c34df1fbffc5f0b354d6d9c5d395c9b7  proposed/kit/k00_ctl.py
46fb78fd67c33869028c510f216111035b847218ea69812ccec006ef3f32d7ab  proposed/kit/k00_supervisor.py
5fe14e5aa22da66e6be12e3a699301917082e1e10440f7d5298640a2461179e4  proposed/kit/k01_source.py
5c36e940302233c89948c824eee03928537432c05bedbc3f5d1c50c8617324f4  proposed/kit/k02_runtime.sh   =v1.1
dbc04012a18642d8a7b0048dd2c58a41454538eef52f296bfde46b80031c8c27  proposed/kit/k03_ports.py   =v1.1
b79beadd25e85af872d1c3e394be3679457f30111723c987c5d8b4f000ef04d6  proposed/kit/k04_config.py
2f628842f13f1f00b90020a0587edb40d1c3336fb242fe421ffe283a467cca16  proposed/kit/k04b_gateway_routes.py   =v1.1
2b8f71bb53be6ffd1284994350a939d3e50e03b64903901862899a93cb3b5904  proposed/kit/k05_mongo.sh   =v1.1
1628d6edb9f4e0ff595d1e0dec8db954828b9ca4e231ab0546b1aa943ee6c35c  proposed/kit/k06_build_launch.sh   =v1.1
8e30106d5e00c6d107be5fb795a5aea0fa859247ea2071f1554b04464f636429  proposed/kit/k07_identity.py
9913748b6abe63765dc5780a181a64b77e80f8aa15ffa61998036a47aa72b061  proposed/kit/k08_redact_scan.py
578b96c85e40bf0f36e93c83f838de7e7843f92dc52481044faefc99ed5cf227  proposed/kit/k09_binding.py   =v1.1
9c216257707ae8e4aba054d17e7a8b69a0918c71edfc45a970cad86f2078aa44  proposed/kit/k10_db_diff.py
dbcd4d8821e4299b8411f1d9a2ff14c9d069e4bc2cbe03fbadf8475fdb19b16c  proposed/kit/k10_db_snapshot.js   =v1.1
c0400c28b1e8714d8023d0d29f3674e58f2ecc643767a2101f18c89ee3211045  proposed/kit/k10_snap.sh   =v1.1
bfed75056e4e02669b626a75bafe233563aed48f111e0d3060c13cad85c1ebe7  proposed/kit/k11_cleanup.sh
751e6357c7dd08b4ddc60b5df1c5f7d838e914d57ad5eb01a9ecaa8046f275ae  proposed/kit/key-pairing.tsv
d260d4b8277cbc9a0af40250fba75bd5a1766f1dc6e4291857c6f457e07b82fc  proposed/kit/lane.env.example
93220a42ad25c13c6bd4d5178ec8e13d26961e2b6d51277a4ce1e92f523912c2  proposed/kit/run-kit.sh
c02c4cf45c4f361091401f45133b1535a5cdf64b783124acaa7239739746e051  proposed/kit/service-map.tsv
9a1e17eb531f20153c4067d4fee8a74c6223ee6d948f35d700997d6308904d57  proposed/kit/tests/test_k04_pairing.py
8843c0c209ba2a08ffdbbbae413fd79a7951ff86543b823d46a3fed9f8cb9fba  proposed/templates/actors.tsv   =v1.1
3a5da74b97202c3eca79ca7bef285be2dee430a0700065269f1fc369fb84d27e  proposed/templates/overlays.tsv   =v1.1
bb493d327e88bfef406ab0e6009b2d901b2959b5c9ff0c3f9ac83b03bb45caf7  proposed/templates/overrides.tsv   =v1.1
9aaed0843deb0308eb0ae8c9af411b85cbab29e8d4f5055da1f9989e546cf7fb  proposed/templates/served-asset-hash.js   =v1.1
```

28 files: 14 changed, 2 new (`kit/key-pairing.tsv`, `kit/tests/test_k04_pairing.py`), 12 unchanged from v1.1. Install modes
are listed in `proposed/PLACEMENT.tsv`.

## 4. Precondition — narrow independent re-check

Scope: the 16 changed or new files above, `CHANGES.tsv` rows F1–F14, and the product citations in `kit/key-pairing.tsv`.
Every citation must resolve in the HEAD + overlays source, and the pairing must match the product code. A lane that did
not write v1.2 re-runs `STATIC-CHECKS` items 6–13. The re-check needs no service, Mongo or browser.

## 5. Validation run Q24 (plan, not authorized until V1 is recorded)

This is A1 §2 on v1.2: environment owner, slot 8, suffix `EvidenceKitValidation01`, sources `templates/overlays.tsv`, all
six services, one seeded T1/LE-A actor.

Pass conditions:
- every phase PASS;
- in K04: no `#pairing` row `FAIL`, and no `NOT FOUND` in `raw/key-pairing-citations.tsv`;
- Platform accepted MDM's module registration, and at least one MDM audit append returned 2xx. This is the live proof
  of the F1 fix; K04 alone proves only the configuration.
- `raw/processes.tsv` shows only `127.0.0.1` listening addresses;
- `SECRET-SCAN-FINAL-aN.txt` PASS with `artifacts_verified PASS`, written after ARTIFACTS.sha256 and carrying its hash;
- `CLEANUP.tsv` has no FAIL, and no supervisor or socket is left afterwards;
- `SOURCE-BINARY-PROCESS.tsv` all match/listening;
- one `k06 test` run with TRX inside the evidence folder;
- one deliberate abort drill: a `run-kit.sh up` stopped by a failing phase leaves no lane process and no socket.

No acceptance row is opened or closed by this run.
