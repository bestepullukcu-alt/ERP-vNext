---
decision_id: MVP6-EVIDENCE-KIT-ADOPTION-OWNER-DECISION-01
status: approved
decided_at_local: 2026-09-25T23:26+03:00
decided_by: Natig Yusubov, Chief Executive Officer (repository owner)
decision_source: owner choice of option A1 in the LANE 3 chat, confirmed by the owner in the MVP6 Control Tower conversation (in-app question), 2026-09-25
bound_to: docs/roadmap/plans/mvp6-evidence-kit-proposal-01/ADOPTION-DECISION.md sha256 2f282dade60b01d993ff7c1ba4e9256d28055ef31ca7b5a7880f5a0c9cb99d18
---

# MVP6 evidence kit — decision A1 (pilot adoption, validated first)

## Decision text (verbatim from ADOPTION-DECISION.md §1)

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

## Line to be added to process guide §5 (verbatim)

> `- Environment evidence is produced with the MVP6 evidence kit (guide: mvp6-evidence-kit-v1.0.md in this folder; scripts: scripts/evidence-kit/).`

## Candidate file hashes bound by this decision (ADOPTION-DECISION.md §4)

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

## Not decided here

Installation of the files and the §5 line is an execution step for the environment lane, done byte-identically and
verified against the hashes above. Decision B (durable PNG) and the other items listed in ADOPTION-DECISION.md remain open.
No commit, push, product, gateway, `.antigravity`, contract, pack or guard change is authorized.
