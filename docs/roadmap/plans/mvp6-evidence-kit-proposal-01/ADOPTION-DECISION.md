---
decision_id: MVP6-EVIDENCE-KIT-ADOPTION-OWNER-DECISION-01
status: NOT APPROVED — prepared text only
prepared_by: MVP6 Lane-3 (environment owner, proposal only), queue Q06
prepared_at_local: 2026-09-25 (Europe/Istanbul)
decides: repository owner (Natig Yusubov, Chief Executive Officer), on CT presentation
record_on_approval: docs/records/decisions/2026-09/mvp6-evidence-kit-adoption-owner-decision-01.md
---

# Owner decision needed to adopt the MVP6 evidence kit

There is one decision now (A). The PNG decision (B) is prepared but **HELD until A is recorded**, so decisions come one at a time.

## 1. DECISION A — adopt the evidence kit

**Question:** Should the evidence kit in `docs/roadmap/plans/mvp6-evidence-kit-proposal-01/proposed/` become the standard
environment kit for MVP6 runtime lanes?

| Option | Effect |
|---|---|
| **A1 — Adopt as a pilot, validated first (recommended)** | Install the files at the paths in `proposed/PLACEMENT.tsv`, byte-identical to the hashes in §4. The first use is one validation run on the Mac by the environment owner (no acceptance claim) to execute the phases not yet run: K02, K05, K06, K07, K09 and K11. The kit becomes the required environment method for new MVP6 runtime lanes only after that run's evidence passes CT review. |
| A2 — Adopt and require immediately | As A1, but required for the next dispatched runtime lane without a separate validation run. That lane carries the risk of the six unexecuted phases. |
| A3 — Keep as reference only | Nothing is installed. Lanes may copy parts, and each lane stays responsible for its own launcher, identity step and cleanup, as today. |
| A4 — Reject | Proposal stays as a roadmap record only. |

**Exact decision text for A1 (to be recorded only if chosen):**

> "The owner adopts the MVP6 evidence kit v1.0 as a pilot. The environment owner installs the files listed in
> `docs/roadmap/plans/mvp6-evidence-kit-proposal-01/proposed/PLACEMENT.tsv` at the listed paths, byte-identical to the
> SHA-256 values in §4 of `ADOPTION-DECISION.md`, and adds the one line to §5 of
> `docs/guides/operations/mvp6-development-process-v1.0.md` quoted there. This authorizes the kit's identity method:
> inside a lane's own isolated Auth database only (never port 27017), the password hash of each named seeded user is replaced with a
> bcrypt(12) hash of a locally generated password, which is destroyed with the database at cleanup. It authorizes no change to
> product code, `.antigravity`, gateway configuration, contracts, packs, guards or existing evidence, and it does not
> reinterpret any existing approval, acceptance or exact-hash boundary. Each lane still sets its own security switches
> (`TenantResolution__DevBypassEnabled` and the other explicit keys) in its work package. The kit becomes the required
> environment method for new MVP6 runtime lanes after one validation run on the Mac has executed K02, K05, K06, K07, K09 and K11
> and CT has reviewed that evidence. Durable PNG remains a separate decision."

**Line added to process guide §5 under A1 or A2 (exact):**

> `- Environment evidence is produced with the MVP6 evidence kit (guide: mvp6-evidence-kit-v1.0.md in this folder; scripts: scripts/evidence-kit/).`

**What A1 does not decide:**
- Internal service-credential pairings. The integration owner confirms them before those groups are rotated (KIT-SPEC §7.1).
- Which seeded users and legal entities a lane uses. The work package decides.
- PNG (decision B).

## 2. Validation run under A1 (plan, not authorized until A1 is recorded)

- Environment owner, slot 8, suffix `EvidenceKitValidation01`, sources = `templates/overlays.tsv` (a08 inputs), all six services.
- Actor: one seeded T1/LE-A user. Pass condition: every phase PASS, `SECRET-SCAN` PASS, `CLEANUP.tsv` has no FAIL, and
  `SOURCE-BINARY-PROCESS.tsv` all match/listening.
- Output: `docs/records/audits/2026-09/mvp6-evidence-kit-validation-01/evidence/`. No acceptance row is opened or closed by this run.

## 3. DECISION B — durable PNG mechanism (HELD until A is recorded)

**Question:** Which supported mechanism should close the durable-PNG criterion (B02/Q13)?

| Option | Effect |
|---|---|
| **B1 — Operator OS screenshot, bound by the kit (recommended)** | A human saves a screenshot of the authenticated lane page into the lane evidence folder. K08 scans it and K09 records its SHA-256, capture time, tab URL (lane Web origin) and source-tree manifest. There is no agent capture and no bypass. |
| B2 — Authorize a named automated mechanism | e.g. headless Playwright on the Mac. This uses CDP, which ENVIRONMENT.md lists as not authorized, and it runs a different browser session from the real-Auth one. It needs explicit owner authorization naming the mechanism. |
| B3 — Keep OPEN | PNG stays a mandatory open criterion; CT verdicts that depend on it stay PARTIAL. |

## 4. Candidate file hashes bound by decision A

`sha256  path` (relative to this proposal directory). Any byte change needs a new proposal version.

```
97775075281d7f70539c2fe61330e8b493c3c7e79179dabf58aca5a5d9b5e98d  proposed/PLACEMENT.tsv
1a95942c80841ea5fbaf1c028b8b597bdd15456302e37d505f065adcf5d0f7fa  proposed/guide/mvp6-evidence-kit-v1.0.md
09faff5a3b88298fd43d27966ee5f160e6b0f54aae6fb4cf1d706c8b799e8861  proposed/kit/ek_lib.sh
d6cbb0155784fbe36bc021ed5fc073c6f11a7b88d3c1a4abfab1c2cf87d1d978  proposed/kit/k01_source.py
5c36e940302233c89948c824eee03928537432c05bedbc3f5d1c50c8617324f4  proposed/kit/k02_runtime.sh
dbc04012a18642d8a7b0048dd2c58a41454538eef52f296bfde46b80031c8c27  proposed/kit/k03_ports.py
b7bda37722292f6e8075e79416d06e7171cee6fcc377c8c822b30ed29b9f0efd  proposed/kit/k04_config.py
2f628842f13f1f00b90020a0587edb40d1c3336fb242fe421ffe283a467cca16  proposed/kit/k04b_gateway_routes.py
2b8f71bb53be6ffd1284994350a939d3e50e03b64903901862899a93cb3b5904  proposed/kit/k05_mongo.sh
4759c6b891f33cd2bad4f66ee0189bfa719eb6a717031667aded8d6e20085200  proposed/kit/k06_build_launch.sh
d20ea3a3e9521916ec54189573acdd96fedeeb922c6964c55d400837ae6456fa  proposed/kit/k07_identity.py
e30e9ac6731e2a403ebaa157ad80ab1740b11997b72e9dd8918882ef9e7f5451  proposed/kit/k08_redact_scan.py
fe9b624756b4034f60dee33775bc115533a8fbda8f1d656b87c535950b2b738b  proposed/kit/k09_binding.py
11b9b0da4b3f53166915499840ec064060132a621f11df7e2eea843333243106  proposed/kit/k10_db_diff.py
03de54c6b1c011a1af01b931bfdc3be9b79af3474bccd9010a82cd75eb714674  proposed/kit/k10_db_snapshot.js
ed8718250384f976fe7edd60b49d7f8acf344fbea366c355745ad031aa0e4cb1  proposed/kit/k11_cleanup.sh
8a73d524d625e6d3de8f2e16bae8f55e721bd26ab0e1c0d86158f037ff8c7730  proposed/kit/lane.env.example
40c7121d5b741af545d9da3d5b13973e1f5bd703bd2a9fe1a6dbbd0623e1cba9  proposed/kit/run-kit.sh
8d8d015685f49c7967b2610861983631befce6255b3ca6e52748c8afe5fb9a1e  proposed/kit/service-map.tsv
8843c0c209ba2a08ffdbbbae413fd79a7951ff86543b823d46a3fed9f8cb9fba  proposed/templates/actors.tsv
3a5da74b97202c3eca79ca7bef285be2dee430a0700065269f1fc369fb84d27e  proposed/templates/overlays.tsv
bb493d327e88bfef406ab0e6009b2d901b2959b5c9ff0c3f9ac83b03bb45caf7  proposed/templates/overrides.tsv
```
