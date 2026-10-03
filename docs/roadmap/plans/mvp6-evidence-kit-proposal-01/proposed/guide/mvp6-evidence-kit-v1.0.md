# MVP6 evidence kit v1.0 — how to run a lane

> **CANDIDATE — NOT ACTIVE.** Proposed location: `docs/guides/operations/mvp6-evidence-kit-v1.0.md`.
> Proposed kit location: `scripts/evidence-kit/`. Adoption needs the owner decision in
> `docs/roadmap/plans/mvp6-evidence-kit-proposal-01/ADOPTION-DECISION.md`. Until then, nothing here is a rule.

Use it for every MVP6 runtime lane that needs real Auth, Gateway, UI and an isolated DB. It covers the environment half of the
process §5 evidence checklist (`docs/guides/operations/mvp6-development-process-v1.0.md`). Acceptance probes, the SOP-22 report and EVIDENCE-REUSE rows
stay with the work package.

## Before you start

1. Pick a free **slot** 1–9. The slot is your whole port block, so two lanes never share one. Write it in the CT queue row.
2. Pick one **DB suffix**, e.g. `ShipmentA12Ver01`. DB-010 means one fixed suffix per lane and never a GUID.
3. Copy `scripts/evidence-kit/lane.env.example` to a workspace **outside** the repository and fill it in.
4. Fill three small files next to it, starting from `templates/`:
   - `overlays.tsv`: the sealed source inputs in order, with their SHA-256.
   - `actors.tsv`: one row per browser/HTTP identity, with the tenant and expected legal entity.
   - `overrides.tsv`: your explicit value for each security-relevant switch (e.g. `TenantResolution__DevBypassEnabled`).
     The kit will not guess them.

## Run

```bash
set -a; . ./lane.env; set +a
scripts/evidence-kit/run-kit.sh up        # K01 source … K07 identities; stops at the first failure
```
Then do the lane's work while everything is running:
- **HTTP/browser acceptance** through `http://127.0.0.1:<web port>` only. For a browser login, get the actor's password with
  `k07_identity.py reveal --work "$EK_WORK" --actor <label>`. It is valid only until cleanup.
- **DB before/after** around every mutation and every negative case: `k10_db_snapshot.js` then `k10_db_diff.py`.
- **Binding** before you stop anything: `k09_binding.py --served <asset=source path> --browser-url <urls you used>`.
- Pass every raw capture that might hold headers or bodies through `k08_redact_scan.py redact` before it lands in `evidence/`.

```bash
EK_BROWSER_CLOSED=PASS scripts/evidence-kit/run-kit.sh down   # netcheck → secret scan → cleanup → rescan
```

## What must be true at hand-off

- `SECRET-SCAN.txt` and `SECRET-RESCAN-AFTER-CLEANUP.txt` both end in `result PASS`.
- `CLEANUP.tsv` has no `FAIL`.
- `raw/effective-config-findings.txt` says `none`.
- `raw/netcheck.tsv` shows `0` connections to 27017 for every process.
- `SOURCE-BINARY-PROCESS.tsv` shows `match` and `listening` for every component.

If a phase fails, fix the input or the environment and start again from `up`. Do not edit evidence by hand.

## Never

- Use `run_all.sh`, `kill -9` by port or `killall dotnet` in a lane. Other lanes run beside you.
- Point anything at 27017, or edit the repository's `ocelot.json`, `appsettings*.json` or product code to make a run work.
- Archive a token, cookie, password, password hash or signing secret, or log in as a seeded user with the shared seed password.
- Capture a PNG through data-URL/base64 extraction, CDP, a tunnel or unapproved native capture (see KIT-SPEC §5).
