#!/bin/zsh
# Q381: install Program.cs variant $1, build, start Development on 58381, probe 10 requests, stop.
E=${0:A:h}; V=$1; LABEL=${2:-$1}; A=$E/src/services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api; O=$E/runs/$LABEL; mkdir -p $O
cp $E/variants/Program.$V.cs $A/Program.cs; shasum -a 256 $A/Program.cs | awk '{print "installed Program.cs " $1}' | tee $O/installed.txt
(cd $A && dotnet build -nodeReuse:false -p:UseSharedCompilation=false -v q > $O/build.log 2>&1); echo "build rc=$? $(grep -E 'Warning\(s\)|Error\(s\)' $O/build.log | tr -s ' ' | tr '\n' ' ')"
SECRET=$(openssl rand -hex 32); T=$(uuidgen | tr A-Z a-z); L=$(uuidgen | tr A-Z a-z)
TOKEN=$(SECRET=$SECRET T=$T L=$L python3 -c '
import os,json,hmac,hashlib,base64,time,uuid
b=lambda x:base64.urlsafe_b64encode(x).rstrip(b"=").decode(); n=int(time.time())
h=b(json.dumps({"alg":"HS256","typ":"JWT"}).encode())
p=b(json.dumps({"iss":"q381","aud":"q381","sub":str(uuid.uuid4()),"tenant_id":os.environ["T"],"legal_entity_id":os.environ["L"],
 "permission":["supplychain.capacity-plans.read","supplychain.capacity-plans.create","supplychain.sandop-plans.read","supplychain.sandop-plans.create","supplychain.shipments.read"],
 "iat":n,"nbf":n,"exp":n+900}).encode())
print(h+"."+p+"."+b(hmac.new(os.environ["SECRET"].encode(),(h+"."+p).encode(),hashlib.sha256).digest()))')
(cd $A && ASPNETCORE_ENVIRONMENT=Development DOTNET_ENVIRONMENT=Development \
  Mongo__ConnectionString='mongodb://127.0.0.1:57381/?replicaSet=rsq381&serverSelectionTimeoutMS=5000' Mongo__DatabaseName=diten_q381_$LABEL \
  JwtSettings__Secret=$SECRET JwtSettings__Issuer=q381 JwtSettings__Audience=q381 PlatformRegistration__BaseUrl= \
  exec dotnet bin/Debug/net8.0/Diten.SupplyChainService.Api.dll --urls http://127.0.0.1:58381 > $O/service.log 2>&1) &
PID=$!; up=0; for i in $(seq 1 60); do sleep 0.5; kill -0 $PID 2>/dev/null || break; curl -s -o /dev/null -w '%{http_code}' http://127.0.0.1:58381/health | grep -q 200 && { up=1; break; }; done
echo "boot: $([ $up = 1 ] && echo up || echo FAILED)"
req() { # name method path auth
  local id=$(uuidgen | tr A-Z a-z); local args=(-s -o $O/$1.body -w '%{http_code}' -X $2 -H "X-Correlation-Id: $id")
  [ $4 = auth ] && args+=(-H "Authorization: Bearer $TOKEN" -H "X-Tenant-Id: $T" -H "X-Legal-Entity-Id: $L")
  [ $2 = POST ] && args+=(-H "Content-Type: application/json" -H "Idempotency-Key: k-$id" -d '{}')
  local code=$(curl "${args[@]}" http://127.0.0.1:58381$3)
  printf '%-22s %-4s %-52s %s %s\n' $1 $2 $3 $code "$(head -c 160 $O/$1.body | tr '\n' ' ')"
}
if [ $up = 1 ]; then
  G=$(uuidgen | tr A-Z a-z)
  { req cap-get-noauth GET /api/supply-chain/capacity-plans/$G none
    req cap-get-auth GET /api/supply-chain/capacity-plans/$G auth
    req cap-post-noauth POST /api/supply-chain/capacity-plans none
    req cap-post-auth POST /api/supply-chain/capacity-plans auth
    req sop-get-noauth GET /api/supply-chain/sandop-plans/$G none
    req sop-get-auth GET /api/supply-chain/sandop-plans/$G auth
    req sop-post-noauth POST /api/supply-chain/sandop-plans none
    req sop-post-auth POST /api/supply-chain/sandop-plans auth
    req ship-list-auth GET /api/shipment-bundle/shipments auth
    req ship-list-noauth GET /api/shipment-bundle/shipments none
    req carriers-auth GET /api/shipment-bundle/carriers auth
    req loads-noauth GET /api/shipment-bundle/loads none
    req returns-noauth GET /api/shipment-bundle/returns none
    req claims-noauth GET /api/shipment-bundle/claims none
    req unknown-noauth GET /api/supply-chain/nothing-here none; } | tee $O/probes.txt
  echo "unhandled exceptions in log: $(grep -c 'An unhandled exception\|Unhandled exception' $O/service.log)"; grep -m3 -oE '(InvalidOperationException|Unable to resolve)[^.]{0,180}' $O/service.log | sort | uniq -c
  kill -INT $PID; for i in $(seq 1 20); do sleep 0.5; kill -0 $PID 2>/dev/null || break; done; kill -0 $PID 2>/dev/null && kill -TERM $PID
fi
wait $PID 2>/dev/null; nc -z 127.0.0.1 58381 && echo "58381 STILL OPEN" || echo "58381 free"
unset SECRET TOKEN
