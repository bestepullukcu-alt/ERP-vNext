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

# Q480 (owner decision, 2026-10-09): 15000 ms, raised from 5000.
#
# WHY, AND WHAT IT DOES NOT FIX. Loads.LoadAtomicityTests.UnknownCommitResultRetriesAndCommitsExactlyOnce
# injects error 91 (ShutdownInProgress) into commitTransaction. Q214 measured what follows: the driver reads 91
# as "this server is going away", marks the lane set's ONLY server Unknown, and learns otherwise only when its
# monitor's hello long-poll returns — up to 10 s. The product then retries, and the retry must first select a
# server. At 5000 the selection gave up inside that window and the product answered 503, so the test passed
# only when the fault happened to land in the second half of the monitor cycle: green in Q266 and Q335, red
# when run alone. 15000 outlasts the poll, so the retry commits and the outcome stops depending on a clock.
#
# ⚠ This is NOT Q214's recommended fix, and it was taken knowing so. Q214 R1-R3 — inject error 8 with the
# UnknownTransactionCommitResult label, scope the fail point with appName, assert how many times it fired, as
# CapacityPlans/CapacityAtomicityTests.cs:108-127 already does — addresses the 91 problem itself and takes
# about 80 s off the suite. Raising this value instead leaves error 91's 10 s stall in place and buys the
# green with waiting, which is why Q214 lists it under "not recommended as the fix".
#
# The cost is bounded and is a wait, not a wrong answer: a caller whose lane mongod is unreachable now takes
# up to 15 s instead of 5 s to fail closed. A port that is closed outright still refuses in about a second —
# the timeout only governs a host that answers while no replica-set member does.
uri="mongodb://127.0.0.1:${port}/?replicaSet=${rs}&serverSelectionTimeoutMS=15000"

printf 'export DITEN_PLATFORM_TEST_MONGO_URI=%q\n' "$uri"
printf 'export MVP6_MOD0192_MONGO_URI=%q\n' "$uri"
if [ "$all_supplychain" -eq 1 ]; then
  # The existing SupplyChain suite variables, all on the same lane mongod (as in Q103/Q119/Q121c).
  for name in MOD0183_TEST_MONGO MOD0184_TEST_MONGO MOD0185_TEST_MONGO MVP6_MOD0190_MONGO_URI RETURNS_MONGO_URI CLAIMS_TEST_MONGO; do
    printf 'export %s=%q\n' "$name" "$uri"
  done
fi
