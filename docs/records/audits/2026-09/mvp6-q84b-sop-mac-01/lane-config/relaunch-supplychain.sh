#!/usr/bin/env bash
# Q84b lane deviation D-2: S&OP demand fixtures load only in environment "Testing" (Program.cs:78) and must carry the lane
# legal-entity id, which exists only after the org fixture. So: add the fixture rows to overrides.tsv (no secret), re-run
# K04 render + check exactly as run-kit.sh `up` does, stop ONLY this lane's SupplyChain PID (ownership re-checked), relaunch it
# through the supervisor (same launch path as `up`). No code, no appsettings file, no repo file is changed.
# usage: relaunch-supplychain.sh <legalEntityIdA>
set -u
LE=${1:?legal entity id}
set -a; . /Users/natig/mvp6-env/q84b/lane/lane.env; set +a
K=/Users/natig/Projects/ERP-vNext-recovery/scripts/evidence-kit; . "$K/ek_lib.sh"; ek_require_env
ROTATE=${EK_ROTATE}
step() { local ph=$1 d=$2; shift 2; [ "${1:-}" = "--" ] && shift; echo "== $ph: $d"; ek_run "$ph" "$ph" "$d" -- "$@" || ek_die "phase failed: $ph ($d)"; }
grep -q 'Sandop__DemandFixtures__0__' "$EK_OVERRIDES" && ek_die "fixture rows already in overrides.tsv"
printf 'supplychain\tSandop__DemandFixtures__0__TenantId\t97c59330-dbc4-4665-b29c-0c26dbb5cc93\nsupplychain\tSandop__DemandFixtures__0__LegalEntityId\t%s\nsupplychain\tSandop__DemandFixtures__0__PlanId\tDP-Q84B-01\nsupplychain\tSandop__DemandFixtures__0__Version\t1\nsupplychain\tSandop__DemandFixtures__0__Checksum\tsha256:q84b-demand-01\nsupplychain\tSandop__DemandFixtures__0__Published\ttrue\n' "$LE" >> "$EK_OVERRIDES"
common=(--map "$K/service-map.tsv" --services "$EK_SERVICES" --work "$EK_WORK" --evidence "$EK_EVIDENCE"
        --suffix "$EK_DB_SUFFIX" --env-name "$EK_ASPNET_ENV" --overrides "${EK_OVERRIDES:-/dev/null}" --rotate "$ROTATE")
step K04 "k04_config.py render (Q84b D-2: S&OP demand fixture rows)" -- python3 "$K/k04_config.py" render "${common[@]}"
step K04 "k04_config.py check (Q84b D-2)" -- python3 "$K/k04_config.py" check "${common[@]}"
row=$(awk -F'\t' '$1=="supplychain"' "$EK_WORK/pids.tsv" | tail -1); pid=$(echo "$row" | cut -f2); port=$(echo "$row" | cut -f3); marker=$(echo "$row" | cut -f4)
[ -n "$pid" ] || ek_die "no supplychain pid"
ps -o command= -p "$pid" | grep -qF "$marker" || ek_die "pid $pid does not belong to the lane"
kill -TERM "$pid"; for _ in $(seq 1 40); do kill -0 "$pid" 2>/dev/null || break; sleep 0.5; done
kill -0 "$pid" 2>/dev/null && ek_die "supplychain $pid did not stop"
echo "stopped supplychain pid $pid (port $port)"
for _ in $(seq 1 20); do [ -z "$(ek_listener_pid "$port")" ] && break; sleep 0.5; done
step K06 "launch supplychain via supervisor (Q84b D-2 relaunch)" -- bash "$K/k06_build_launch.sh" launch supplychain
step K06 "netcheck after relaunch" -- bash "$K/k06_build_launch.sh" netcheck
