#!/usr/bin/env bash
# CANDIDATE — NOT ACTIVE. MVP6 evidence kit v1.1 phase K06: fresh native .NET 8 Release build, bound launch, tests.
#   k06_build_launch.sh build  <service>              restore + build -c Release from the K01 source; hash the DLL
#   k06_build_launch.sh launch <service>              start ONLY after K04 check PASS, THROUGH the lane supervisor (D4)
#   k06_build_launch.sh test   <label> <csproj> [dotnet-test args...]
#                                                     dotnet test with --results-directory inside the lane evidence (G6)
#   k06_build_launch.sh netcheck                      every kit PID: no TCP connection to :27017; Mongo users on the lane port
# v1.1: D4 launch is done by k00_supervisor.py, which substitutes the @@LANE:<group>@@ placeholders in memory — this
#       script never sees a secret. G3 build logs, TRX folders and health files are attempt-suffixed, never overwritten.
#       G2 every command is one COMMANDS.tsv row via ek_run.
set -u
. "$(dirname "$0")/ek_lib.sh"
ek_require_env
RAW="$EK_EVIDENCE/raw"; MAP="$(dirname "$0")/service-map.tsv"
mode=${1:?build|launch|test|netcheck}; svc=${2:-}

col() { awk -F'\t' -v s="$svc" -v c="$1" 'NR==1{for(i=1;i<=NF;i++)h[$i]=i; next} $1==s{print $h[c]}' "$MAP"; }

if [ "$mode" = build ] || [ "$mode" = launch ]; then
  [ -n "$svc" ] || ek_die "service name required"
  PROJ="$EK_WORK/source/$(col project_dir)"; CSPROJ="$PROJ/$(col csproj)"; DLLNAME=$(col dll)
  OUT="$PROJ/bin/Release/net8.0"
  [ -f "$CSPROJ" ] || ek_die "unknown service or missing project: $svc"
fi

case "$mode" in
build)
  mkdir -p "$RAW/build"
  ropts=""; [ "${EK_RESTORE_MODE:-online}" = offline-cache ] && ropts="--ignore-failed-sources -p:NuGetAudit=false"
  rlog=$(ek_unique "$RAW/build" "$svc-restore" log); blog=$(ek_unique "$RAW/build" "$svc-build" log)
  # shellcheck disable=SC2086
  ek_run K06 "$svc-restore" "dotnet restore <$svc csproj> $ropts > raw/build/$(basename "$rlog")" -- \
    "$EK_DOTNET" restore "$CSPROJ" $ropts > "$rlog" 2>&1 || ek_die "$svc restore failed (see $rlog)"
  ek_run K06 "$svc-build" "dotnet build <$svc csproj> -c Release --no-restore > raw/build/$(basename "$blog")" -- \
    "$EK_DOTNET" build "$CSPROJ" -c Release --no-restore > "$blog" 2>&1 || ek_die "$svc build failed (see $blog)"
  rc_json="$OUT/${DLLNAME%.dll}.runtimeconfig.json"
  python3 - "$rc_json" <<'PY' || ek_die "$svc runtimeconfig is not net8.0"
import json,sys
c=json.load(open(sys.argv[1]))["runtimeOptions"]
assert c["tfm"]=="net8.0", c["tfm"]
fws=c.get("frameworks") or [c["framework"]]
assert all(f["version"].startswith("8.0.") for f in fws), fws
PY
  printf '%s  %s\n' "$(ek_sha256 "$OUT/$DLLNAME")" "$OUT/$DLLNAME" >> "$RAW/binary-sha256.txt"
  echo "K06 build PASS: $svc $(ek_sha256 "$OUT/$DLLNAME")"
  ;;

launch)
  grep -qx none "$RAW/effective-config-findings.txt" 2>/dev/null || ek_die "K04 check has not passed — refusing to start $svc"
  [ -z "$(ek_listener_pid "$(ek_port "$svc")")" ] || ek_die "port $(ek_port "$svc") already in use"
  # The supervisor writes the COMMANDS.tsv row, pids.tsv and raw/processes.tsv for this launch.
  reply=$(ek_ctl launch "$svc") || ek_die "$svc launch failed: $reply"
  echo "K06 launch: $reply"
  ;;

test)
  # G6 (Loads lesson, mvp6-loads-publication-guard-01/REPORT.md §Deviations 1): TRX always lands in the lane evidence
  # folder under an attempt-suffixed directory, never in the repository's TestResults/ (existing evidence there).
  label=${2:?label}; proj=${3:?csproj relative to the K01 source}; shift 3
  tdir=$(ek_unique "$RAW/test-results" "$label" d); mkdir -p "$tdir"
  tlog="$tdir/dotnet-test.log"
  ek_run K06 "test-$label" "dotnet test <$proj> --results-directory raw/test-results/$(basename "$tdir") --logger trx $*" -- \
    "$EK_DOTNET" test "$EK_WORK/source/$proj" --results-directory "$tdir" --logger "trx;LogFileName=$label.trx" "$@" > "$tlog" 2>&1
  rc=$?
  echo "K06 test $label: exit $rc; TRX in $tdir"
  exit $rc
  ;;

netcheck)
  mport=$(ek_port mongo); fail=0
  out=$(ek_unique "$RAW" netcheck tsv)
  printf 'service\tpid\tconnections_to_27017\tconnections_to_lane_mongo\n' > "$out"
  while IFS=$'\t' read -r role pid _ _; do
    [ "$role" = mongod ] && continue
    conns=$(lsof -a -nP -p "$pid" -iTCP -sTCP:ESTABLISHED 2>/dev/null)
    op=$(printf '%s\n' "$conns" | grep -c -- '->[^ ]*:27017 ' || true)
    ln=$(printf '%s\n' "$conns" | grep -c -- "->127.0.0.1:$mport " || true)
    printf '%s\t%s\t%s\t%s\n' "$role" "$pid" "$op" "$ln" >> "$out"
    [ "$op" = 0 ] || fail=1
  done < "$EK_WORK/pids.tsv"
  lsof -nP -iTCP:"$mport" -sTCP:ESTABLISHED > "$(ek_unique "$RAW" lane-mongo-connections txt)" 2>&1
  [ $fail = 0 ] || ek_die "a kit process holds a connection to 27017 — evidence invalid; run K11"
  echo "K06 netcheck PASS: no kit process connected to 27017 ($(basename "$out"))"
  ;;
*) ek_die "unknown mode $mode" ;;
esac
