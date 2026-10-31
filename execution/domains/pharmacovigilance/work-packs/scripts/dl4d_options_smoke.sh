#!/usr/bin/env bash
# WP-0230-DL4D live smoke (AC-34, card S-83: written by the CT, not by the worker).
# Run from the repository root, normally through scripts/run_record.py.
# Starts the PVG service (5011) and the API Gateway (5000) from this checkout with a JWT signing secret generated
# at run time (never printed, never written to disk outside the private temp dir, never committed), then measures:
#   1. Gateway GET /api/pv-case-intake-triage/options without a token -> 401
#   2. Gateway GET .../options with a valid tenant_user token -> 200, exactly reportTypes, productRoles,
#      unknownCountry, lists in StringComparer.Ordinal order of PvgIntakeOptionSets (read from the source file)
#   3. the same request straight to the service (X-Tenant-Id = token tenant) -> same status, byte-identical body
#   4. service, missing X-Tenant-Id and X-Tenant-Id != token tenant_id -> options status + reasonCode equal the
#      list endpoint's for the same request (the Gateway refuses these itself, so they are measured on the service)
#   5. Gateway PUT .../options with a valid token and body -> 404 (reserved segment)
# then stops ONLY the two processes it started (by PID) and checks that both ports are free again.
# Exit 0 = every check passed; 1 = a check failed; 3 = setup failed (port busy, build failed, a started process
# never listened on its port - interim review 03 F-3, or a request got no HTTP answer at all, status 000 - interim
# review 04 F-3: a started process that stops answering is a setup failure, never read as the red baseline).
# Red baseline (before WP-0230-DL4D): check 2 fails - GET options is read as an intake draft id, goes to
# GetDraftByIdAsync and answers 409 (Blocked or PVG_INTAKE_DRAFT_NOT_FOUND; ToHttpResult maps everything but
# Succeeded / Invalid to 409), not 200; the Gateway route only maps path and method and passes the status through.
# Check 5 also fails (options is not yet a reserved segment). So the script exits 1.
# Hardening (interim review 02 F-5): build exit codes are checked, every curl has --max-time, the bearer token is
# passed to curl from a header file (-H @file), never as an argument, and the secret / token files are removed on
# every exit by the EXIT trap, which removes the whole mktemp directory (interim review 03 F-3).
set -u

PVG_PORT=5011
GW_PORT=5000
fail=0
conn_fail=0
pvg_pid=""
gw_pid=""
T=$(mktemp -d)
chmod 700 "$T"

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
cleanup() {
  stop_own
  if [ -n "$T" ] && [ -d "$T" ]; then rm -rf -- "$T"; fi
}
trap cleanup EXIT

check() { # name, condition result (0 = pass)
  if [ "$2" = "0" ]; then echo "PASS $1"; else echo "FAIL $1"; fail=1; fi
}

echo "== pre-check: ports must be free"
for p in $PVG_PORT $GW_PORT; do
  if lsof -nP -iTCP:$p -sTCP:LISTEN >/dev/null 2>&1; then
    echo "FAIL: port $p already in use"; exit 3
  fi
  echo "port $p free"
done

echo "== run-time identity (secret and tokens stay in $T, mode 700; values are not printed)"
python3 -I - "$T" <<'PY'
import base64, hashlib, hmac, json, secrets, sys, time, uuid, pathlib
d = pathlib.Path(sys.argv[1])
secret = secrets.token_urlsafe(48)
iss, aud = "dl4d-smoke-issuer", "dl4d-smoke-audience"
tenant_a, tenant_b = str(uuid.uuid4()), str(uuid.uuid4())
def b64(b): return base64.urlsafe_b64encode(b).rstrip(b"=").decode()
def token(tenant):
    now = int(time.time())
    head = b64(json.dumps({"alg": "HS256", "typ": "JWT"}).encode())
    body = b64(json.dumps({"sub": "dl4d-smoke-actor", "tenant_id": tenant, "actor_type": "tenant_user",
                           "iss": iss, "aud": aud, "iat": now, "nbf": now, "exp": now + 900}).encode())
    sig = b64(hmac.new(secret.encode(), f"{head}.{body}".encode(), hashlib.sha256).digest())
    return f"{head}.{body}.{sig}"
(d / "secret").write_text(secret)
(d / "iss").write_text(iss)
(d / "aud").write_text(aud)
(d / "tenant_a").write_text(tenant_a)
(d / "tenant_b").write_text(tenant_b)
(d / "auth_a").write_text("Authorization: Bearer " + token(tenant_a) + "\n")
for f in ("secret", "auth_a"):
    (d / f).chmod(0o600)
print("identity ready: two random tenants, one HS256 token, secret length", len(secret))
PY
TENANT_A=$(cat "$T/tenant_a")
TENANT_B=$(cat "$T/tenant_b")

echo "== build"
build() { # project path; the exit code is not swallowed by a pipe
  local log="$T/build-$(basename "$1").log"
  dotnet build "$1" -c Debug -nologo -v q > "$log" 2>&1
  local rc=$?
  tail -3 "$log"
  if [ "$rc" -ne 0 ]; then echo "FAIL: build $1 exit $rc"; exit 3; fi
}
build services/Diten.PvgService/src/Diten.PvgService.Api/Diten.PvgService.Api.csproj
build gateway/Diten.ApiGateway/Diten.ApiGateway.csproj

pvg_dll=$(ls services/Diten.PvgService/src/Diten.PvgService.Api/bin/Debug/net8.0/Diten.PvgService.Api.dll)

echo "== start PVG service on $PVG_PORT (Development, run-time JwtSettings)"
JwtSettings__Secret="$(cat "$T/secret")" JwtSettings__Issuer="$(cat "$T/iss")" JwtSettings__Audience="$(cat "$T/aud")" \
  ASPNETCORE_ENVIRONMENT=Development dotnet "$pvg_dll" > "$T/pvg.log" 2>&1 &
pvg_pid=$!
echo "pvg pid $pvg_pid"

echo "== start Gateway on $GW_PORT (Development, same run-time JwtSettings)"
( cd gateway/Diten.ApiGateway && JwtSettings__Secret="$(cat "$T/secret")" JwtSettings__Issuer="$(cat "$T/iss")" \
  JwtSettings__Audience="$(cat "$T/aud")" ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://localhost:$GW_PORT \
  exec dotnet "bin/Debug/net8.0/Diten.ApiGateway.dll" ) > "$T/gw.log" 2>&1 &
gw_pid=$!
echo "gateway pid $gw_pid (exec: the subshell becomes the dotnet process)"

wait_port() {
  for i in $(seq 1 60); do
    if lsof -nP -iTCP:$1 -sTCP:LISTEN >/dev/null 2>&1; then echo "port $1 listening after ${i}s"; return 0; fi
    sleep 1
  done
  echo "FAIL: port $1 not listening after 60s"; return 1
}
wait_port $PVG_PORT || exit 3
wait_port $GW_PORT || exit 3

GW="http://localhost:$GW_PORT/api/pv-case-intake-triage"
SVC="http://127.0.0.1:$PVG_PORT/api/v1/pv-case-intake-triage"
AUTH_A="@$T/auth_a"   # curl reads the header from the file; the token never appears in an argument list

# curl_to <out-body> <args...> -> prints the status code
curl_to() {
  local out=$1; shift
  local s
  s=$(curl -s --max-time 20 -o "$out" -w '%{http_code}' "$@")
  echo "$s" >> "$T/statuses"
  printf '%s' "$s"
}

echo "== 1. Gateway GET options without a token"
s1=$(curl_to "$T/b1" "$GW/options")
echo "status $s1"
check "1 no token -> 401" "$([ "$s1" = "401" ]; echo $?)"

echo "== 2. Gateway GET options with a valid tenant_user token"
s2=$(curl_to "$T/b2" -H "$AUTH_A" "$GW/options")
echo "status $s2"; echo "body:"; cat "$T/b2"; echo
python3 -I - "$T/b2" services/Diten.PvgService/src/Diten.PvgService.Domain/RegPvBase/PvgIntakeOptionSets.cs <<'PY'
import json, re, sys
body, src = open(sys.argv[1], encoding="utf-8").read(), open(sys.argv[2], encoding="utf-8").read()
def block(name):
    m = re.search(name + r"\s*\{[^}]*\}\s*=\s*new HashSet<string>\(StringComparer\.Ordinal\)\s*\{(.*?)\};", src, re.S)
    return re.findall(r'"([^"]*)"', m.group(1)) if m else None
unknown = re.search(r'UnknownCountry\s*=\s*"([^"]*)"', src)
expected = {"reportTypes": sorted(block("ReportTypes") or [], key=lambda s: s.encode("utf-16-be")),
            "productRoles": sorted(block("ProductRoles") or [], key=lambda s: s.encode("utf-16-be")),
            "unknownCountry": unknown.group(1) if unknown else None}
print("expected from PvgIntakeOptionSets.cs:", json.dumps(expected))
try:
    got = json.loads(body)
except ValueError:
    print("body is not JSON"); sys.exit(1)
ok = (isinstance(got, dict) and list(got.keys()) == ["reportTypes", "productRoles", "unknownCountry"]
      and got == expected and expected["reportTypes"] and expected["productRoles"])
print("members", list(got.keys()) if isinstance(got, dict) else type(got).__name__, "->", "match" if ok else "MISMATCH")
sys.exit(0 if ok else 1)
PY
r2=$?
check "2 valid token -> 200 and exactly the PvgIntakeOptionSets codes in ordinal order" "$([ "$s2" = "200" ] && [ "$r2" = "0" ]; echo $?)"
for t in "$TENANT_A" "$TENANT_B"; do
  if grep -q "$t" "$T/b2"; then echo "FAIL 2 body carries a tenant id"; fail=1; fi
done

echo "== 3. the same request straight to the service"
s3=$(curl_to "$T/b3" -H "$AUTH_A" -H "X-Tenant-Id: $TENANT_A" -H "X-Correlation-Id: dl4d-smoke-3" "$SVC/options")
echo "status $s3"
check "3 Gateway returns the service's result (status + byte-identical body)" \
  "$([ "$s3" = "$s2" ] && cmp -s "$T/b2" "$T/b3"; echo $?)"

echo "== 4. tenant refusal equals the list endpoint's (service)"
for case in missing different; do
  if [ "$case" = "missing" ]; then hdr=(); else hdr=(-H "X-Tenant-Id: $TENANT_B"); fi
  so=$(curl_to "$T/o-$case" -H "$AUTH_A" -H "X-Correlation-Id: dl4d-smoke-4" ${hdr[@]+"${hdr[@]}"} "$SVC/options")
  sl=$(curl_to "$T/l-$case" -H "$AUTH_A" -H "X-Correlation-Id: dl4d-smoke-4" ${hdr[@]+"${hdr[@]}"} "$SVC")
  ro=$(python3 -I -c 'import json,sys
try: print(json.load(open(sys.argv[1])).get("reasonCode") or "")
except Exception: print("")' "$T/o-$case")
  rl=$(python3 -I -c 'import json,sys
try: print(json.load(open(sys.argv[1])).get("reasonCode") or "")
except Exception: print("")' "$T/l-$case")
  echo "tenant $case: options $so $ro | list $sl $rl"
  codes=$(grep -cE '"(Spontaneous|Study|Literature|Solicited|Other|Suspect|Concomitant)"' "$T/o-$case")
  check "4 tenant $case -> refused like the list endpoint, no codes" \
    "$([ "$so" = "$sl" ] && [ "$ro" = "$rl" ] && [ -n "$ro" ] && [ "$so" != "200" ] && [ "$codes" = "0" ]; echo $?)"
done

echo "== 5. Gateway PUT options (reserved segment)"
s5=$(curl_to "$T/b5" -X PUT -H "$AUTH_A" -H "Content-Type: application/json" \
  -d '{"expectedVersion":1,"intakeChannel":"Email","sourceType":"Spontaneous","reportType":"Spontaneous"}' "$GW/options")
echo "status $s5"
check "5 PUT options -> 404" "$([ "$s5" = "404" ]; echo $?)"
if grep -qx "000" "$T/statuses"; then
  conn_fail=1
  echo "SETUP FAIL: a request got no HTTP answer (status 000) - a started process stopped answering"
fi

echo "== gateway log lines for the route"
grep -E "pv-case-intake-triage|5011" "$T"/gw.log | sed -E 's/[0-9a-f]{8}-[0-9a-f-]{27}/<id>/g' | head -10

cleanup
trap - EXIT
echo "== post-check: ports must be free"
for p in $PVG_PORT $GW_PORT; do
  if lsof -nP -iTCP:$p -sTCP:LISTEN >/dev/null 2>&1; then echo "FAIL: port $p still in use"; fail=1; else echo "port $p free"; fi
done

if [ "$conn_fail" = "1" ]; then echo "== exit 3"; exit 3; fi
echo "== exit $fail"
exit $fail
