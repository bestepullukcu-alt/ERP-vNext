# MVP6 evidence kit v1.1 — how to run a lane

> **CANDIDATE — NOT ACTIVE.** Proposed location: `docs/guides/operations/mvp6-evidence-kit-v1.0.md`. The file name stays
> v1.0 so the process-guide §5 line approved in A1 still points to it; the content is v1.1. Proposed kit location:
> `scripts/evidence-kit/`. Adoption needs the owner decision in
> `docs/roadmap/plans/mvp6-evidence-kit-proposal-02/ADOPTION-DECISION-v1.1.md`. Until then, nothing here is a rule.

Use the kit for every MVP6 runtime lane that needs real Auth, Gateway, UI and an isolated DB. It covers the environment half of the
process §5 evidence checklist (`docs/guides/operations/mvp6-development-process-v1.0.md`). Acceptance probes, the SOP-22
report and EVIDENCE-REUSE rows stay with the work package.

## Before you start

1. Pick a free **slot** 1–9. The slot is your whole port block, so two lanes never share one. Write it in the CT queue row.
2. Pick one **DB suffix**, e.g. `ShipmentA12Ver01`. DB-010 means one fixed suffix per lane and never a GUID.
3. Copy `scripts/evidence-kit/lane.env.example` to a workspace **outside** the repository and fill it in. `EK_SOCK` must be
   outside `EK_WORK`.
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

`up` starts the **lane supervisor** (`k00_supervisor.py`). It generates every lane secret once, in memory: the JWT and
MFA keys, one shared internal key for Auth↔Platform↔SupplyChain↔Web, one Platform↔MDM key, the service-identity secret,
and one password per actor. It launches the services with those values in their environment. Config files and env files hold
only placeholders `@@LANE:<group>@@`. K04 refuses to continue unless every pair that must match does match (`#pairing` rows).

Then do the lane's work while everything is running:
- **Commands:** run each command through `ek_run` or `k00_ctl.py "$EK_SOCK" run <label> -- <cmd>`. Each produces exactly one
  `COMMANDS.tsv` row with the real exit code. Never add rows by hand, and never put `$(date)` next to an exit-code echo.
- **HTTP/browser acceptance** goes through `http://127.0.0.1:<web port>` only. For a browser login, `k00_ctl.py "$EK_SOCK" reveal
  <actor>` prints the actor's password to a terminal only (it refuses a pipe or file). The password is valid until
  `seal`.
- **DB before/after:** take a snapshot around every mutation, every **setup step** and every negative case with
  `k10_snap.sh <label> '<spec>'`, which writes a new `-aN` file each time and never overwrites one. Then run
  `k10_db_diff.py before after --expect … --totals …`. Collections you do not list must stay unchanged unless you pass
  `--allow-unlisted "<reason>"`. A reused file pair is refused.
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

- `SECRET-SCAN-FINAL-aN.txt` ends in `result PASS`, was written after `ARTIFACTS.sha256`, and records that file's hash.
- `CLEANUP.tsv` (or `CLEANUP-aN.tsv` for a later attempt) has no `FAIL`.
- `raw/effective-config-findings.txt` says `none`, and the K04 summary has no `#pairing` row `FAIL`.
- `raw/netcheck-aN.tsv` shows `0` connections to 27017 for every process.
- `SOURCE-BINARY-PROCESS.tsv` shows `match` and `listening` for every component, and `raw/served-overrides.tsv` matches.
- `COMMANDS.tsv` has one row per command and no multi-line rows.

If a phase fails, fix the input or the environment and start again from `up`. The next attempt gets new `-aN` file names.
Do not edit evidence by hand.

## Never

- Use `run_all.sh`, `kill -9` by port or `killall dotnet` in a lane. Other lanes run beside you.
- Point anything at 27017, or edit the repository's `ocelot.json`, `appsettings*.json` or product code to make a run work.
- Write a secret to any file, including a `secrets/` folder, an env file or a harness config. Archive a token, cookie,
  password, password hash or signing secret, or log in as a seeded user with the shared seed password.
- Capture a PNG through data-URL/base64 extraction, CDP, a tunnel or unapproved native capture (see KIT-SPEC §5).
