#!/usr/bin/env bash
# Q131a — prints the lane-local test MongoDB variables for one evidence-kit slot. Fail-closed.
#
#   eval "$(scripts/test-env/mvp6-test-mongo-env.sh --slot 7 --rs rsq131s7)"
#   eval "$(scripts/test-env/mvp6-test-mongo-env.sh --slot 7 --rs rsq131s7 --all-supplychain)"
#
# The Mongo port follows the evidence-kit rule 30994 + 1000*slot (slots 1..9; k03_ports.py), so two sessions on
# different slots never share a MongoDB. The lane starts that mongod itself (single-member replica set, loopback,
# enableTestCommands=1 for the CapacityPlans fail-point tests). This script only prints `export` lines: it starts
# nothing, connects to nothing and writes nothing. Loopback only, no credentials.
# On any error it prints nothing to stdout and exits non-zero, so `eval` sets no variable and the tests fail closed.
set -euo pipefail

usage() {
  echo "usage: $0 --slot 1..9 --rs <replica-set-name> [--port <mongo-port>] [--all-supplychain]" >&2
  exit 2
}

slot="${EK_SLOT:-}"
rs=""
port=""
all_supplychain=0
while [ $# -gt 0 ]; do
  case "$1" in
    --slot) [ $# -ge 2 ] || usage; slot="$2"; shift 2 ;;
    --rs) [ $# -ge 2 ] || usage; rs="$2"; shift 2 ;;
    --port) [ $# -ge 2 ] || usage; port="$2"; shift 2 ;;
    --all-supplychain) all_supplychain=1; shift ;;
    *) usage ;;
  esac
done

case "$slot" in
  [1-9]) ;;
  *) echo "mvp6-test-mongo-env: --slot (or EK_SLOT) must be 1..9" >&2; exit 2 ;;
esac
if ! printf '%s' "$rs" | grep -Eq '^[A-Za-z0-9_-]{1,64}$'; then
  echo "mvp6-test-mongo-env: --rs must name the lane's replica set ([A-Za-z0-9_-]{1,64})" >&2
  exit 2
fi
if [ -z "$port" ]; then
  port=$((30994 + 1000 * slot))
fi
if ! printf '%s' "$port" | grep -Eq '^[0-9]{4,5}$'; then
  echo "mvp6-test-mongo-env: --port must be numeric" >&2
  exit 2
fi
if [ "$port" -ge 27017 ] && [ "$port" -le 27021 ]; then
  echo "mvp6-test-mongo-env: refusing the operational MongoDB band 27017-27021" >&2
  exit 2
fi
if [ "$port" -ge 49152 ]; then
  echo "mvp6-test-mongo-env: refusing the OS ephemeral range (>= 49152)" >&2
  exit 2
fi

uri="mongodb://127.0.0.1:${port}/?replicaSet=${rs}&serverSelectionTimeoutMS=5000"

printf 'export DITEN_PLATFORM_TEST_MONGO_URI=%q\n' "$uri"
printf 'export MVP6_MOD0192_MONGO_URI=%q\n' "$uri"
if [ "$all_supplychain" -eq 1 ]; then
  # The existing SupplyChain suite variables, all on the same lane mongod (as in Q103/Q119/Q121c).
  for name in MOD0183_TEST_MONGO MOD0184_TEST_MONGO MOD0185_TEST_MONGO MVP6_MOD0190_MONGO_URI RETURNS_MONGO_URI CLAIMS_TEST_MONGO; do
    printf 'export %s=%q\n' "$name" "$uri"
  done
fi
