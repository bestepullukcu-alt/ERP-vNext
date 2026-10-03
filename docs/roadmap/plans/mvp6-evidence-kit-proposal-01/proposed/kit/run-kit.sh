#!/usr/bin/env bash
# CANDIDATE — NOT ACTIVE. MVP6 evidence kit driver.
#   run-kit.sh up     K01 source → K03 ports → K02 runtime → K06 build → K04b gateway → K04 config → K05 mongo
#                     → K06 launch (map order) → K06 netcheck → K07 identities
#   (lane work: HTTP/browser acceptance, K10 snapshots around every mutation and negative case, K09 binding while running)
#   run-kit.sh down   K06 netcheck (again) → K08 scan → K11 cleanup → K08 pattern-only rescan (secrets now gone)
# Every phase stops the run on failure; nothing is retried silently. Requires lane.env (see lane.env.example).
set -u
K="$(cd "$(dirname "$0")" && pwd)"
. "$K/ek_lib.sh"
ek_require_env
step() { echo "== $*"; "$@" || ek_die "phase failed: $*"; }

case "${1:-}" in
up)
  step python3 "$K/k01_source.py" --repo "$EK_REPO" --head "$EK_EXPECTED_HEAD" --branch "$EK_EXPECTED_BRANCH" \
       --overlays "$EK_OVERLAYS" --work "$EK_WORK" --evidence "$EK_EVIDENCE"
  step python3 "$K/k03_ports.py" --slot "$EK_SLOT" --services "$EK_SERVICES" --work "$EK_WORK" --evidence "$EK_EVIDENCE"
  step bash "$K/k02_runtime.sh"
  for s in $EK_SERVICES; do step bash "$K/k06_build_launch.sh" build "$s"; done
  case " $EK_SERVICES " in *" gateway "*) step python3 "$K/k04b_gateway_routes.py" --work "$EK_WORK" --evidence "$EK_EVIDENCE" ;; esac
  common=(--map "$K/service-map.tsv" --services "$EK_SERVICES" --work "$EK_WORK" --evidence "$EK_EVIDENCE"
          --suffix "$EK_DB_SUFFIX" --env-name "$EK_ASPNET_ENV" --overrides "${EK_OVERRIDES:-/dev/null}")
  step python3 "$K/k04_config.py" secrets "${common[@]}"
  step python3 "$K/k04_config.py" render "${common[@]}"
  step python3 "$K/k04_config.py" check "${common[@]}"
  step bash "$K/k05_mongo.sh"
  for s in $EK_SERVICES; do step bash "$K/k06_build_launch.sh" launch "$s"; done
  step bash "$K/k06_build_launch.sh" netcheck
  step python3 "$K/k07_identity.py" rotate --work "$EK_WORK" --evidence "$EK_EVIDENCE" --suffix "$EK_DB_SUFFIX" --actors "$EK_ACTORS"
  echo "UP: lane running. Do the lane work, K10 snapshots and K09 binding, then: run-kit.sh down"
  ;;
down)
  step bash "$K/k06_build_launch.sh" netcheck
  step python3 "$K/k08_redact_scan.py" scan "$EK_EVIDENCE" --work "$EK_WORK"
  bash "$K/k11_cleanup.sh"; rc=$?
  step python3 "$K/k08_redact_scan.py" scan "$EK_EVIDENCE" --out SECRET-RESCAN-AFTER-CLEANUP.txt
  exit $rc
  ;;
*) echo "usage: run-kit.sh up|down"; exit 2 ;;
esac
