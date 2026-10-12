# MVP6 evidence kit v1.0 — specification (PROPOSAL, not active)

Queue item **Q06** · Lane-3 (environment owner, proposal only) · repo `feature/mvp6-logistics` @ `4a8d4d4b339528a88e6220fb8402e5a2c771136c`
Implements the evidence checklist of [MVP6 development process v1.0 §5](../../../guides/operations/mvp6-development-process-v1.0.md)
and the environment-owner row of §8. It does not change any acceptance, approval or exact-hash boundary.

## 1. What the kit is

The kit is a set of phases, K01–K11. Each phase is a small script with one job. It fails closed and writes only redacted evidence.
A lane runs `run-kit.sh up`, does its own acceptance work (HTTP, browser, K10 snapshots, K09 binding), then runs `run-kit.sh down`.
The acceptance probes stay with each work package. The kit covers only the environment and the evidence around them.

| Phase | File (candidate) | §5 checklist item it satisfies | Fails when |
|---|---|---|---|
| K01 source | `k01_source.py` | Exact source manifest and archive SHA-256; base HEAD; overlay list; *incomplete archive → HEAD-archive + overlay method* | branch/HEAD differ; any sealed archive/manifest hash differs; any manifest row mismatches after its overlay or at the end (undeclared overlap); unsafe archive member |
| K02 runtime | `k02_runtime.sh` | Native .NET 8 SDK/runtime versions | SDK ≠ expected 8.0.x; runtime 8.0.x missing; `global.json` redirect present |
| K03 ports | `k03_ports.py` | Ports in the source→binary→process chain | any port is forbidden (27017, 5000/5001, 5011–5064, 7000–7999, ≥49152) or already in use |
| K04 config | `k04_config.py`, `k04b_gateway_routes.py` | Effective configuration captured before start; no connection to 27017 | any Mongo value is not the lane replica set; any value names 27017; any local URL is not lane-owned; DB name not DB-010; explicit security key not set by the lane; lane secret or Auth issuer/audience not effective everywhere |
| K05 Mongo | `k05_mongo.sh` | Isolated DB-010 replica set on non-27017 port | port busy or 27017; replica set not PRIMARY |
| K06 build/launch | `k06_build_launch.sh` | Source → binary → process (binary hashes, ports, PIDs); native .NET 8 | build fails; runtimeconfig not net8.0; K04 not passed; listener PID ≠ launched PID; loaded runtime ≠ expected 8.0.x; any kit process connected to :27017 (`netcheck`) |
| K07 identity | `k07_identity.py` | Real Auth; no reusable credentials | user not exactly one match; user inactive/unconfirmed/must-change/locked/deleted; login ≠ 200; tenant or expected LE claim missing |
| K08 redaction | `k08_redact_scan.py` | Redacted raw evidence (no bearer tokens, cookies or reusable credentials) | any credential pattern, or any exact lane secret (also base64/URL-encoded), in any evidence file or archive member |
| K09 binding | `k09_binding.py` | Source → binary → process → **browser** | built DLL hash ≠ running DLL hash; PID no longer the listener; served asset ≠ source bytes; browser URL not on the lane Web origin |
| K10 DB | `k10_db_snapshot.js`, `k10_db_diff.py` | DB before/after for every mutation and every negative case | snapshot not taken on the lane port; expected delta/zero-write/state not met |
| K11 cleanup | `k11_cleanup.sh` | Cleanup record: processes, ports, secrets, Mongo data removed | any lane port still listening; workspace/secret/DB path still present; HEAD/branch changed; new repo status entry outside the lane evidence directory |

## 2. Why each phase exists (findings from the successful lanes)

These are measured observations, not opinions. The sources are listed in `REUSED-SCRIPTS.tsv`.

1. **No lane archived its launcher, its manifest verifier or its identity step.** The a08-a09-a12-01 `scripts/`
   contains only acceptance probes and `db-snapshot.js`. The carrier auth-chain lane records `verify_manifests.py`
   in `commands-exits.tsv`, but the script is in neither archive. The shipment integration verifier recorded "no exact
   replayable Auth fixture". Each lane re-invented these steps, and none can be replayed.
2. **Seeded users share one committed password hash.** `DataSeeder.cs` gives admin and every `john.doe/jane.smith/…`
   user the same bcrypt hash. The hash verifies against the seed password printed in `.antigravity/rules/dev-runbook.md`
   (checked for this proposal). A login as a seeded user therefore proves nothing about who holds the credential → **K07**.
3. **Each service reads Mongo from a different section, and every committed default is 27017.** Auth and Platform use
   `MongoDbSettings`, MDM and SupplyChain use `Mongo`, Web uses `ConnectionStrings:MongoDb` plus `DatabaseName`. Auth and
   Platform also carry a `UserSecretsId`, so in Development a user-secrets file sits between appsettings and environment.
   Setting the wrong section silently falls back (the ENVIRONMENT.md trap) → **K04**. The dry run below reproduces
   the trap: with `Mongo__…` in place of `MongoDbSettings__…`, K04 reports the value as coming from the user-secrets layer and refuses to start.
4. **Development settings reach external or operational systems.** Platform Development enables RabbitMQ eventing,
   SMTP to localhost, Hangfire jobs including `EmailDispatchJob`, and a WorkAggregation provider on 5058. Auth Development
   uses RabbitMQ. Every service points at Seq on 5341. K04 forces these off or onto the closed sink port. Any local URL it
   does not cover fails the check.
5. **The committed Gateway routes to operational ports.** `ocelot.json` has 275 downstream entries on 5004, 5011 and 5056–5065.
   The lanes used a disposable `gateway-runtime/ocelot.json` (a08 recorded its hash). **K04b** reproduces that. Only the lane
   services are mapped: 94 entries in the dry run. The other 181 go to a closed sink port, so no route can reach a live
   operational service.
6. **JWT issuer and audience differ between committed appsettings.** Values seen: `diten-auth-service`, `Diten` and empty.
   SupplyChain has none, although its `Program.cs` requires them. K04 pins every lane service to the issuer and audience Auth signs with.
7. **Repo helpers kill by port or by process name.** `run_all.sh` runs `kill -9` by port and `killall -9 dotnet`, and dev-runbook
   shows `kill -9` by port. With parallel lanes (§8 pilot limit) that would hit other lanes. **K11** stops only PIDs the kit
   started, after checking that their command line still contains the lane workspace.
8. **Archives carry macOS AppleDouble members.** The Auth successor `final-source.tar.gz` has 3 333 `._*` entries next to its
   3 333 files. K01 skips and counts them. Extracting them blindly would add 3 333 junk files to the build tree.
9. **Lanes captured redacted config, listener lists, cleanup tables and repo status before/after.** These formats are good and
   are kept: `effective-config-redacted.json`, `listeners-before-cleanup.txt`, `CLEANUP.tsv`, `SOURCE-BINARY-PROCESS.tsv`, `repo-status-*.txt`.

## 3. Phase details

### K01 — exact source input
- `git archive <expected HEAD>` of the commit, not the dirty working tree (345 status entries at measurement). That goes into an empty
  `W/source`. Then the ordered overlays from `overlays.tsv` (template: the exact a08 inputs).
- Every archive and manifest is hash-checked before use. Two manifest formats are accepted: `path/sha256` (e.g. COMBINED-360)
  and `path/baseline/target/disposition` (e.g. FINAL-22; target `-` means the file must be absent).
- Every manifest is checked right after its overlay and again at the end. A later overlay may replace a path only if the
  earlier row lists it in `declared_overlaps`.
- The output `raw/SOURCE-TREE-MANIFEST.tsv` covers every file of the disposable source. Its SHA-256 is the source identity that K09 binds to.
  `raw/repo-status-before.txt` is kept for K11.

### K02 — native .NET 8
- The lane dotnet is `/Users/natig/.dotnet/dotnet` by default. It must report SDK = `EK_EXPECTED_SDK` (8.0.417 in accepted evidence) and
  both `Microsoft.NETCore.App` and `Microsoft.AspNetCore.App` at `EK_EXPECTED_RUNTIME` (8.0.23). No `global.json` redirect is allowed.
- Launch policy: `DOTNET_ROLL_FORWARD=LatestPatch`, so patch roll-forward is allowed and minor/major is not. K06 then proves the runtime the
  process actually **loaded** (`lsof` on the PID shows `Microsoft.NETCore.App/<version>`).

### K03 — ports
One slot S from 1 to 9 per lane: Gateway `5000+100S`, Web `+1`, Auth `+56`, Platform `+57`, MDM `+59`, SupplyChain `+61`,
sink `+99`, which stays closed, and Mongo `30994+1000S`. The block keeps the familiar suffixes the lanes already used
(a08: 5900/5901/5956/5957/5959/5961). The check covers the forbidden ranges, "not listening" and "bindable", and `lsof` output is saved.
K11 re-checks the same set after cleanup.

### K04 — configuration, verified before start
- `secrets`: generates lane secrets for the groups in `--rotate` (default `jwt mfa`) into `W/secrets/lane-secrets.json`
  (0600), and pins the Auth issuer/audience into `lane-shared.json`.
- `render`: writes one `W/env/<service>.env` (0600) from `service-map.tsv`. It covers the Mongo connection string and DB name for each
  service's **own** section, DB-010 names `Diten<Svc>_<SUFFIX>`, peer URLs (a peer outside the lane goes to the sink), forced
  switches (Seq/SMTP/RabbitMQ/Hangfire/holiday provider off), lane overrides and secrets.
- `check`: re-resolves the effective configuration in ASP.NET Core order: `appsettings.json` < `appsettings.{Env}.json`
  < user-secrets (Development) < environment < command line (`--urls`). It reads the files from the content root the process
  will actually use. Rules C1–C6 are listed in the table above.
- Output: `raw/effective-config-<svc>.json` with every key, its value (secrets as `[REDACTED lane-generated|inherited:<layer>]`)
  and the layer it came from, plus `effective-config-summary.tsv` and `effective-config-findings.txt`. K06 refuses to launch unless
  the findings file says `none`.
- **Explicit keys.** `TenantResolution__DevBypassEnabled` (Auth, Platform), `DocumentRegisterSeed__Enabled` and
  `BusinessReferenceData__CatalogLoad__Enabled` (Platform) change security or seed behaviour. The kit does not choose them.
  Each lane sets them in `overrides.tsv`, and a missing or placeholder value fails K04.
- The static resolver approximates .NET configuration. It does not see code defaults or extra JSON sources such as `ocelot.json`.
  The runtime guard is therefore K06 `netcheck`: no kit PID may hold a TCP connection to `:27017`, and Mongo users must be
  connected to the lane port.
- **K04b** copies the built Gateway output to `W/gateway-runtime` (the DLL hash must be unchanged) and rewrites `ocelot.json` there.
  It writes `raw/gateway-routes.tsv` with the source and runtime `ocelot.json` hashes.

### K05 — Mongo (DB-010)
One lane-owned `mongod`, started with `--bind_ip 127.0.0.1 --replSet rs<SUFFIX> --port <K03>`, `dbpath` under `W/mongo`, and a single-member
replica set because transactions need one. DB-010 rules: one fixed database per service per lane (suffix, never a GUID), with
isolation inside the lane by tenant ID. Tests do not create databases per run. Output: `raw/mongo-rs-status.json`
(`operational27017Used:false`), `raw/mongod-version.txt`.

### K06 — build and launch
- `dotnet restore` + `dotnet build -c Release --no-restore` per service from `W/source`. The offline-cache restore mode
  (`--ignore-failed-sources -p:NuGetAudit=false`) is recorded when used, as a08 did. It is never presented as a full online restore.
- Each launch uses `env -i` plus the K04 env file and `--urls http://127.0.0.1:<port>`, and Web also gets `--contentRoot <source project>`.
  Nothing from the operator's shell can leak in, and `--urls` outranks the `Urls` key that SupplyChain's appsettings carries.
- Output: `raw/processes.tsv` (service, PID, port, start time, DLL path + SHA-256, cwd, loaded runtime, health HTTP code,
  argv). The environment is never read with `ps e`. Also `raw/binary-sha256.txt`, `raw/netcheck.tsv` and `raw/lane-mongo-connections.txt`.
  Platform aggregate health 503 (`business_reference_data_provider`) is recorded as observed. It is not re-labelled PASS.

### K07 — fresh real-Auth identities
- For each actor in `actors.tsv`: a password from `secrets.token_urlsafe(24)`, generated locally, is bcrypt-hashed at cost 12 with the `$2a$` prefix,
  exactly as `PasswordHasher.cs` does. It replaces **that one user's** `PasswordHash` in `DitenAuth_<SUFFIX>.users` on the lane
  port. The mongosh step re-checks the server port before writing.
- Preconditions are checked and **not** changed: active, email confirmed, not must-change, not locked out, not deleted.
- Login goes through the lane Gateway (`POST /api/tenant-auth/login`, `X-Tenant-Id`). The Auth-issued token is decoded in memory to check
  `tenant_id` and, when expected, `legal_entity_id`, then discarded. Evidence (`raw/identities.json`) holds claim names and
  match booleans, `previousHashWasRepoSeed` and cookie *names*. It holds no password, hash or token.
- The only copy of each password is `W/secrets/actors.json` (0600). `reveal --actor` prints one password to the terminal
  for manual browser login. K11 destroys it together with the lane database. **Residual risk:** a password typed into
  a browser by an agent appears in that agent's transcript. It is single-lane and dead after K11, which is why it is not reusable.
- Out of kit scope: the seeded users must already carry the tenant/LE/position data the acceptance needs. a08 used three actors
  (`T1/LE-A`, `T1/LE-A`, `T1/LE-B`) but archived only one email. `templates/actors.tsv` marks the others as placeholders, and K07 fails on them.

### K08 — redaction and scan
- `redact`: bearer headers, cookies, JWTs, token, password, secret and API-key JSON fields, Mongo credentials and bcrypt hashes.
- `scan`: pattern rules (the union of the prior SECRET-SCAN rules) plus an **exact-value** scan for every lane secret and
  actor password, including their base64 and URL-encoded forms, across all files and `.tar/.tar.gz` members. The report names
  file, line and rule only.
- Order matters: scan → K11 → pattern-only rescan (`SECRET-RESCAN-AFTER-CLEANUP.txt`), because K11 destroys the secrets the
  exact scan needs.

### K09 — binding (while the lane is still running)
`SOURCE-BINARY-PROCESS.tsv` has one row per component:
- source-tree manifest SHA-256;
- DLL SHA-256 now = at build;
- PID is still the listener;
- loaded runtime;
- cwd;
- health.

Browser binding goes in `raw/browser-binding.tsv`. Named static assets fetched from the lane Web port must be byte-identical to their
source files (e.g. `Shipments/details.js`, the A08/A09 refresh path). Every browser URL the lane used must be on `127.0.0.1:<web>`.
`localhost` and `127.0.0.1` are different origins for cookies (dev-runbook), so the kit pins `127.0.0.1`.

### K10 — DB before/after
`k10_db_snapshot.js` is the parameterised a08 `db-snapshot.js`. It is refused unless the server port is the lane port.
`k10_db_diff.py` asserts `0` (zero-write), `+N/-N` (exact delta), `@N` (absolute) and state fields, and appends to
`raw/db-assertions.tsv`. The rule is one pair for every mutation **and** every negative case.

### K11 — cleanup and verification
It stops kit PIDs in reverse order, with SIGTERM, a wait, and SIGKILL only for that PID. Mongo is stopped with `shutdownServer`. Every lane port must show no
listener. The workspace is removed and verified: source, builds, env files, secrets, Mongo dbpath, gateway-runtime and logs. Repository
no-change: HEAD and branch unchanged, and no new `git status` entry outside the lane's evidence directory. Browser tab/profile closure
is an operator row (`EK_BROWSER_CLOSED`). Output: `CLEANUP.tsv` and `raw/listeners-before-cleanup.txt`.

## 4. Evidence layout produced (per lane)

```
docs/records/audits/<yyyy-mm>/<lane-id>/evidence/
  COMMANDS.tsv  CLEANUP.tsv  SOURCE-BINARY-PROCESS.tsv  SECRET-SCAN.txt  SECRET-RESCAN-AFTER-CLEANUP.txt
  raw/ source-baseline.tsv source-overlays.tsv source-manifest-verification.txt SOURCE-TREE-MANIFEST.tsv repo-status-*.txt
       dotnet-info.txt dotnet-sdks.txt dotnet-runtimes.txt runtime-check.tsv PORTS-before.tsv ports-lsof-before.txt ports.json
       effective-config-<svc>.json effective-config-summary.tsv effective-config-findings.txt gateway-routes.tsv
       mongod-version.txt mongo-rs-status.json build/<svc>-{restore,build}.{log,exit} binary-sha256.txt processes.tsv
       health-<svc>.{body,headers} netcheck.tsv lane-mongo-connections.txt identities.json browser-binding.tsv
       <label>.json (K10 snapshots) db-assertions.tsv listeners-before-cleanup.txt
```
Lane acceptance files (SOP-22, ACCEPTANCE, ARTIFACTS.sha256, EVIDENCE-REUSE rows) stay the lane's responsibility.

## 5. Durable PNG — a separate environment dependency (not in the kit)

Status stays **OPEN** (BLOCKERS B02 / queue Q13 / `mvp6-carrier-real-auth-ct-review-01/PNG-DISPOSITION.md`).
I checked the supported save and export mechanisms available to agents in the current tooling (25 Sep 2026, this session's tool definitions):

| Mechanism | Durable PNG file? | Status |
|---|---|---|
| Built-in browser (`Claude_Browser` `computer` → `screenshot`/`zoom`) | No. The image is returned to the agent only; there is no path or export parameter | Not a save/export mechanism |
| Built-in browser `read_page`, `get_page_text`, `find` | No (text/DOM) | Does not satisfy the criterion (PNG-DISPOSITION) |
| Desktop computer use (`computer_screenshot`, `computer_app_screenshot`) | No. The image is returned to the agent only | Also "native capture", not authorized |
| Claude in Chrome (`gif_creator`) | Records a GIF, not a PNG. Not available in this session | Not applicable |
| SendUserFile / Artifact | Moves files that already exist on disk; cannot receive a screenshot the agent was shown | Not a capture mechanism |
| Playwright + Chromium in the cloud container (`page.screenshot({path})`) | Yes, but the container cannot reach the Mac's `127.0.0.1` lane without a tunnel | Tunnel **not authorized** |
| Playwright/headless browser on the Mac | Yes | Drives the browser over CDP and is a different browser session from the real-Auth IAB one. CDP is **not authorized** (ENVIRONMENT.md) |
| Data-URL/base64 extraction from the page | — | **Not authorized**; not attempted |
| Operator OS screenshot (human saves the file into the lane evidence folder; K08/K09 then hash and bind it: SHA-256, capture time, tab URL = lane Web origin, source-tree manifest) | Yes | Supported and uses no bypass. It needs a human action, which is the owner's choice |

No PNG was captured for this proposal and no restriction was tested or bypassed. The closure options (operator capture, or authorizing
a named mechanism) are a **separate owner decision** (PNG-DECISION in `ADOPTION-DECISION.md` §3, not part of adoption).

## 6. Validation performed for this proposal

| Check | Where | Result |
|---|---|---|
| Python compile, `bash -n`, `shellcheck -S warning`, `node --check` on every candidate | cloud workspace | PASS (0 warnings after two fixes) |
| K01 against the **real** sealed a08 inputs (HEAD `4a8d4d4` + COMBINED-360 + FINAL-22) | linked-computer shell, scratch copy outside the repo | **PASS: 360/360 and 22/22**, 14 251 HEAD files, 3 333 AppleDouble members skipped; tree manifest `78f45246…9064` |
| K01 negatives: wrong HEAD, tampered hash, undeclared overlap, declared overlap, `../` member | synthetic repo | STOP / FAIL / FAIL / PASS / refused, as designed |
| K03 + K04b + K04 on the real source tree, all six services, Development | scratch copy | K04b: 275 routes, 94 mapped, 181 to the sink. K04 **first run found 3 real gaps**: SupplyChain `PlatformRegistration:BaseUrl` → 5057, and issuer/audience mismatch ×2. After the service-map fix: PASS |
| K04 negatives: missing explicit key, wrong Mongo section, GUID suffix, deleted env key | synthetic and real tree | FAIL with the layer named (user-secrets or appsettings.Development.json), as designed |
| K08 redact/scan incl. exact secret inside a `.tar.gz` member | synthetic | PASS on the redacted file, FAIL on raw/leaked content; the secret value is never printed |
| K07 bcrypt compatibility (`$2a$12$`, verify), seed-hash detection (1 distinct hash in DataSeeder), JWT claim decode | linked-computer shell | PASS |
| K10 diff (delta, zero-write, absolute, state) | synthetic | PASS/FAIL as designed |
| **Not executed:** K02, K05, K06, K07 DB write + real login, K09, K11 on the Mac | — | These need the Mac's native dotnet/mongod. The linked shell is a Linux VM without them. The first adoption run is the validation run (ADOPTION-DECISION §2) |

## 7. Open items (for the adoption run, not decided here)

1. **Internal service credential pairings are not confirmed.** Groups such as `auth-platform`, `auth-events`,
   `platform-mdm-*`, `sc-platreg` and `web-platform` are inherited from committed defaults unless added to `--rotate`. K04 records them as
   "unverified pairing". The integration owner confirms which keys must match before those groups are rotated.
2. **ASP.NET environment per lane.** `Development` pulls user-secrets and the Development JSON. K04 handles both, but the value
   earlier lanes used was not recorded.
3. **Actor fixtures.** The emails and LE assignments for actor-b and actor-le-b must come from the Platform/MDM seed or the lane's own fixture step.
4. macOS specifics (`lsof -Fn`, `ps -o lstart`, `mongod --fork`) are written for bash 3.2 and the BSD tools but are unexecuted (see §6).
