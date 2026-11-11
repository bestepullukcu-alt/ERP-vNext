#!/usr/bin/env bash
# WP-0230-DL3 host smoke: how the MOD-0230 service host (Diten.PvgService.Api) really starts.
# Measures AC-8 (startup fail-closed + /health/ready 503), AC-10 (approval reference) and the other start refusals
# the code has, on the REAL process: build once, then start the API host with `dotnet run --no-build` for each case.
# Run from anywhere inside this checkout (the script finds the repository root from its own path); normally through
# scripts/run_record.py. Exit 0 = every case PASS; 1 = at least one case FAIL; 3 = setup failure (load never dropped,
# port 5011 busy, build failed, tool missing) - a setup failure is never read as a refusal.
#
# Configuration keys: read from the code, not assumed.
#   Program.cs:14-18                env ASPNETCORE_ENVIRONMENT (unset -> null), env ASPNETCORE_URLS (unset -> http://127.0.0.1:5011)
#   PvgServiceApiHost.cs:172-173    unset / empty / whitespace environment name -> Production; any other name passed untrimmed
#   PvgServiceApiHost.cs:213-236    Pvg:Runtime:ProductionLike (false), Pvg:Runtime:OperationalRuntimeAuthorized (false),
#                                   Pvg:Runtime:UseNonProductionAdapters (default = not production-like),
#                                   Pvg:Runtime:ApprovalReference (read as given, shape ^RTA-MOD0230-[0-9]{3}\z)
#   PvgServiceApiHost.cs:109-111    JwtSettings:Secret / Issuer / Audience / PreviousSecrets (missing = no refusal, by design)
# Production-like = environment name blank or "Production" (case-insensitive) or Pvg:Runtime:ProductionLike=true.
# Refusals, in the order the code raises them (PvgServiceApiHost.cs:238-258, 175-192):
#   ThrowIfNullOrWhiteSpace(urls)                 -> ArgumentException            (empty ASPNETCORE_URLS)
#   configuration.GetValue<bool>                  -> InvalidOperationException    (unparsable bool value)
#   production-like && !authorized                -> "operational runtime is not authorized"
#   production-like && UseNonProductionAdapters   -> "cannot use non-production adapters"
#   production-like && authorized && bad reference-> "requires an operational runtime approval reference"
#   production-like && a required port/store not registered -> "required dependencies are not registered: <safe codes>"
#   Kestrel rejects the URL (unrecognized scheme)  -> framework refusal
#
# Safety:
#   - Only the PvgService API host is started, on 5011 (the port MOD-0230 owns). If 5011 is already in use the script
#     stops with exit 3; it never picks another port. Every case uses the default URL, except the two URL cases
#     (empty value; ftp scheme), which cannot bind anything.
#   - Only processes this script started are signalled: `dotnet run` (my child; pid + start time recorded) and the
#     host app below it (child of that pid; pid + start time recorded). Before any signal the pid is checked to be
#     alive, to carry the recorded start time and to have the recorded parent. No pkill, no kill by name.
#   - The host gets a minimal environment (`env -i`): nothing inherited from the caller can change its configuration.
#   - JwtSettings values are generated at run time, kept in a mode-600 file inside a private temp directory, passed
#     to the host through its environment, never printed, never written elsewhere. They are also the sentinels for the
#     "no secret appears" checks. No connection string, no MongoDB, nothing on port 27017, no shared dev service.
#   - Build is `dotnet build` of the API project only; the host is started with `--no-build`, so a compile problem
#     (exit 3) cannot be confused with a refusal. Every wait is bounded; a host that neither answers nor exits is a FAIL.
set -u

PORT=5011
HOST_WAIT=60        # seconds a started host may take to either listen or exit
HTTP_MAX=10         # seconds per HTTP request
STOP_WAIT=20        # seconds to wait for my processes to end after SIGTERM
LOAD_MAX=12         # 1-minute load average above which the script waits before building
LOAD_WAIT_MAX=1200  # seconds the script waits for the load to drop before giving up (exit 3)

SCRIPT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
ROOT=$(cd "$SCRIPT_DIR/../../../../.." && pwd)
API_DIR=services/Diten.PvgService/src/Diten.PvgService.Api
CSPROJ=$API_DIR/Diten.PvgService.Api.csproj

NOT_AUTH='operational runtime is not authorized'
NO_ADAPTERS='cannot use non-production adapters'
NO_REF='approval reference'
DEP_PHRASE='required dependencies are not registered'
DEP_CODES_SORTED='PVG_DEPENDENCY_EVIDENCE_LINK,PVG_DEPENDENCY_FIELD_SECURITY,PVG_DEPENDENCY_INTAKE_STORE,PVG_DEPENDENCY_PERMISSION_GATE,PVG_DEPENDENCY_WORKFLOW_GATE'
GOOD_REF='RTA-MOD0230-999'   # well-formed, synthetic: the host checks the shape only; no approval record exists for it
LEAK_RE='mongodb(\+srv)?://|connection ?string|password|passwd|pwd=|secret|api[_-]?key|bearer |eyJ[A-Za-z0-9_-]{10,}|:27017|server=|data source=|user ?id='

fail=0
host_pid=""; host_lstart=""; host_reaped=0; child_pid=""; child_lstart=""
HOST_LOG=""; OUT_STATE=""; OUT_RC=""
T=""

note() { echo "-- $*"; }
setup_fail() { echo "SETUP FAIL: $*"; exit 3; }

for tool in dotnet curl lsof python3 pgrep ps awk sed grep cut tr sort; do
  command -v "$tool" >/dev/null 2>&1 || setup_fail "required tool not found: $tool"
done
cd "$ROOT" || setup_fail "cannot enter repository root $ROOT"
[ -f "$CSPROJ" ] || setup_fail "$CSPROJ not found under $ROOT (is this script inside the repository checkout?)"

T=$(mktemp -d) || setup_fail "mktemp failed"
chmod 700 "$T"

# ---------------------------------------------------------------------------------------------------- process helpers
proc_lstart() { ps -o lstart= -p "$1" 2>/dev/null | sed -E 's/^ +//; s/ +$//'; }
proc_ppid() { ps -o ppid= -p "$1" 2>/dev/null | tr -d ' '; }
proc_alive() {
  local st
  st=$(ps -o stat= -p "$1" 2>/dev/null | tr -d ' ')
  [ -n "$st" ] || return 1
  case "$st" in Z*) return 1 ;; esac
  return 0
}
# my `dotnet run`: alive, same start time as recorded, and a direct child of this script
own_host() {
  [ -n "$host_pid" ] && proc_alive "$host_pid" && [ "$(proc_lstart "$host_pid")" = "$host_lstart" ] \
    && [ "$(proc_ppid "$host_pid")" = "$$" ]
}
# the host app: alive, same start time as recorded, child of my `dotnet run` (or re-parented to 1 if that one is gone)
own_child() {
  local pp
  [ -n "$child_pid" ] && proc_alive "$child_pid" && [ "$(proc_lstart "$child_pid")" = "$child_lstart" ] || return 1
  pp=$(proc_ppid "$child_pid")
  [ "$pp" = "$host_pid" ] || [ "$pp" = "1" ]
}
find_child() {
  local c
  [ -n "$child_pid" ] && return 0
  [ -n "$host_pid" ] || return 1
  for c in $(pgrep -P "$host_pid" 2>/dev/null); do
    child_pid=$c
    child_lstart=$(proc_lstart "$c")
    return 0
  done
  return 1
}
listener_pids() { lsof -nP -iTCP:"$PORT" -sTCP:LISTEN -Fp 2>/dev/null | sed -n 's/^p//p'; }
port_free() { [ -z "$(listener_pids)" ]; }
wait_port_free() {
  local i=0
  while [ "$i" -lt 40 ]; do
    port_free && return 0
    sleep 0.5
    i=$((i + 1))
  done
  return 1
}

stop_host() {
  local n=0
  [ -n "$host_pid" ] || return 0
  if own_host; then find_child; fi
  if own_host; then
    note "stop: kill -TERM $host_pid (dotnet run, started $host_lstart, my child; it forwards the signal to the host app${child_pid:+ pid $child_pid})"
    kill -TERM "$host_pid" 2>/dev/null
  fi
  while [ "$n" -lt $((STOP_WAIT * 2)) ]; do
    if ! own_host && ! own_child; then break; fi
    sleep 0.5
    n=$((n + 1))
  done
  if own_child; then
    note "stop: host app pid $child_pid still running -> kill -KILL (recorded pid, start time and parent verified)"
    kill -KILL "$child_pid" 2>/dev/null
  fi
  if own_host; then
    note "stop: dotnet run pid $host_pid still running -> kill -KILL (recorded pid, start time and parent verified)"
    kill -KILL "$host_pid" 2>/dev/null
  fi
  if [ "$host_reaped" = "0" ]; then wait "$host_pid" 2>/dev/null; fi
  host_pid=""; host_lstart=""; child_pid=""; child_lstart=""; host_reaped=0
  return 0
}

cleanup() {
  stop_host
  if [ -n "$T" ] && [ -d "$T" ]; then rm -rf -- "$T"; fi
}
trap cleanup EXIT
trap 'exit 130' INT
trap 'exit 143' TERM

# ------------------------------------------------------------------------------------------------ result + scan helpers
record() { # PASS|FAIL id name expected actual
  local verdict=$1 id=$2 name=$3 expected=$4 actual=$5
  [ "$verdict" = "FAIL" ] && fail=1
  echo "[$verdict] $id $name :: expected: $expected :: actual: $(printf '%s' "$actual" | cut -c1-420)"
  echo "$verdict" >> "$T/verdicts"
}

# prints "clean" or "LEAK:<kind>" (never the leaked value); rc 0 = clean. Sentinels = run-time JwtSettings values + extras.
scan_file() { # file [extra sentinel...]
  local f=$1 s; shift
  cp "$T/sentinels" "$T/pat"; chmod 600 "$T/pat"
  for s in "$@"; do [ -n "$s" ] && printf '%s\n' "$s" >> "$T/pat"; done
  if grep -qF -f "$T/pat" "$f" 2>/dev/null; then echo "LEAK:configured-value"; return 1; fi
  if [ "${SCAN_PATTERN:-1}" = "1" ] && grep -qiE "$LEAK_RE" "$f" 2>/dev/null; then echo "LEAK:secret-or-connection-pattern"; return 1; fi
  echo clean
}

health_summary() { # file -> one safe line
  python3 -I - "$1" <<'PY'
import json, sys
try:
    d = json.load(open(sys.argv[1], encoding="utf-8"))
except Exception:
    print("not-json"); sys.exit(0)
if not isinstance(d, dict):
    print("not-object"); sys.exit(0)
def b(v): return str(v).lower()
parts = ["status=%s" % d.get("status"), "mode=%s" % d.get("mode"),
         "operationalRuntimeAuthorized=%s" % b(d.get("operationalRuntimeAuthorized")),
         "nonProductionAdaptersEnabled=%s" % b(d.get("nonProductionAdaptersEnabled"))]
codes = d.get("missingDependencyCodes")
if codes is not None:
    parts.append("codes=" + ",".join(sorted(str(c) for c in codes)))
extra = sorted(set(d.keys()) - {"status", "mode", "operationalRuntimeAuthorized", "nonProductionAdaptersEnabled", "missingDependencyCodes"})
if extra:
    parts.append("unexpected-members=" + ",".join(extra))
print(" ".join(parts))
PY
}

http_get() { # path outfile -> HTTP status on stdout (000 = no answer)
  curl -sS --noproxy '*' --max-time "$HTTP_MAX" -o "$2" -w '%{http_code}' "http://127.0.0.1:$PORT$1" 2>/dev/null
}

exc_line() { # first unhandled-exception line of the host log: "<Type>: <message>" (cut before an inner exception)
  grep -m1 -E '^Unhandled exception\. ' "$HOST_LOG" 2>/dev/null | sed -E 's/^Unhandled exception\. //; s/ ---> .*$//' | cut -c1-300
}

# ------------------------------------------------------------------------------------------------------ host start/wait
start_host() { # label, then VAR=VALUE entries for the host environment
  local label=$1; shift
  if ! port_free; then
    echo "SETUP FAIL: port $PORT is already in use before case $label (listener pid: $(listener_pids | tr '\n' ' ')). Not starting, not choosing another port."
    exit 3
  fi
  HOST_LOG="$T/host-$label.log"
  : > "$HOST_LOG"
  env -i ${BASE_ENV[@]+"${BASE_ENV[@]}"} ${JWT_ENV[@]+"${JWT_ENV[@]}"} ${@+"$@"} \
    dotnet run --no-build --no-launch-profile -c Debug --project "$CSPROJ" > "$HOST_LOG" 2>&1 &
  host_pid=$!
  host_lstart=$(proc_lstart "$host_pid")
  host_reaped=0; child_pid=""; child_lstart=""
  note "case $label: started 'dotnet run --no-build' pid $host_pid (my child, started $host_lstart)"
}

# sets OUT_STATE = exited | listening | timeout, and OUT_RC for exited; a listener that is not my host app is a setup failure
await_outcome() {
  local n=0 max=$((HOST_WAIT * 2)) lp
  OUT_STATE=timeout; OUT_RC=""
  while [ "$n" -lt "$max" ]; do
    if ! proc_alive "$host_pid"; then
      wait "$host_pid" 2>/dev/null; OUT_RC=$?; host_reaped=1; OUT_STATE=exited
      return 0
    fi
    find_child
    lp=$(listener_pids)
    if [ -n "$lp" ] && [ -n "$child_pid" ]; then
      if printf '%s\n' "$lp" | grep -qx "$child_pid"; then OUT_STATE=listening; return 0; fi
      setup_fail "port $PORT is held by pid(s) $(printf '%s' "$lp" | tr '\n' ' ') which are not my host app (pid $child_pid). Not touching them, not choosing another port."
    fi
    sleep 0.5
    n=$((n + 1))
  done
  return 0
}

describe_outcome() { # text for a case whose host was expected to listen
  case "$OUT_STATE" in
    exited) echo "host exited (exit=$OUT_RC) before listening; $(exc_summary)" ;;
    timeout) echo "NO ANSWER: host neither listened on $PORT nor exited within ${HOST_WAIT}s" ;;
    *) echo "$OUT_STATE" ;;
  esac
}

exc_summary() { # message line, after the leak scan
  local msg
  msg=$(exc_line)
  if [ -z "$msg" ]; then echo "no unhandled-exception line in the host log"; return; fi
  printf '%s\n' "$msg" > "$T/msg.txt"
  if scan_file "$T/msg.txt" >/dev/null; then echo "msg=\"$msg\""; else echo "msg=(withheld: scan flagged it)"; fi
}

# refusal_case ID NAME PHRASE KIND EXTRA_SENTINEL ENV...   KIND: contains | deps | any
refusal_case() {
  local id=$1 name=$2 phrase=$3 kind=$4 extra=$5; shift 5
  local expected verdict=PASS actual msg scan rc_scan body codes
  case "$kind" in
    contains) expected="start refused: exit != 0, host never listens, message contains '$phrase'" ;;
    deps) expected="start refused: exit != 0, host never listens, message '$phrase' and exactly the 5 safe dependency codes" ;;
    *) expected="start refused: exit != 0, host never listens" ;;
  esac
  start_host "$id" "$@"
  await_outcome
  if [ "$OUT_STATE" != "exited" ]; then
    case "$OUT_STATE" in
      listening) actual="HOST STARTED and listens on $PORT (not refused)" ;;
      *) actual=$(describe_outcome) ;;
    esac
    stop_host
    wait_port_free || setup_fail "port $PORT still in use after stopping my processes (case $id)"
    record FAIL "$id" "$name" "$expected" "$actual"
    return
  fi
  if grep -qiE 'address already in use|Failed to bind' "$HOST_LOG"; then
    setup_fail "case $id: the host failed to bind port $PORT; something unseen holds it. Not choosing another port."
  fi
  msg=$(exc_line)
  body=$(printf '%s' "$msg" | sed -E 's/^[A-Za-z0-9_.]+(Exception): //')
  printf '%s\n' "$msg" > "$T/msg.txt"
  scan=$(scan_file "$T/msg.txt" "$extra"); rc_scan=$?
  actual="exit=$OUT_RC"
  [ "$OUT_RC" != "0" ] || verdict=FAIL
  if grep -q 'Now listening on' "$HOST_LOG"; then verdict=FAIL; actual="$actual; log shows 'Now listening on' (host had started)"; fi
  if [ -z "$msg" ]; then
    verdict=FAIL; actual="$actual; no unhandled-exception line in the host log"
  else
    if [ "$rc_scan" -eq 0 ]; then actual="$actual; msg=\"$msg\""; else actual="$actual; msg=(withheld: $scan)"; verdict=FAIL; fi
    case "$kind" in
      contains)
        printf '%s' "$body" | grep -qiF -- "$phrase" || verdict=FAIL ;;
      deps)
        printf '%s' "$body" | grep -qiF -- "$phrase" || verdict=FAIL
        codes=$(printf '%s' "$body" | grep -oE 'PVG_[A-Z0-9_]+' | sort | tr '\n' ',' | sed 's/,$//')
        [ "$codes" = "$DEP_CODES_SORTED" ] || { verdict=FAIL; actual="$actual; codes=[$codes]"; }
        if printf '%s' "$body" | grep -qE 'IPvg|Diten\.|System\.'; then verdict=FAIL; actual="$actual; message carries a type name"; fi ;;
    esac
  fi
  if [ -n "$extra" ] && grep -qF -- "$extra" "$HOST_LOG"; then verdict=FAIL; actual="$actual; configured value echoed in the host output"; fi
  if grep -qF -f "$T/sentinels" "$HOST_LOG"; then verdict=FAIL; actual="$actual; run-time JwtSettings value found in the host output"; fi
  wait_port_free || setup_fail "port $PORT still in use after case $id ended"
  stop_host
  record "$verdict" "$id" "$name" "$expected" "$actual"
}

# check_health ID NAME PATH WANT_HTTP WANT_STATUS EXTRA EXPECTED     EXTRA: none | codes5 | adapters-on
check_health() {
  local id=$1 name=$2 path=$3 want=$4 wstatus=$5 extra=$6 expected=$7
  local f="$T/body-$id.json" s scan rc sum verdict=PASS
  s=$(http_get "$path" "$f")
  if [ "$s" = "000" ]; then
    record FAIL "$id" "$name" "$expected" "NO HTTP ANSWER within ${HTTP_MAX}s from the listening host"
    return
  fi
  scan=$(scan_file "$f"); rc=$?
  if [ "$rc" -eq 0 ]; then sum=$(health_summary "$f"); else sum="(body withheld)"; verdict=FAIL; fi
  [ "$s" = "$want" ] || verdict=FAIL
  case " $sum " in *" status=$wstatus "*) ;; *) verdict=FAIL ;; esac
  case "$extra" in
    codes5) case "$sum" in *"codes=$DEP_CODES_SORTED"*) ;; *) verdict=FAIL ;; esac
            case "$sum" in *unexpected-members*) verdict=FAIL ;; esac ;;
    adapters-on) case "$sum" in *"nonProductionAdaptersEnabled=true"*) ;; *) verdict=FAIL ;; esac ;;
  esac
  record "$verdict" "$id" "$name" "$expected" "HTTP $s; $sum; secret/connection scan: $scan"
}

# ============================================================================================================ pre-checks
echo "== pre-check: machine load (build starts only when the 1-minute load average is <= $LOAD_MAX)"
waited=0
while :; do
  up=$(LC_ALL=C uptime)
  load=$(printf '%s' "$up" | sed -E 's/.*load averages?: *([0-9]+[.,][0-9]+).*/\1/' | tr ',' '.')
  echo "uptime: $up"
  case "$load" in
    ''|*[!0-9.]*) setup_fail "cannot read the 1-minute load average from uptime" ;;
  esac
  if awk -v l="$load" -v m="$LOAD_MAX" 'BEGIN { exit !(l > m) }'; then
    if [ "$waited" -ge "$LOAD_WAIT_MAX" ]; then setup_fail "1-minute load $load stayed above $LOAD_MAX for ${LOAD_WAIT_MAX}s"; fi
    echo "load $load > $LOAD_MAX: waiting 30s, then measuring again"
    sleep 30
    waited=$((waited + 30))
    continue
  fi
  echo "load $load <= $LOAD_MAX: ok"
  break
done

echo "== pre-check: port $PORT must be free"
if ! port_free; then
  echo "SETUP FAIL: port $PORT is already in use (listener pid: $(listener_pids | tr '\n' ' ')). Stopping; not choosing another port."
  exit 3
fi
echo "port $PORT free"

echo "== run-time identity (JwtSettings values stay in $T, mode 700; values are never printed)"
JWT_SECRET=$(python3 -I -c 'import secrets; print(secrets.token_urlsafe(48))')
JWT_PREV=$(python3 -I -c 'import secrets; print(secrets.token_urlsafe(48))')
JWT_ISS="dl3-smoke-iss-$(python3 -I -c 'import secrets; print(secrets.token_hex(6))')"
JWT_AUD="dl3-smoke-aud-$(python3 -I -c 'import secrets; print(secrets.token_hex(6))')"
( umask 077; printf '%s\n%s\n%s\n%s\n' "$JWT_SECRET" "$JWT_PREV" "$JWT_ISS" "$JWT_AUD" > "$T/sentinels" )
chmod 600 "$T/sentinels"
JWT_ENV=(JwtSettings__Secret="$JWT_SECRET" JwtSettings__PreviousSecrets__0="$JWT_PREV" JwtSettings__Issuer="$JWT_ISS" JwtSettings__Audience="$JWT_AUD")
BASE_ENV=(PATH="$PATH" HOME="$HOME" TMPDIR="${TMPDIR:-/tmp}" LANG=en_US.UTF-8 DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1)
echo "identity ready: 4 random JwtSettings values (secret length ${#JWT_SECRET})"

echo "== build (API project only; a build failure is a setup failure, never a refusal)"
dotnet build "$CSPROJ" -c Debug -nologo -v q > "$T/build.log" 2>&1
build_rc=$?
tail -3 "$T/build.log"
[ "$build_rc" -eq 0 ] || setup_fail "dotnet build exit $build_rc"

# ================================================================================================================ cases
echo "== case 1: Development (JwtSettings from run time; no flags)"
start_host dev ASPNETCORE_ENVIRONMENT=Development
await_outcome
dev_listened=0
if [ "$OUT_STATE" = "listening" ]; then
  dev_listened=1
  check_health 1a "development /health/live" /health/live 200 live none \
    "HTTP 200, status=live, no secret or connection value in the body"
  check_health 1b "development /health/ready" /health/ready 200 ready adapters-on \
    "HTTP 200, status=ready (non-production adapters on), no secret or connection value in the body"
  stop_host
  wait_port_free || setup_fail "port $PORT still in use after case 1"
else
  d=$(describe_outcome)
  record FAIL 1a "development /health/live" "HTTP 200, status=live, no secret or connection value in the body" "$d"
  record FAIL 1b "development /health/ready" "HTTP 200, status=ready (non-production adapters on), no secret or connection value in the body" "$d"
  stop_host
  wait_port_free || setup_fail "port $PORT still in use after case 1"
fi
SCAN_PATTERN=0
if [ "$dev_listened" != "1" ]; then
  record FAIL 1c "development host log" "none of the 4 run-time JwtSettings values appears in the host output" "host never started, so there is no output to scan"
elif scan_file "$HOST_LOG" >/dev/null; then
  record PASS 1c "development host log" "none of the 4 run-time JwtSettings values appears in the host output" "clean ($(wc -l < "$HOST_LOG" | tr -d ' ') log lines scanned)"
else
  record FAIL 1c "development host log" "none of the 4 run-time JwtSettings values appears in the host output" "a run-time JwtSettings value appears in the host output"
fi
SCAN_PATTERN=1

echo "== case 1d: Development, UseNonProductionAdapters=false (AC-8 part 2: /health/ready in every environment)"
start_host devnoadapters ASPNETCORE_ENVIRONMENT=Development Pvg__Runtime__UseNonProductionAdapters=false
await_outcome
if [ "$OUT_STATE" = "listening" ]; then
  check_health 1d "development without adapters /health/ready" /health/ready 503 not-ready codes5 \
    "HTTP 503, status=not-ready, exactly the 5 safe dependency codes, no secret or connection value"
else
  record FAIL 1d "development without adapters /health/ready" \
    "HTTP 503, status=not-ready, exactly the 5 safe dependency codes, no secret or connection value" "$(describe_outcome)"
fi
stop_host
wait_port_free || setup_fail "port $PORT still in use after case 1d"

echo "== case 2: Production without adapters"
refusal_case 2a "production, no flags" "$NOT_AUTH" contains "" ASPNETCORE_ENVIRONMENT=Production
refusal_case 2b "production, authorized + well-formed reference, no adapters (AC-8 part 1)" "$DEP_PHRASE" deps "" \
  ASPNETCORE_ENVIRONMENT=Production Pvg__Runtime__OperationalRuntimeAuthorized=true Pvg__Runtime__ApprovalReference="$GOOD_REF"

echo "== case 3: ASPNETCORE_ENVIRONMENT unset / empty / blank (code: blank = Production, owner decision K3)"
refusal_case 3a "environment variable unset" "$NOT_AUTH" contains ""
refusal_case 3b "environment variable empty" "$NOT_AUTH" contains "" ASPNETCORE_ENVIRONMENT=
refusal_case 3c "environment variable whitespace only" "$NOT_AUTH" contains "" "ASPNETCORE_ENVIRONMENT=   "

echo "== case 3d: 'Production ' with a trailing space (code passes a non-blank name through untrimmed)"
start_host padded "ASPNETCORE_ENVIRONMENT=Production "
await_outcome
if [ "$OUT_STATE" = "listening" ]; then
  check_health 3d "environment 'Production ' (trailing space)" /health/ready 200 ready adapters-on \
    "starts as NOT production-like (PvgServiceApiHost.cs:170-173: name not trimmed): HTTP 200, non-production adapters on"
else
  record FAIL 3d "environment 'Production ' (trailing space)" \
    "starts as NOT production-like (PvgServiceApiHost.cs:170-173: name not trimmed): HTTP 200, non-production adapters on" "$(describe_outcome)"
fi
stop_host
wait_port_free || setup_fail "port $PORT still in use after case 3d"

echo "== case 4: Production, OperationalRuntimeAuthorized=true, approval reference missing or malformed (AC-10)"
BAD_REF="RTA-MOD0230-DL3SMOKE-$(python3 -I -c 'import secrets; print(secrets.token_hex(4))')"
refusal_case 4a "authorized, no approval reference" "$NO_REF" contains "" \
  ASPNETCORE_ENVIRONMENT=Production Pvg__Runtime__OperationalRuntimeAuthorized=true
refusal_case 4b "authorized, whitespace-only approval reference" "$NO_REF" contains "" \
  ASPNETCORE_ENVIRONMENT=Production Pvg__Runtime__OperationalRuntimeAuthorized=true "Pvg__Runtime__ApprovalReference=   "
refusal_case 4c "authorized, malformed approval reference (value must not be echoed)" "$NO_REF" contains "$BAD_REF" \
  ASPNETCORE_ENVIRONMENT=Production Pvg__Runtime__OperationalRuntimeAuthorized=true Pvg__Runtime__ApprovalReference="$BAD_REF"

echo "== case 5: Production, UseNonProductionAdapters=true"
refusal_case 5a "production, adapters flag only (not authorized)" "$NOT_AUTH" contains "" \
  ASPNETCORE_ENVIRONMENT=Production Pvg__Runtime__UseNonProductionAdapters=true
refusal_case 5b "production, authorized + well-formed reference + adapters flag" "$NO_ADAPTERS" contains "" \
  ASPNETCORE_ENVIRONMENT=Production Pvg__Runtime__OperationalRuntimeAuthorized=true Pvg__Runtime__ApprovalReference="$GOOD_REF" \
  Pvg__Runtime__UseNonProductionAdapters=true

echo "== case 6: other start refusals the code has and a smoke can reach"
refusal_case 6a "Development + Pvg:Runtime:ProductionLike=true" "$NOT_AUTH" contains "" \
  ASPNETCORE_ENVIRONMENT=Development Pvg__Runtime__ProductionLike=true
refusal_case 6b "Development + ProductionLike + authorized + well-formed reference (dependency check applies)" "$DEP_PHRASE" deps "" \
  ASPNETCORE_ENVIRONMENT=Development Pvg__Runtime__ProductionLike=true Pvg__Runtime__OperationalRuntimeAuthorized=true \
  Pvg__Runtime__ApprovalReference="$GOOD_REF"
refusal_case 6c "Development + unparsable bool (OperationalRuntimeAuthorized)" "Failed to convert configuration value" contains "" \
  ASPNETCORE_ENVIRONMENT=Development Pvg__Runtime__OperationalRuntimeAuthorized=not-a-bool-dl3
if grep -qF 'not-a-bool-dl3' "$T/host-6c.log" 2>/dev/null; then
  note "6c observation: the unparsable flag value is echoed in the crash output (a flag value, not a secret)"
else
  note "6c observation: the unparsable flag value is not echoed in the crash output"
fi
refusal_case 6d "ASPNETCORE_URLS set but empty (Development)" "Parameter 'urls'" contains "" \
  ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=
refusal_case 6e "ASPNETCORE_URLS with an unsupported scheme (Development; framework refusal, binds nothing)" "" any "" \
  ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=ftp://127.0.0.1:$PORT

# ======================================================================================================== not measured
echo "== not measured (no PASS/FAIL line)"
persist_refs=$(grep -rlE 'Pvg:Persistence|Persistence:Store|PVG_PERSISTENCE' "$API_DIR" --include='*.cs' 2>/dev/null | wc -l | tr -d ' ')
note "NOT REACHABLE  rev-14 persistence refusals (PVG_PERSISTENCE_*; AC-13 (c), AC-35 (d)(e)): $persist_refs files under $API_DIR mention them; the pack describes DL-10, the code at this commit has no such key. A Mongo store would also need a connection, which this smoke must not make."
note "NOT REACHABLE  production-like host that starts: no configuration registers real adapters, and the adapters flag is refused in production-like, so 2b/6b is the only outcome (AC-8 part 1 holds for all five at once)."
note "NOT REACHABLE  one dependency missing out of five: only the test-only configureServices hook can remove a single registration (PvgServiceApiHost.cs:36,51); configuration cannot."
note "NOT REACHABLE  Kestrel 'address already in use': would need another process on $PORT; this script stops instead when the port is busy."
note "NOT A REFUSAL   missing JwtSettings: the key list stays empty and protected endpoints answer 401 (PvgServiceApiHost.cs:104-106)."
note "NOT A HOST TEST AC-9 / the CI scan of ApprovalReference values in committed files: static tests, run with dotnet test."

# ========================================================================================================== wrap-up
stop_host
echo "== post-check: port $PORT must be free"
if port_free; then
  echo "port $PORT free"
else
  echo "FAIL: port $PORT still in use (listener pid: $(listener_pids | tr '\n' ' ')) - not mine to kill by name; check manually"
  fail=1
fi
n_pass=$(grep -c '^PASS$' "$T/verdicts" 2>/dev/null)
n_fail=$(grep -c '^FAIL$' "$T/verdicts" 2>/dev/null)
echo "== cases: ${n_pass:-0} PASS, ${n_fail:-0} FAIL"
cleanup
trap - EXIT
echo "== exit $fail"
exit $fail
