#!/usr/bin/env bash
# WP-0230-DL8 live smoke (AC-16 + "Gateway üzerinden smoke çağrısı servisin sonucunu döndürür").
# Run from the repository root, normally through scripts/run_record.py.
# Starts the PVG service (5011) and the API Gateway (5000) from this checkout, sends the same GET to both,
# compares status and body, checks the four MOD-0230 route entries in ocelot.json, then stops ONLY the two
# processes it started (by PID) and checks that both ports are free again. Exit 0 = smoke passed.
set -u

PVG_PORT=5011
GW_PORT=5000
fail=0
pvg_pid=""
gw_pid=""
T=$(mktemp -d)

stop_own() {
  for pid in "$gw_pid" "$pvg_pid"; do
    if [ -n "$pid" ] && kill -0 "$pid" 2>/dev/null; then
      echo "stop: kill -TERM $pid ($(ps -o lstart= -p "$pid" 2>/dev/null | xargs))"
      kill -TERM "$pid" 2>/dev/null
    fi
  done
  sleep 3
  for pid in "$gw_pid" "$pvg_pid"; do
    if [ -n "$pid" ] && kill -0 "$pid" 2>/dev/null; then
      echo "stop: still running $pid -> kill -KILL"
      kill -KILL "$pid" 2>/dev/null
    fi
  done
}
trap stop_own EXIT

echo "== pre-check: ports must be free"
for p in $PVG_PORT $GW_PORT; do
  if lsof -nP -iTCP:$p -sTCP:LISTEN >/dev/null 2>&1; then
    echo "FAIL: port $p already in use"; exit 3
  fi
  echo "port $p free"
done

echo "== AC-16: MOD-0230 route entries in gateway/Diten.ApiGateway/ocelot.json"
python3 -I - <<'PY' || fail=1
import json, sys
routes = json.load(open("gateway/Diten.ApiGateway/ocelot.json"))["Routes"]
mine = [r for r in routes if "pv-case-intake-triage" in r.get("UpstreamPathTemplate", "")
        or any(h.get("Port") == 5011 for h in r.get("DownstreamHostAndPorts", []))]
expected = {
    "/api/pv-case-intake-triage": ["GET", "POST"],
    "/api/pv-case-intake-triage/{intakeDraftId}": ["GET", "PUT"],
    "/api/pv-case-intake-triage/{intakeDraftId}/triage": ["POST"],
    "/api/pv-case-intake-triage/{intakeDraftId}/route": ["POST"],
}
got = {r["UpstreamPathTemplate"]: sorted(r.get("UpstreamHttpMethod", [])) for r in mine}
for k, v in got.items():
    print("route", k, v)
ok = len(mine) == 4 and got == {k: sorted(v) for k, v in expected.items()}
bad = [k for k in got if any(w in k.lower() for w in ("archive", "export", "delete", "bulk"))]
bad += [k for k, v in got.items() if "DELETE" in v]
print("route count", len(mine), "matches expected" if ok else "DOES NOT match expected")
print("forbidden routes", bad if bad else "none")
sys.exit(0 if ok and not bad else 1)
PY

echo "== build"
dotnet build services/Diten.PvgService/src/Diten.PvgService.Api/Diten.PvgService.Api.csproj -c Debug -nologo -v q 2>&1 | tail -3
dotnet build gateway/Diten.ApiGateway/Diten.ApiGateway.csproj -c Debug -nologo -v q 2>&1 | tail -3

pvg_dll=$(ls services/Diten.PvgService/src/Diten.PvgService.Api/bin/Debug/net8.0/Diten.PvgService.Api.dll)
gw_dll=$(ls gateway/Diten.ApiGateway/bin/Debug/net8.0/Diten.ApiGateway.dll)

echo "== start PVG service on $PVG_PORT (Development)"
ASPNETCORE_ENVIRONMENT=Development dotnet "$pvg_dll" > "$T/pvg.log" 2>&1 &
pvg_pid=$!
echo "pvg pid $pvg_pid"

echo "== start Gateway on $GW_PORT (Development)"
( cd gateway/Diten.ApiGateway && ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://localhost:$GW_PORT dotnet "bin/Debug/net8.0/Diten.ApiGateway.dll" ) > "$T/gw.log" 2>&1 &
gw_pid=$!
echo "gateway pid $gw_pid"

wait_port() {
  for i in $(seq 1 60); do
    if lsof -nP -iTCP:$1 -sTCP:LISTEN >/dev/null 2>&1; then echo "port $1 listening after ${i}s"; return 0; fi
    sleep 1
  done
  echo "FAIL: port $1 not listening after 60s"; return 1
}
wait_port $PVG_PORT || fail=1
wait_port $GW_PORT || fail=1

echo "== request 1: through the Gateway"
gw_status=$(curl -s -o "$T"/gw-body.txt -w '%{http_code}' "http://localhost:$GW_PORT/api/pv-case-intake-triage")
echo "status $gw_status"; echo "body:"; cat "$T"/gw-body.txt; echo

echo "== request 2: directly to the service"
svc_status=$(curl -s -o "$T"/svc-body.txt -w '%{http_code}' "http://127.0.0.1:$PVG_PORT/api/v1/pv-case-intake-triage")
echo "status $svc_status"; echo "body:"; cat "$T"/svc-body.txt; echo

if [ "$gw_status" = "$svc_status" ] && cmp -s "$T"/gw-body.txt "$T"/svc-body.txt && [ "$gw_status" != "000" ]; then
  echo "RESULT: Gateway returned the service's result (same status, byte-identical body)"
else
  echo "RESULT: MISMATCH"; fail=1
fi

echo "== gateway log lines for the route"
grep -E "pv-case-intake-triage|5011" "$T"/gw.log | sed -E 's/[0-9a-f]{8}-[0-9a-f-]{27}/<id>/g' | head -10

stop_own
trap - EXIT
echo "== post-check: ports must be free"
for p in $PVG_PORT $GW_PORT; do
  if lsof -nP -iTCP:$p -sTCP:LISTEN >/dev/null 2>&1; then echo "FAIL: port $p still in use"; fail=1; else echo "port $p free"; fi
done

echo "== exit $fail"
exit $fail
