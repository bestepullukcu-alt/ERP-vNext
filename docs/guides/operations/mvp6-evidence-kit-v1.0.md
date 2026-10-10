# MVP6 evidence kit v1.2 — how to run a lane

> **CANDIDATE — NOT ACTIVE.** Proposed location: `docs/guides/operations/mvp6-evidence-kit-v1.0.md`. The file name stays
> v1.0 so the process-guide §5 line approved in A1 still points to it; the content is v1.2. Proposed kit location:
> `scripts/evidence-kit/`. Adoption needs the owner decision in
> `docs/roadmap/plans/mvp6-evidence-kit-proposal-03/ADOPTION-DECISION-v1.2.md`. Until then, nothing here is a rule.

Use the kit for every MVP6 runtime lane that needs real Auth, Gateway, UI and an isolated DB. It covers the environment half of the
process §5 evidence checklist (`docs/guides/operations/mvp6-development-process-v1.0.md`). Acceptance probes, the SOP-22
report and EVIDENCE-REUSE rows stay with the work package.

## Before you start

1. Pick a free **slot** 1–9. The slot is your whole port block, so two lanes never share one. Write it in the CT queue row.
2. Pick one **DB suffix**, e.g. `ShipmentA12Ver01`. DB-010 means one fixed suffix per lane and never a GUID.
3. Copy `scripts/evidence-kit/lane.env.example` to a workspace **outside** the repository and fill it in. `EK_SOCK` must be
   outside `EK_WORK`, in a folder only you can write. The per-user `$TMPDIR` qualifies; `/tmp` and `/private/tmp` do not,
   and the supervisor refuses them. Put your browser/HTTP harness scripts in `EK_HARNESS_DIR`, which is the only folder
   the supervisor will run scripts from.
4. Fill three small files next to it, starting from `templates/`:
   - `overlays.tsv`: the sealed source inputs in order, with their SHA-256. A single wrapper folder in an archive is
     stripped only when that folder is not a real top-level path of HEAD; `raw/source-overlays.tsv` records it.
   - `actors.tsv`: one row per browser/HTTP identity, with the tenant and expected legal entity.
   - `overrides.tsv`: your explicit value for each security-relevant switch (e.g. `TenantResolution__DevBypassEnabled`).
     The kit will not guess them. Literal secret values are rejected here.

## Run

```bash
set -a; . ./lane.env; set +a
scripts/evidence-kit/run-kit.sh up        # K01 source … K07 identities; stops at the first failure
```

`up` starts the **lane supervisor** (`k00_supervisor.py`). It generates every lane secret once, in memory:
- the JWT and MFA keys;
- one internal key for the six keys that Platform checks against `AuthService:InternalApiKey` (Auth ×2, SupplyChain,
  Web, MDM's audit key and Platform itself);
- one MDM module-registration secret (Platform `ModuleRegistrationCredentials:Mdm:ActiveSecret` = MDM
  `PlatformRegistration:ModuleRegistrationCredentialSecret`);
- the Platform→MDM service-identity secret;
- one password per actor.

It launches the services with those values in their environment. Config files and env files hold only placeholders
`@@LANE:<group>@@`.

K04 refuses to continue unless two things hold:
- every key in `key-pairing.tsv` is still at its cited line in the source you are running;
- every service resolves the same effective value for each pairing group (`#pairing` rows;
  `raw/key-pairing-citations.tsv`).

The supervisor serves only your own Unix user, never gives out a secret over its socket, and runs only kit tasks.
It stops by itself after 12 h, or after 4 h without a request.

If `up` fails at any step, it runs the abort path by itself: the supervisor stops its services, clears the values
and removes the socket, then K11 cleans up. `up` then exits non-zero. `run-kit.sh abort` does the same by hand.

Then do the lane's work while everything is running:
- **Commands:** run each command through `ek_run`. Run harness scripts that need actor passwords through
  `k00_ctl.py "$EK_SOCK" run harness <file> -- <args>`; they get `ACTOR_PW_<LABEL>` and nothing else. Each produces
  exactly one `COMMANDS.tsv` row with the real exit code. A phase row can follow the rows of the steps inside it; read
  them by start time. Never add rows by hand, and never put `$(date)` next to an exit-code echo.
- **HTTP/browser acceptance** goes through `http://127.0.0.1:<web port>` only. For a manual browser login, run
  `k00_ctl.py "$EK_SOCK" reveal <actor>` in your terminal. Type the 6-digit code the supervisor writes to that terminal;
  the supervisor then writes the password to the same terminal. The password is valid until `seal`.
- **DB before/after:** take a snapshot around every mutation, every **setup step** and every negative case with
  `k10_snap.sh <label> '<spec>'`, which writes a new `-aN` file each time and never overwrites one. Then run
  `k10_db_diff.py before after --expect … --totals …`. Collections you do not list must stay unchanged unless you pass
  `--allow-unlisted "<reason>"`. Asynchronous writes you do not control (for example Platform background jobs) can
  be excluded from the whole-DB rule, but only with `--totals-exempt <collections> --exempt-reason "<why>"`, and both
  are recorded. A reused file pair is refused.
- **Negative controls with a replaced asset:** first record the file with `k09_binding.py record-served --label … --file …`.
  Then serve it with `templates/served-asset-hash.js` (`fulfillHashed`), which hashes the bytes actually served. K09 fails
  unless every served hash matches a recorded one.
- **Tests:** `k06_build_launch.sh test <label> <csproj>` always runs `dotnet test` with `--results-directory` inside the evidence
  folder, with a TRX file.
- **Binding** before you stop anything: `k09_binding.py --served <asset=source path> --browser-url <urls you used>`.
- Run every raw capture that might hold headers or bodies through `k08_redact_scan.py redact` before it lands in `evidence/`.

```bash
EK_BROWSER_CLOSED=PASS scripts/evidence-kit/run-kit.sh down   # netcheck → cleanup (supervisor stays up)
# write ARTIFACTS.sha256 now (the lane's last content write)
scripts/evidence-kit/run-kit.sh seal                          # exact-value + pattern scan = LAST write; supervisor shutdown
```

## What must be true at hand-off

- `SECRET-SCAN-FINAL-aN.txt` ends in `result PASS`, was written after `ARTIFACTS.sha256`, records that file's hash,
  and shows `artifacts_verified PASS`: every listed hash is correct and every evidence file is listed.
- The final scan also opened every zip (Playwright traces included), gzip and tar file. Do not archive any other
  compressed format (bzip2, xz, zstd, 7z, rar); the scan reports it as unscannable and fails.
- `CLEANUP.tsv` (or `CLEANUP-aN.tsv` for a later attempt) has no `FAIL`.
- `raw/effective-config-findings.txt` says `none`, the K04 summary has no `#pairing` row `FAIL`, and
  `raw/key-pairing-citations.tsv` has no `NOT FOUND`.
- Platform accepted MDM's module registration, and at least one MDM audit append returned 2xx. MDM uses both lane keys,
  and K04 alone cannot prove a live call.
- `raw/netcheck-aN.tsv` shows `0` connections to 27017 for every process.
- `SOURCE-BINARY-PROCESS.tsv` shows `match` and `listening` for every component, `raw/processes.tsv` lists only
  `127.0.0.1` listening addresses, and `raw/served-overrides.tsv` matches.
- `COMMANDS.tsv` has one row per command and no multi-line rows.

If a phase fails, `up` has already run the abort path. Fix the input or the environment and start again from `up`. The
next attempt gets new `-aN` file names.
Do not edit evidence by hand.

## Never

- Use `run_all.sh`, `kill -9` by port or `killall dotnet` in a lane. Other lanes run beside you.
- Point anything at 27017, or edit the repository's `ocelot.json`, `appsettings*.json` or product code to make a run work.
- Write a secret to any file, including a `secrets/` folder, an env file or a harness config. Archive a token, cookie,
  password, password hash or signing secret, or log in as a seeded user with the shared seed password.
- Capture a PNG through data-URL/base64 extraction, CDP, a tunnel or unapproved native capture (see KIT-SPEC §5).
