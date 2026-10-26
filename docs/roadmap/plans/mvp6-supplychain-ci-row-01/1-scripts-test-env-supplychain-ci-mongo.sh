#!/usr/bin/env bash
set -euo pipefail

# WP-LANE-MAIN-SYNC-05 — the owned test replica set for the supplychain-tests row of service-tests.yml.
#
# Modelled on scripts/test-env/mod0188-ci-mongo.sh, which main added for the planning-tests row on 2026-10-16.
#
# KNOWN TENSION, recorded rather than hidden (BL-650): CT decision D says "no row starts a job-local mongod; every
# service test owns its mongod (DITEN_TEST_MONGOD)", and CiServiceTestsGuardTests enforces it by reading the WORKFLOW
# text for --replSet / --dbpath / mongodb://. A mongod started from a script, as planning-tests does and as this does,
# passes that text check while doing the thing the decision forbids. The honest alternative - a shared fixture that
# starts its own replica set - would reverse Q131a's deliberate "there is no fallback port, a missing URI fails closed"
# across seven call sites plus three child-process tests that inherit the variable from their parent. That is its own
# work package. Owner decision (2026-10-16): follow the planning-tests precedent, record the tension.
#
# What the SupplyChain suite needs from a mongod, and why a plain one will not do:
#   - a single-member REPLICA SET: the suites use transactions, and ShipmentSchema refuses to start without
#     "Mongo transaction support (replica set or mongos)";
#   - enableTestCommands=1: the CapacityPlans and SandopPlans fail-point tests configure fail points;
#   - loopback only, and never the operational band 27017-27021 - CapacityTestMongo rejects those ports itself.
#
# It writes no credential: loopback, no auth, and it dies with the job.

state="$RUNNER_TEMP/supplychain-ci-mongo-state"
rs="rs-supplychain"

# The seven fail-closed variables the suite reads (measured: GetEnvironmentVariable call sites in
# services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests), plus the composition-root guard's own.
# Q492: SUPPLYCHAIN_COMPOSITION_GUARD_MONGO_URI is included because without it CompositionRootGuardTests refuses to run
# ("this guard must not be skipped") - it builds the PRODUCTION container and asserts every controller resolves.
variables=(
  MOD0183_TEST_MONGO
  MOD0184_TEST_MONGO
  MOD0185_TEST_MONGO
  MVP6_MOD0190_MONGO_URI
  MVP6_MOD0192_MONGO_URI
  RETURNS_MONGO_URI
  CLAIMS_TEST_MONGO
  SUPPLYCHAIN_COMPOSITION_GUARD_MONGO_URI
)

stop_owned() {
  if [[ ! -f "$state" ]]; then
    return
  fi
  dir=$(cat "$state")
  case "$dir" in
    "$RUNNER_TEMP"/supplychain-ci-mongo-*) ;;
    *) echo "Unexpected SupplyChain test directory" >&2; exit 1 ;;
  esac
  if [[ -f "$dir/mongod.pid" ]]; then
    pid=$(cat "$dir/mongod.pid")
    if [[ "$pid" =~ ^[0-9]+$ ]] && [[ -e "/proc/$pid/cmdline" ]]; then
      if ! tr '\0' ' ' < "/proc/$pid/cmdline" | grep -Fq -- "--dbpath $dir/data"; then
        echo "PID $pid is not the SupplyChain test mongod" >&2
        exit 1
      fi
      kill "$pid"
      for _ in $(seq 1 30); do
        [[ ! -e "/proc/$pid/cmdline" ]] && break
        sleep 1
      done
      [[ ! -e "/proc/$pid/cmdline" ]] || { echo "Owned mongod did not stop" >&2; exit 1; }
    fi
  fi
  rm -rf -- "$dir"
  rm -f -- "$state"
}

case "${1:-}" in
  stop) stop_owned ;;
  start)
    [[ ! -e "$state" ]] || { echo "SupplyChain test Mongo already started" >&2; exit 1; }
    dir=$(mktemp -d "$RUNNER_TEMP/supplychain-ci-mongo-XXXXXXXX")
    printf '%s\n' "$dir" > "$state"
    mkdir "$dir/data"
    # Deliberately not 31994-34994: those are mod0188-ci-mongo.sh's candidates. Never the operational band, and
    # below the OS ephemeral range - the same two rules scripts/test-env/mvp6-test-mongo-env.sh enforces.
    port=''
    for candidate in 35994 36994 37994 38994; do
      if python3 - "$candidate" <<'PY'
import socket
import sys
with socket.socket() as sock:
    sock.bind(("127.0.0.1", int(sys.argv[1])))
PY
      then
        port="$candidate"
        break
      fi
    done
    [[ -n "$port" ]] || { echo "No free SupplyChain test port" >&2; exit 1; }
    "$DITEN_TEST_MONGOD" --replSet "$rs" --port "$port" --bind_ip 127.0.0.1 \
      --setParameter enableTestCommands=1 \
      --dbpath "$dir/data" --logpath "$dir/mongod.log" --pidfilepath "$dir/mongod.pid" --fork
    python3 -m venv "$dir/venv"
    "$dir/venv/bin/pip" install --disable-pip-version-check 'pymongo==4.10.1'
    "$dir/venv/bin/python" - "$port" "$rs" <<'PY'
import sys
import time
from pymongo import MongoClient

port, rs = int(sys.argv[1]), sys.argv[2]
client = MongoClient(f"mongodb://127.0.0.1:{port}/?directConnection=true&serverSelectionTimeoutMS=1000")
deadline = time.monotonic() + 30
while True:
    try:
        client.admin.command({"replSetInitiate": {"_id": rs, "members": [{"_id": 0, "host": f"127.0.0.1:{port}"}]}})
        break
    except Exception:
        if time.monotonic() >= deadline:
            raise
        time.sleep(0.5)
while True:
    try:
        if client.admin.command("hello").get("isWritablePrimary"):
            break
    except Exception:
        pass
    if time.monotonic() >= deadline:
        raise TimeoutError("SupplyChain test replica set did not become PRIMARY")
    time.sleep(0.5)
PY
    # Q480 (owner decision 2026-10-09): 15000 ms, not 5000 - error 91 injected into commitTransaction marks the lane
    # set's only server Unknown until the monitor's hello long-poll returns, up to 10 s. The same value the lane script
    # uses, for the same reason.
    uri="mongodb://127.0.0.1:$port/?replicaSet=$rs&serverSelectionTimeoutMS=15000"
    for name in "${variables[@]}"; do
      echo "$name=$uri" >> "$GITHUB_ENV"
    done
    echo "[supplychain] owned test replica set ready on $port (${#variables[@]} variables exported)"
    ;;
  *) echo 'Usage: supplychain-ci-mongo.sh start|stop' >&2; exit 2 ;;
esac
