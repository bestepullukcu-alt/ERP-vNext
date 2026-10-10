#!/usr/bin/env bash
# CANDIDATE — NOT ACTIVE. MVP6 evidence kit phase K06: fresh native .NET 8 Release build and bound launch.
#   k06_build_launch.sh build  <service>   restore + build -c Release from the K01 source; hash the DLL
#   k06_build_launch.sh launch <service>   start ONLY after K04 check PASS; record PID/command/cwd/listener/runtime
#   k06_build_launch.sh netcheck           every kit PID: no TCP connection to :27017; Mongo users reach the lane port
# Processes start with `env -i` so nothing from the operator's shell (e.g. a stray Mongo__ConnectionString)
# can leak in; the only configuration is the K04-rendered env file + --urls (+ --contentRoot for Web).
set -u
. "$(dirname "$0")/ek_lib.sh"
ek_require_env
RAW="$EK_EVIDENCE/raw"; MAP="$(dirname "$0")/service-map.tsv"
mode=${1:?build|launch|netcheck}; svc=${2:-}

col() { awk -F'\t' -v s="$svc" -v c="$1" 'NR==1{for(i=1;i<=NF;i++)h[$i]=i; next} $1==s{print $h[c]}' "$MAP"; }

if [ "$mode" != netcheck ]; then
  [ -n "$svc" ] || ek_die "service name required"
  PROJ="$EK_WORK/source/$(col project_dir)"; CSPROJ="$PROJ/$(col csproj)"; DLLNAME=$(col dll)
  OUT="$PROJ/bin/Release/net8.0"
  [ -f "$CSPROJ" ] || ek_die "unknown service or missing project: $svc"
fi

case "$mode" in
build)
  mkdir -p "$RAW/build"
  ropts=""; [ "${EK_RESTORE_MODE:-online}" = offline-cache ] && ropts="--ignore-failed-sources -p:NuGetAudit=false"
  # shellcheck disable=SC2086
  ek_run K06 "$svc-restore" "dotnet restore <$svc csproj> $ropts" -- \
    "$EK_DOTNET" restore "$CSPROJ" $ropts > "$RAW/build/$svc-restore.log" 2>&1
  echo $? > "$RAW/build/$svc-restore.exit"
  ek_run K06 "$svc-build" "dotnet build <$svc csproj> -c Release --no-restore" -- \
    "$EK_DOTNET" build "$CSPROJ" -c Release --no-restore > "$RAW/build/$svc-build.log" 2>&1
  rc=$?; echo $rc > "$RAW/build/$svc-build.exit"; [ $rc = 0 ] || ek_die "$svc build failed"
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
  PORT=$(ek_port "$svc"); ENVF="$EK_WORK/env/$svc.env"; LOG="$EK_WORK/logs/$svc.log"; mkdir -p "$EK_WORK/logs"
  [ -z "$(ek_listener_pid "$PORT")" ] || ek_die "port $PORT already in use"
  extra=""
  case "$(col content_root)" in
    outdir)          RUNDIR="$OUT";                  DLL="$OUT/$DLLNAME" ;;
    project)         RUNDIR="$PROJ";                 DLL="$OUT/$DLLNAME"; extra="--contentRoot $PROJ" ;;
    gateway-runtime) RUNDIR="$EK_WORK/gateway-runtime"; DLL="$RUNDIR/$DLLNAME" ;;
  esac
  # shellcheck disable=SC2086
  ( cd "$RUNDIR" && exec env -i HOME="$HOME" PATH="/usr/bin:/bin:/usr/sbin:/sbin" TMPDIR="${TMPDIR:-/tmp}" LANG=en_US.UTF-8 \
      bash -c 'set -a; . "$1"; set +a; shift; exec "$@"' _ "$ENVF" "$EK_DOTNET" "$DLL" --urls "http://127.0.0.1:$PORT" $extra \
  ) > "$LOG" 2>&1 &
  PID=$!
  printf '%s\t%s\t%s\t%s\n' "$svc" "$PID" "$PORT" "$EK_WORK" >> "$EK_WORK/pids.tsv"
  printf '%s\t%s\t%s\t%s\t%s\t%s\n' "$(ek_now)" "$(ek_now)" K06 "$svc-launch" \
    "env -i + <$svc.env> dotnet $DLLNAME --urls http://127.0.0.1:$PORT$( [ -n "$extra" ] && echo ' --contentRoot <source project>')" \
    "started pid $PID" >> "$EK_EVIDENCE/COMMANDS.tsv"
  for _ in $(seq 1 240); do
    lp=$(ek_listener_pid "$PORT"); [ -n "$lp" ] && break
    kill -0 "$PID" 2>/dev/null || ek_die "$svc exited during start (see lane log)"; sleep 0.5
  done
  [ "${lp:-}" = "$PID" ] || ek_die "$svc: listener on $PORT is pid ${lp:-none}, not the launched pid $PID"
  code=$(curl -s -o "$RAW/health-$svc.body" -D "$RAW/health-$svc.headers" -m 10 -w '%{http_code}' "http://127.0.0.1:$PORT$(col health_path)")
  rt=$(lsof -nP -p "$PID" 2>/dev/null | grep -o 'Microsoft\.NETCore\.App/[0-9][0-9.]*' | head -1)
  cwd=$(lsof -a -nP -p "$PID" -d cwd -Fn 2>/dev/null | sed -n 's/^n//p')
  cmd=$(ps -o command= -p "$PID")   # argv only; never `ps e`, which would print the environment (secrets)
  lstart=$(ps -o lstart= -p "$PID" | sed 's/  */ /g')
  [ "$rt" = "Microsoft.NETCore.App/$EK_EXPECTED_RUNTIME" ] || ek_die "$svc loaded runtime '$rt', expected $EK_EXPECTED_RUNTIME"
  [ -f "$RAW/processes.tsv" ] || printf 'service\tpid\tport\tstarted\tdll\tdll_sha256\tcwd\tloaded_runtime\thealth_http\tcommand\n' > "$RAW/processes.tsv"
  printf '%s\t%s\t%s\t%s\t%s\t%s\t%s\t%s\t%s\t%s\n' "$svc" "$PID" "$PORT" "$lstart" "$DLL" "$(ek_sha256 "$DLL")" "$cwd" "$rt" "$code" "$cmd" >> "$RAW/processes.tsv"
  echo "K06 launch: $svc pid=$PID port=$PORT health=$code runtime=$rt"
  ;;

netcheck)
  mport=$(ek_port mongo); fail=0
  printf 'service\tpid\tconnections_to_27017\tconnections_to_lane_mongo\n' > "$RAW/netcheck.tsv"
  while IFS=$'\t' read -r role pid _ _; do
    [ "$role" = mongod ] && continue
    conns=$(lsof -a -nP -p "$pid" -iTCP -sTCP:ESTABLISHED 2>/dev/null)
    op=$(printf '%s\n' "$conns" | grep -c -- '->[^ ]*:27017 ' || true)
    ln=$(printf '%s\n' "$conns" | grep -c -- "->127.0.0.1:$mport " || true)
    printf '%s\t%s\t%s\t%s\n' "$role" "$pid" "$op" "$ln" >> "$RAW/netcheck.tsv"
    [ "$op" = 0 ] || fail=1
  done < "$EK_WORK/pids.tsv"
  lsof -nP -iTCP:"$mport" -sTCP:ESTABLISHED > "$RAW/lane-mongo-connections.txt" 2>&1
  [ $fail = 0 ] || ek_die "a kit process holds a connection to 27017 — evidence invalid; run K11"
  echo "K06 netcheck PASS: no kit process connected to 27017"
  ;;
*) ek_die "unknown mode $mode" ;;
esac
