#!/usr/bin/env bash
# CANDIDATE — NOT ACTIVE. MVP6 evidence kit phase K11: cleanup and cleanup verification.
# Stops ONLY the PIDs this lane started (W/pids.tsv), and only after re-checking that each PID still belongs to the
# lane (its command line contains the lane workspace path) — never `kill -9` by port and never `killall dotnet`
# (the repo's run_all.sh / dev-runbook habit), which would hit other lanes and the owner's own stack.
# Then removes the lane workspace (source, builds, env files, secrets, Mongo dbpath) and verifies each step.
# Run K08 scan BEFORE this phase: the exact-secret scan needs W/secrets, which this phase destroys.
set -u
. "$(dirname "$0")/ek_lib.sh"
ek_require_env
EV="$EK_EVIDENCE"; OUT="$EV/CLEANUP.tsv"
printf 'resource\tcheck\tresult\tutc\n' > "$OUT"
row() { printf '%s\t%s\t%s\t%s\n' "$1" "$2" "$3" "$(ek_now)" >> "$OUT"; }
fail=0

# Listener inventory as it was just before cleanup (evidence of what was running).
[ -f "$EK_WORK/pids.tsv" ] && awk -F'\t' '{print $3}' "$EK_WORK/pids.tsv" | sort -u | while read -r p; do
  lsof -nP -iTCP:"$p" -sTCP:LISTEN 2>/dev/null
done > "$EV/raw/listeners-before-cleanup.txt"

# 1. Browser: the kit cannot close browser tabs/profiles; the operator records it (see templates/CLEANUP.tsv).
row browser-tabs-and-profiles "closed by operator (manual)" "${EK_BROWSER_CLOSED:-NOT-RECORDED}"

# 2. .NET services, in reverse start order; mongod last via shutdownServer.
if [ -f "$EK_WORK/pids.tsv" ]; then
  tac_() { awk '{a[NR]=$0} END{for(i=NR;i>0;i--)print a[i]}' "$1"; }
  tac_ "$EK_WORK/pids.tsv" | while IFS=$'\t' read -r role pid port marker; do
    if ! kill -0 "$pid" 2>/dev/null; then row "$role:$pid" "process" "already-exited"; continue; fi
    if ! ps -o command= -p "$pid" | grep -qF "$marker"; then
      row "$role:$pid" "ownership" "SKIPPED: pid no longer belongs to lane"; continue
    fi
    if [ "$role" = mongod ]; then
      mongosh --quiet "mongodb://127.0.0.1:$port/?directConnection=true" --eval 'db.getSiblingDB("admin").shutdownServer()' >/dev/null 2>&1
    else
      kill -TERM "$pid" 2>/dev/null
    fi
    for _ in $(seq 1 40); do kill -0 "$pid" 2>/dev/null || break; sleep 0.5; done
    if kill -0 "$pid" 2>/dev/null; then kill -KILL "$pid" 2>/dev/null; row "$role:$pid" "stop" "KILLED-after-timeout"
    else row "$role:$pid" "stop" "stopped"; fi
  done
fi

# 3. Ports: every lane port (services, sink, mongo) must have no listener.
if [ -f "$EK_WORK/ports.json" ]; then
  python3 -c 'import json,sys;[print(k,v) for k,v in json.load(open(sys.argv[1]))["ports"].items()]' "$EK_WORK/ports.json" |
  while read -r role port; do
    if [ -z "$(ek_listener_pid "$port")" ]; then row "port:$role:$port" "no listener" PASS; else row "port:$role:$port" "no listener" FAIL; fi
  done
  cp "$EK_WORK/ports.json" "$EV/raw/ports.json"
fi

# 4. Workspace incl. secrets, env files, source, builds, Mongo data. EK_WORK is outside the repo (ek_require_env).
for sub in secrets env mongo source gateway-runtime logs; do
  [ -e "$EK_WORK/$sub" ] && rm -rf "${EK_WORK:?}/$sub"
  if [ -e "$EK_WORK/$sub" ]; then row "workspace:$sub" "removed" FAIL; else row "workspace:$sub" "removed" PASS; fi
done
rm -rf "${EK_WORK:?}"
if [ -e "$EK_WORK" ]; then row workspace "removed" FAIL; else row workspace "removed" PASS; fi

# 5. Repository no-change: HEAD/branch unchanged and no status entry outside the lane evidence directory.
head_now=$(git -C "$EK_REPO" rev-parse HEAD); br_now=$(git -C "$EK_REPO" rev-parse --abbrev-ref HEAD)
[ "$head_now" = "$EK_EXPECTED_HEAD" ] && row repo-head "$head_now" PASS || row repo-head "$head_now" FAIL
[ "$br_now" = "${EK_EXPECTED_BRANCH:-$br_now}" ] && row repo-branch "$br_now" PASS || row repo-branch "$br_now" FAIL
if [ -f "$EV/raw/repo-status-before.txt" ]; then
  git -C "$EK_REPO" status --porcelain > "$EV/raw/repo-status-after.txt"
  rel=${EV#"$EK_REPO"/}
  extra=$(diff "$EV/raw/repo-status-before.txt" "$EV/raw/repo-status-after.txt" | grep '^>' | grep -vF " $rel" | grep -vF " ${rel%%/evidence*}" || true)
  [ -z "$extra" ] && row repo-status "no new entries outside evidence" PASS || row repo-status "new entries: $(echo "$extra" | wc -l | tr -d ' ')" FAIL
fi

grep -q $'\tFAIL\t' "$OUT" && fail=1
echo "K11 $([ $fail = 0 ] && echo PASS || echo FAIL): see CLEANUP.tsv"
exit $fail
