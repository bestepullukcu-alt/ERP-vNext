#!/usr/bin/env bash
# CANDIDATE — NOT ACTIVE. MVP6 evidence kit v1.1 driver.
#   run-kit.sh up     K01 source → K03 ports → K02 runtime → K06 build → K04b gateway → K04 shared/render/check
#                     → K05 mongo → supervisor start (secrets in memory only) → K06 launch via supervisor → netcheck
#                     → K07 identities (run by the supervisor)
#   (lane work: HTTP/browser acceptance through `k00_ctl.py $EK_SOCK run <label> -- <cmd>` so helpers get the lane
#    values in their environment; K10 snapshots with k10_snap.sh around every mutation, setup step and negative case;
#    record-served for every test-route override; K09 binding while running; dotnet tests via `k06_build_launch.sh test`)
#   run-kit.sh down   netcheck → K11 cleanup (supervisor stays up) → ARTIFACTS.sha256 → seal
#   run-kit.sh seal   final exact-value + pattern scan by the supervisor (the LAST write, G4) → supervisor shutdown
# v1.1: every phase is one COMMANDS.tsv row via ek_run (G2); the supervisor adds one row per launch/run (D4).
# Every phase stops the run on failure; nothing is retried silently. Requires lane.env (see lane.env.example).
set -u
K="$(cd "$(dirname "$0")" && pwd)"
. "$K/ek_lib.sh"
ek_require_env
# step <phase> <description> -- command...   (logged, fails the run on non-zero exit)
step() { local ph=$1 d=$2; shift 2; [ "${1:-}" = "--" ] && shift; echo "== $ph: $d"; ek_run "$ph" "$ph" "$d" -- "$@" || ek_die "phase failed: $ph ($d)"; }
ROTATE=${EK_ROTATE:-"jwt mfa auth-platform platform-mdm-active svcid"}

case "${1:-}" in
up)
  step K01 "k01_source.py (HEAD archive + overlays)" -- python3 "$K/k01_source.py" --repo "$EK_REPO" --head "$EK_EXPECTED_HEAD" \
       --branch "$EK_EXPECTED_BRANCH" --overlays "$EK_OVERLAYS" --work "$EK_WORK" --evidence "$EK_EVIDENCE"
  step K03 "k03_ports.py slot $EK_SLOT" -- python3 "$K/k03_ports.py" --slot "$EK_SLOT" --services "$EK_SERVICES" --work "$EK_WORK" --evidence "$EK_EVIDENCE"
  step K02 "k02_runtime.sh" -- bash "$K/k02_runtime.sh"
  for s in $EK_SERVICES; do step K06 "build $s" -- bash "$K/k06_build_launch.sh" build "$s"; done
  case " $EK_SERVICES " in *" gateway "*) step K04b "k04b_gateway_routes.py" -- python3 "$K/k04b_gateway_routes.py" --work "$EK_WORK" --evidence "$EK_EVIDENCE" ;; esac
  common=(--map "$K/service-map.tsv" --services "$EK_SERVICES" --work "$EK_WORK" --evidence "$EK_EVIDENCE"
          --suffix "$EK_DB_SUFFIX" --env-name "$EK_ASPNET_ENV" --overrides "${EK_OVERRIDES:-/dev/null}" --rotate "$ROTATE")
  step K04 "k04_config.py shared (issuer/audience; no secret)" -- python3 "$K/k04_config.py" shared "${common[@]}"
  step K04 "k04_config.py render (placeholders only)" -- python3 "$K/k04_config.py" render "${common[@]}"
  step K04 "k04_config.py check (incl. D2 pairing, D4 no literal secret)" -- python3 "$K/k04_config.py" check "${common[@]}"
  step K05 "k05_mongo.sh" -- bash "$K/k05_mongo.sh"
  # D4: the supervisor generates every lane value in memory; its socket is outside W.
  nohup python3 "$K/k00_supervisor.py" --work "$EK_WORK" --evidence "$EK_EVIDENCE" --map "$K/service-map.tsv" \
    --actors "$EK_ACTORS" --socket "$EK_SOCK" --rotate "$ROTATE" --dotnet "$EK_DOTNET" \
    --expected-runtime "$EK_EXPECTED_RUNTIME" > "$(ek_unique "$EK_WORK" supervisor log)" 2>&1 &
  for _ in $(seq 1 40); do ek_ctl ping >/dev/null 2>&1 && break; sleep 0.25; done
  ek_ctl ping >/dev/null 2>&1 || ek_die "supervisor did not start"
  for s in $EK_SERVICES; do step K06 "launch $s via supervisor" -- bash "$K/k06_build_launch.sh" launch "$s"; done
  step K06 "netcheck after start" -- bash "$K/k06_build_launch.sh" netcheck
  step K07 "k07_identity.py rotate under the supervisor" -- ek_ctl run k07 -- python3 "$K/k07_identity.py" rotate \
       --work "$EK_WORK" --evidence "$EK_EVIDENCE" --suffix "$EK_DB_SUFFIX" --actors "$EK_ACTORS"
  echo "UP: lane running. Do the lane work (helpers via k00_ctl.py run), K10 snapshots, K09 binding, then: run-kit.sh down"
  ;;
down)
  step K06 "netcheck before cleanup" -- bash "$K/k06_build_launch.sh" netcheck
  ek_run K11 cleanup "k11_cleanup.sh (supervisor stays up for the seal)" -- bash "$K/k11_cleanup.sh"; rc=$?
  echo "Now write the lane's SOP-22/ACCEPTANCE files, then ARTIFACTS.sha256 (all files), then: run-kit.sh seal"
  exit $rc
  ;;
seal)
  # G4: the final exact-value scan is the LAST write; it records the ARTIFACTS.sha256 hash it sealed.
  [ -f "$EK_EVIDENCE/ARTIFACTS.sha256" ] || ek_die "write ARTIFACTS.sha256 first (every evidence file except SECRET-SCAN-FINAL-*)"
  ek_ctl run seal -- python3 "$K/k08_redact_scan.py" scan "$EK_EVIDENCE" --exact --seal; rc=$?
  ek_ctl shutdown >/dev/null 2>&1 || true
  [ $rc = 0 ] || ek_die "final seal scan FAILED (see SECRET-SCAN-FINAL-a*.txt)"
  echo "SEALED: supervisor shut down; lane values no longer exist anywhere."
  ;;
*) echo "usage: run-kit.sh up|down|seal"; exit 2 ;;
esac
