# Q235 — Is the exposed value live or dead?

The value is never reproduced in this record, not even in part. It is identified only through the sha256 of the
three files that carry it.

## 1. What it is

| Question | Answer | Evidence |
|---|---|---|
| Kind | A **symmetric HMAC-SHA256 JWT signing secret**. Not a bearer token, not a private key | Pre-fix `runtime_probe.py` (sha256 `e5ee0aed…a4a6`): the constant `SECRET` is the key of `hmac.new(SECRET, signing, hashlib.sha256)` that signs the probe's tokens |
| Length class | 47 characters / 47 bytes (376 bits as used) | measured in memory |
| Form | A human-readable descriptive phrase of 7 separator-delimited tokens, mixed case and digits. **Not random key material.** It describes itself as a test credential of one module's probe | measured in memory; token contents deliberately not listed |
| Where the service reads its signing secret | `JwtSettings:Secret`, required from configuration at start-up, at least 32 bytes | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/Program.cs:25-26` |
| How the value reached the service | The pre-fix probes put it into the environment of an API process **they started themselves**: `JwtSettings__Secret` in the `env.update(...)` call | pre-fix `failure_probe.py` (`8f6cc448…5a63`), `restart_probe.py` (`296c150a…5dcf`), `runtime_probe.py` |
| Default in configuration | None. `appsettings*.json` of the service has no `Secret` entry | grep = 0 |

## 2. Evidence

| Check | Result |
|---|---|
| Any current source, appsettings, test or script in the working tree containing the value | **0 loose files.** 26,845 files read (everything except `.git/` and `node_modules/`). Every occurrence is inside an archive |
| What Q117 put in its place | **Not another literal.** The current probe takes `MOD0185_PROBE_JWT_SECRET` from the environment, or generates `secrets.token_urlsafe(48)` per run (`services/Diten.SupplyChainService/tests/loads/runtime_probe.py:8-10, :60`). Current file hashes equal the Q117 overlay hashes (`71fd51b7…`, `ad6ddbc0…`, `5c78f3c1…`) |
| Any test secret in the current tree equal to it | None (same scan) |
| Test-only by construction | Yes: a descriptive phrase, used only by three probe scripts under `tests/loads/`, handed to a locally started process |
| Lane environment outside the repo (`~/mvp6-env`) | **Partial scan** (stopped at a time limit after 672,314 files): 15 carriers, all of them copies of the old probe files under `tests/loads/`; 0 configuration, environment or log files |
| Any deployed or long-running environment configured with it | **Not determinable from this machine's repository.** The old probe also had a mode for an already-running API (`--start-local` off); an operator would then have had to configure that API with the same value. No record of that was found; absence of a record is not proof |

## 3. Verdict

**DEAD in the repository** — nothing in the current tree reads it, nothing is configured with it, and its
replacement is a per-run value, not a new constant.

**CANNOT DETERMINE for anything outside the repository** — whether a running service somewhere was ever
started with this value as `JwtSettings__Secret`. Only the owner or operations can answer that. One check
settles it: inspect the `JwtSettings__Secret` / `JwtSettings:Secret` of every environment that has ever run
`Diten.SupplyChainService`.

The Q101 decision already treats the value as exposed ("the former static signing secret is treated as exposed
and is gone" — current `runtime_probe.py:7`). This record does not change that.
