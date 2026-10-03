#!/usr/bin/env bash
# CANDIDATE — NOT ACTIVE. MVP6 evidence kit v1.2 driver.
#   run-kit.sh up     K01 source → K03 ports → K02 runtime → K06 build → K04b gateway → K04 shared/render/check
#                     → K05 mongo → supervisor start (secrets in memory only) → K06 launch via supervisor → netcheck
#                     → K07 identities (run by the supervisor)
#   (lane work: browser/HTTP harness scripts in $EK_HARNESS_DIR through `k00_ctl.py $EK_SOCK run harness <file>` so
#    they get the actor passwords in their environment; K10 snapshots with k10_snap.sh around every mutation, setup step and negative case;
#    record-served for every test-route override; K09 binding while running; dotnet tests via `k06_build_launch.sh test`)
#   run-kit.sh down   netcheck → K11 cleanup (supervisor stays up) → ARTIFACTS.sha256 → seal
#   run-kit.sh seal   final exact-value + pattern scan by the supervisor (the LAST write, G4) → supervisor shutdown
#   run-kit.sh abort  failure path by hand: supervisor abort (stops its services, clears values, removes socket) → K11
# v1.2 (F4): `up` refuses to start when a live supervisor answers on $EK_SOCK, and any failure inside `up` runs the
#       abort path automatically (supervisor abort → K11 cleanup) and exits non-zero — no orphaned supervisor or service.
# v1.1: every phase is one COMMANDS.tsv row via ek_run (G2); the supervisor adds one row per launch/run (D4).
# Every phase stops the run on failure; nothing is retried silently. Requires lane.env (see lane.env.example).
set -u
K=/Users/natig/Projects/ERP-vNext-recovery/scripts/evidence-kit   # Q84b lane copy (from Q64d up-q64d.sh, unchanged logic) of run-kit.sh `up`: kit files are used in place, unchanged
. "$K/ek_lib.sh"
ek_require_env
# step <phase> <description> -- command...   (logged, fails the run on non-zero exit)
step() { local ph=$1 d=$2; shift 2; [ "${1:-}" = "--" ] && shift; echo "== $ph: $d"; ek_run "$ph" "$ph" "$d" -- "$@" || ek_die "phase failed: $ph ($d)"; }
ROTATE=${EK_ROTATE:-"jwt mfa auth-platform platform-mdm-active svcid"}

supervisor_alive() { [ -S "$EK_SOCK" ] && ek_ctl ping >/dev/null 2>&1; }

abort_path() {   # F4: stop what this lane started, clear the values, remove the socket, clean W
  if supervisor_alive; then
    ek_run K00 abort "k00_ctl.py abort (stop own services, clear values, remove socket)" -- ek_ctl abort >/dev/null
  fi
  # Q84b: K11 (rm -rf of the lane workspace) is NOT run — owner rule "no rm, use new folders"; the workspace stays for evidence.
  echo "K11 skipped by lane rule (no rm); services stopped by supervisor abort" >&2
}

on_up_exit() {
  local rc=$?
  trap - EXIT
  [ "$rc" = 0 ] && return 0
  echo "== UP FAILED (exit $rc): abort path (supervisor abort + K11 cleanup)" >&2
  abort_path
  exit "$rc"
}

case "${1:-}" in
up)
  supervisor_alive && ek_die "a live lane supervisor already owns $EK_SOCK — run 'run-kit.sh abort' (or finish with down/seal) first"
  trap on_up_exit EXIT
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
    --actors "$EK_ACTORS" --socket "$EK_SOCK" --suffix "$EK_DB_SUFFIX" --rotate "$ROTATE" --dotnet "$EK_DOTNET" \
    --expected-runtime "$EK_EXPECTED_RUNTIME" ${EK_HARNESS_DIR:+--harness-dir "$EK_HARNESS_DIR"} \
    > "$(ek_unique "$EK_WORK" supervisor log)" 2>&1 &
  for _ in $(seq 1 40); do ek_ctl ping >/dev/null 2>&1 && break; sleep 0.25; done
  ek_ctl ping >/dev/null 2>&1 || ek_die "supervisor did not start"
  for s in $EK_SERVICES; do step K06 "launch $s via supervisor" -- bash "$K/k06_build_launch.sh" launch "$s"; done
  step K06 "netcheck after start" -- bash "$K/k06_build_launch.sh" netcheck
  # Q84b (as Q64d/Q64b) lane deviation (A12 VER-02 §7.5 / Q24b attempt 1): the lane Platform has no record for seeded Auth tenant 97c5, so
  # tenant login returns 401 and K07 fails. One supervisor harness task writes the tenant record into the LANE Platform DB.
  mkdir -p "$EK_EVIDENCE/raw/fixture"
  step K07pre "tenant_fixture.py via supervisor harness (lane Platform DB tenant records; no secret)" -- ek_ctl run harness tenant_fixture.py -- "$(ek_port mongo)" "$EK_DB_SUFFIX" "$EK_EVIDENCE/raw/fixture/tenant-fixture.json"
  step K07 "k07_identity.py rotate (supervisor task k07; actor passwords only)" -- ek_ctl run k07
  trap - EXIT
  echo "UP: lane running. Do the lane work (harness via k00_ctl.py run harness), K10 snapshots, K09 binding, then: run-kit.sh down"
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
  ek_ctl run seal; rc=$?
  ek_ctl shutdown >/dev/null 2>&1 || true
  [ $rc = 0 ] || ek_die "final seal scan FAILED (see SECRET-SCAN-FINAL-a*.txt)"
  echo "SEALED: supervisor shut down; lane values no longer exist anywhere."
  ;;
abort)
  abort_path; rc=$?
  echo "ABORTED: supervisor values cleared (if it was running); cleanup result in CLEANUP*.tsv"
  exit $rc
  ;;
*) echo "usage: run-kit.sh up|down|seal|abort"; exit 2 ;;
esac
