# WP-0173-OPS1 — incident: first D3 what-if ran real commands

**What happened (2026-10-02 09:57:50–10:00, local time).** The first D3 attempt put the shim directory on `PATH` as a
Windows path (`C:/Users/...`). The `:` in `C:` split the entry, so the shims were never found and copies of
`run_all.sh`, `run_watch.sh` and `run-diten.sh` (with `trap 'kill 0'` removed) ran against real commands under
`timeout 60`:

- `run_all.sh` copy: real `dotnet build` of Auth/DevEnablement/Platform/Mdm/Inventory/Hcm/Gateway/Web (`-v q`);
  stopped by the timeout (exit 124) before reaching `dotnet run`.
- `run_watch.sh` copy: real `dotnet watch run --urls http://0.0.0.0:<port>` for the eight services and a real Prism on
  4010. All failed to bind or were stopped by the timeout; Prism exited `fatal` (4010 already held by the CT's Prism).
- `run-diten.sh` copy: real `dotnet build frontend/Diten.Web` started; the task was stopped (TaskStop) and the
  remaining bash/dotnet/timeout processes (PIDs 31496, 732, 7968, 11996) were already gone when checked.

**What it did not do.** `lsof` and `killall` do not exist in this Git Bash, so no port clean-up and no
`killall -9 dotnet` ran; `xargs kill -9` received no PIDs. No process was killed by the scripts.

**Side effect on the user's fleet.** The rebuilt outputs triggered hot-reload restarts of the user's own
`watch-diten.ps1` windows (dotnet-watch hosts started 09:41–09:50; `DOTNET_WATCH_ITERATION` 2–4). Measured at
10:01: 4010 (node, 09:50, untouched), 5000, 5001, 5056–5061, 5066 all listening again, owned by children of the user's
watch hosts. `/health`: 5066 → 200, 5000 → 200, 5057 → 503 with `rabbitmq: RabbitMQ TCP endpoint is not reachable`
(`masstransit-bus` not started) — whether 5057 was 503 before the restarts was not measured.

**Fix.** The re-run (the D3 logs in this folder) uses the POSIX shim path and a first line in each copy that aborts
(exit 99) unless every external command (`lsof killall kill dotnet node env sleep osascript`) resolves to a shim; the
guard was self-tested without shims (abort, exit 99) before the run.
