#!/usr/bin/env bash
# Q84b cleanup: K11 steps 2-3 (stop ONLY this lane's PIDs after an ownership re-check, reverse start order, mongod via
# shutdownServer; then every lane port must have no listener) and step 5 (repo no-change). K11 step 4 (rm -rf of the
# workspace) is NOT run — owner rule "no rm, use new folders"; ~/mvp6-env/q88b stays (prompt step 8 allows it).
set -u
set -a; . /Users/natig/mvp6-env/q88b/lane/lane.env; set +a
K=/Users/natig/Projects/ERP-vNext-recovery/scripts/evidence-kit; . "$K/ek_lib.sh"; ek_require_env
OUT=$(ek_unique "$EK_EVIDENCE" CLEANUP tsv); printf 'resource\tcheck\tresult\tutc\n' > "$OUT"
row() { printf '%s\t%s\t%s\t%s\n' "$1" "$2" "$3" "$(ek_now)" >> "$OUT"; }
awk '{a[NR]=$0} END{for(i=NR;i>0;i--)print a[i]}' "$EK_WORK/pids.tsv" | while IFS=$'\t' read -r role pid port marker; do
  st=$(ps -o stat= -p "$pid" 2>/dev/null | tr -d ' ')
  if [ -z "$st" ] || [ "${st#Z}" != "$st" ]; then row "$role:$pid" process "already-exited${st:+ (zombie, not reaped by supervisor)}"; continue; fi
  if ! ps -o command= -p "$pid" | grep -qF "$marker"; then row "$role:$pid" ownership "SKIPPED: pid no longer belongs to lane"; continue; fi
  if [ "$role" = mongod ]; then mongosh --quiet "mongodb://127.0.0.1:$port/?directConnection=true" --eval 'db.getSiblingDB("admin").shutdownServer()' >/dev/null 2>&1
  else kill -TERM "$pid" 2>/dev/null; fi
  for _ in $(seq 1 60); do s=$(ps -o stat= -p "$pid" 2>/dev/null | tr -d ' '); { [ -z "$s" ] || [ "${s#Z}" != "$s" ]; } && break; sleep 0.5; done
  s=$(ps -o stat= -p "$pid" 2>/dev/null | tr -d ' ')
  if [ -n "$s" ] && [ "${s#Z}" = "$s" ]; then kill -KILL "$pid" 2>/dev/null; row "$role:$pid" stop "KILLED-after-timeout"; else row "$role:$pid" stop "stopped${s:+ (zombie until supervisor exits)}"; fi
done
sleep 2
python3 -c 'import json,sys;[print(k,v) for k,v in json.load(open(sys.argv[1]))["ports"].items()]' "$EK_WORK/ports.json" | while read -r role port; do
  if [ -z "$(ek_listener_pid "$port")" ]; then row "port:$role:$port" "no listener" PASS; else row "port:$role:$port" "no listener" FAIL; fi
done
row workspace "not removed (owner rule no-rm; allowed path ~/mvp6-env/q88b)" INFO
head_now=$(ek_git "$EK_REPO" rev-parse HEAD); [ "$head_now" = "$EK_EXPECTED_HEAD" ] && row repo-head "$head_now" PASS || row repo-head "$head_now" FAIL
cat "$OUT"
