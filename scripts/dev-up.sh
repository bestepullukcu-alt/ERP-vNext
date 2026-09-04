#!/usr/bin/env bash
#
# dev-up.sh — bring the whole local stack up, in one command.
#
# WHY THIS EXISTS. On 2026-09-02 the local services went down three times in one
# session, and each recovery was hand-typed: find the pid, kill it, remember the
# content root, remember the port, wait, poke a health endpoint. Twice the cause
# was misdiagnosed as a code defect before anyone checked whether the process was
# even listening.
#
# ⚠ THE TRAP THIS SCRIPT EXISTS TO CLOSE. A .NET service that started BEFORE the
# current mongod does not fail loudly — its Mongo driver keeps a topology view the
# server no longer matches, and EVERY query then waits the full 30-second server
# selection timeout before giving up. The symptom looks like a frontend bug: a
# screen that never finishes loading. Measured on 2026-09-02: an MDM process from
# 26 August against a mongod restarted on 1 September answered
# /api/legal-entities/{id}/lookup-validation in 30 021 ms, which froze the Task
# Center's create panel. So this script compares every service's start time
# against mongod's and says so out loud.
#
# USAGE
#   scripts/dev-up.sh              start whatever is not already listening
#   scripts/dev-up.sh --restart    stop everything first, then start it all
#   scripts/dev-up.sh --status     report only; start nothing
#
# Logs go to /private/tmp/dev-<name>.log — one per service.
#
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
LOG_DIR="/private/tmp"
MODE="start"
case "${1:-}" in
  --restart) MODE="restart" ;;
  --status)  MODE="status" ;;
  --help|-h) sed -n '2,30p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
  "")        ;;
  *)         echo "unknown option: $1 (try --help)" >&2; exit 2 ;;
esac

# name | port | project dir (relative to repo root) | binary | health path
#
# ORDER MATTERS: auth issues the tokens, platform and the domain services answer
# behind the gateway, and web is last because it is the only one a person opens.
# Nothing here polls its dependencies, so a service started too early simply logs
# a refused connection and recovers — but starting in this order avoids the noise.
SERVICES=(
  "auth|5056|services/Diten.AuthService/src/Diten.AuthService.Api|Diten.AuthService.Api|/health/live"
  "platform|5057|services/Diten.Platform/src/Diten.Platform.API|Diten.Platform.API|/health/live"
  "deven|5058|services/Diten.DevEnablementService/src/Diten.DevEnablementService.Api|Diten.DevEnablementService.Api|/health/live"
  "mdm|5059|services/Diten.MdmService/src/Diten.MdmService.Api|Diten.MdmService.Api|/api/legal-entities"
  "hcm|5060|services/Diten.HcmService/src/Diten.HcmService.Api|Diten.HcmService.Api|/health/live"
  "gateway|5000|gateway/Diten.ApiGateway|Diten.ApiGateway|/health/live"
  "web|5001|frontend/Diten.Web|Diten.Web|/account/login"
)

listening_pid() { lsof -nP -iTCP:"$1" -sTCP:LISTEN -t 2>/dev/null | head -1; }

# Any HTTP answer means the process is up. 401/400/404 are answers; only a
# connection failure (curl writes 000) is not.
probe() { curl -s -o /dev/null -m 4 -w '%{http_code}' "http://localhost:$1$2" 2>/dev/null; }

started_at() { ps -o lstart= -p "$1" 2>/dev/null | sed 's/  */ /g'; }
started_epoch() {
  local s; s="$(started_at "$1")"; [ -z "$s" ] && return 1
  date -j -f "%a %b %e %T %Y" "$s" "+%s" 2>/dev/null
}

mongod_epoch() {
  local up; up="$(mongosh --quiet --eval 'print(db.serverStatus().uptime)' 2>/dev/null | tr -d '[:space:]')"
  [[ "$up" =~ ^[0-9]+$ ]] || return 1
  echo $(( $(date +%s) - up ))
}

start_one() {
  local name=$1 port=$2 dir=$3 bin=$4
  local abs="$ROOT/$dir"
  local exe="$abs/bin/Debug/net8.0/$bin"
  local dll="$exe.dll"

  # The content root is the PROJECT directory, not the repo root: appsettings.json
  # is resolved relative to it, and starting from anywhere else dies with
  # "Configuration error: 'Mongo:ConnectionString' is missing." (measured).
  # ⚠ `exec` IS WHAT ACTUALLY DETACHES, AND IT TOOK TWO TRIES TO GET RIGHT.
  #
  # The child's own stdout goes to the log, so it holds nothing — but the SUBSHELL
  # that launches it inherits this script's stdout, and that is enough. Anything
  # reading this script through a pipe (`dev-up.sh | tail`, a CI step, an agent's
  # command runner) then waits for EOF that never comes, because a shell is still
  # alive holding the write end. Measured: the first version appeared to run for ten
  # minutes while all seven services were already up and answering in seconds.
  #
  # `exec` REPLACES the subshell with the service, so no shell survives to hold the
  # descriptor. `< /dev/null` and `disown` close the remaining edges: no terminal
  # read, no job-control entry.
  if [ -x "$exe" ]; then
    ( cd "$abs" && exec env ASPNETCORE_ENVIRONMENT=Development "$exe" \
        --urls "http://localhost:$port" > "$LOG_DIR/dev-$name.log" 2>&1 < /dev/null ) &
    disown
  elif [ -f "$dll" ]; then
    ( cd "$abs" && exec env ASPNETCORE_ENVIRONMENT=Development dotnet exec "$dll" \
        --urls "http://localhost:$port" > "$LOG_DIR/dev-$name.log" 2>&1 < /dev/null ) &
    disown
  else
    echo "   $name — not built (dotnet build $dir)"
    return 1
  fi
}

# ── mongod ───────────────────────────────────────────────────────────────────
MONGO_EPOCH=""
if MONGO_EPOCH="$(mongod_epoch)"; then
  echo "mongod  up since $(date -r "$MONGO_EPOCH" '+%Y-%m-%d %H:%M:%S')"
else
  echo "mongod  ⚠ NOT REACHABLE — every service below will hang on its first query."
  echo "        start it, then run this again."
fi
echo

# ── stop, if asked ───────────────────────────────────────────────────────────
if [ "$MODE" = "restart" ]; then
  for row in "${SERVICES[@]}"; do
    IFS='|' read -r name port _ _ _ <<< "$row"
    pid="$(listening_pid "$port")"
    [ -n "$pid" ] && { kill "$pid" 2>/dev/null; echo "stopped $name ($pid)"; }
  done
  sleep 4
  for row in "${SERVICES[@]}"; do
    IFS='|' read -r _ port _ _ _ <<< "$row"
    pid="$(listening_pid "$port")"
    [ -n "$pid" ] && kill -9 "$pid" 2>/dev/null
  done
  sleep 1
  echo
fi

# ── start ────────────────────────────────────────────────────────────────────
if [ "$MODE" != "status" ]; then
  for row in "${SERVICES[@]}"; do
    IFS='|' read -r name port dir bin _ <<< "$row"
    if [ -n "$(listening_pid "$port")" ]; then
      continue                    # already up; leave it alone
    fi
    start_one "$name" "$port" "$dir" "$bin"
  done

  # One shared wait rather than a wait per service: they boot in parallel and the
  # slowest sets the pace.
  for _ in $(seq 1 30); do
    sleep 2
    all_up=1
    for row in "${SERVICES[@]}"; do
      IFS='|' read -r _ port _ _ health <<< "$row"
      [ "$(probe "$port" "$health")" = "000" ] && { all_up=0; break; }
    done
    [ "$all_up" = "1" ] && break
  done
  echo
fi

# ── report ───────────────────────────────────────────────────────────────────
printf "%-9s %-6s %-8s %-7s %s\n" "SERVICE" "PORT" "PID" "HTTP" "STARTED"
stale_found=0
for row in "${SERVICES[@]}"; do
  IFS='|' read -r name port _ _ health <<< "$row"
  pid="$(listening_pid "$port")"
  code="$(probe "$port" "$health")"
  when="—"
  flag=""
  if [ -n "$pid" ]; then
    when="$(started_at "$pid")"
    if [ -n "$MONGO_EPOCH" ] && svc_epoch="$(started_epoch "$pid")"; then
      if [ "$svc_epoch" -lt "$MONGO_EPOCH" ]; then
        flag="  ⚠ STARTED BEFORE mongod — restart it (30s hangs)"
        stale_found=1
      fi
    fi
  fi
  [ "$code" = "000" ] && code="DOWN"
  printf "%-9s %-6s %-8s %-7s %s%s\n" "$name" "$port" "${pid:-—}" "$code" "$when" "$flag"
done

if [ "$stale_found" = "1" ]; then
  echo
  echo "⚠ At least one service predates mongod. Its Mongo driver still holds the old"
  echo "  topology, so every query waits out a 30-second server-selection timeout and"
  echo "  the failure surfaces as a screen that never loads. Fix with:"
  echo "      scripts/dev-up.sh --restart"
fi
