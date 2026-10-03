#!/usr/bin/env python3
import base64, hashlib, hmac, json, os, time, urllib.error, urllib.request, uuid

BASE=os.environ.get("CAPACITY_BASE","http://127.0.0.1:56192")
SECRET=os.environ["CAPACITY_JWT_SECRET"].encode()
ISSUER=os.environ.get("CAPACITY_JWT_ISSUER","mod0192-evidence")
AUDIENCE=os.environ.get("CAPACITY_JWT_AUDIENCE","mod0192-api")
TENANT="19200000-0000-4000-8000-000000000001"
LE="19200000-0000-4000-8000-000000000002"
ACTOR="19200000-0000-4000-8000-000000000004"
PERMS=["supplychain.capacity-plans.read","supplychain.capacity-plans.create","supplychain.capacity-plans.scenario.create","supplychain.capacity-plans.evaluate"]

def b64(data): return base64.urlsafe_b64encode(data).rstrip(b"=").decode()
def token(tenant=TENANT,le=LE,permissions=PERMS):
    header={"alg":"HS256","typ":"JWT"}
    payload={"iss":ISSUER,"aud":AUDIENCE,"exp":int(time.time())+3600,"iat":int(time.time()),"sub":ACTOR,
             "tenant_id":tenant,"legal_entity_id":le,"permission":permissions}
    signing=b64(json.dumps(header,separators=(",",":")).encode())+"."+b64(json.dumps(payload,separators=(",",":")).encode())
    return signing+"."+b64(hmac.new(SECRET,signing.encode(),hashlib.sha256).digest())

def call(name,method,path,expected,body=None,key=None,corr=None,bearer="valid"):
    headers={"Accept":"application/json"}
    if bearer=="valid": headers["Authorization"]="Bearer "+token()
    elif bearer=="foreign": headers["Authorization"]="Bearer "+token("29200000-0000-4000-8000-000000000001")
    elif bearer=="noperm": headers["Authorization"]="Bearer "+token(permissions=[])
    if corr is not None: headers["X-Correlation-Id"]=corr
    if key is not None: headers["Idempotency-Key"]=key
    data=None
    if body is not None:
        data=json.dumps(body,separators=(",",":")).encode();headers["Content-Type"]="application/json"
    req=urllib.request.Request(BASE+path,data=data,headers=headers,method=method)
    try:
        with urllib.request.urlopen(req,timeout=10) as res:
            status=res.status;raw=res.read().decode();rh=dict(res.headers)
    except urllib.error.HTTPError as exc:
        status=exc.code;raw=exc.read().decode();rh=dict(exc.headers)
    parsed=json.loads(raw) if raw else None
    record={"test":name,"method":method,"path":path,"status":status,"expected":expected,
            "correlation":rh.get("X-Correlation-Id"),"body":parsed}
    print(json.dumps(record,separators=(",",":"),sort_keys=True),flush=True)
    assert status==expected,(name,status,raw)
    return parsed,rh

corr1=str(uuid.uuid4())
plan={"name":"FY2027 Capacity Baseline","horizonStart":"2027-01-01","horizonEnd":"2027-12-31",
      "demandPlanId":"dp-2027","demandPlanVersion":"3","sourceCapturedAt":"2026-09-22T20:00:00Z",
      "sourceChecksum":"sha256:ee56d4f9a3c8"}
call("unauthenticated", "POST","/api/supply-chain/capacity-plans",401,plan,"plan-http-key",corr1,bearer="none")
call("permission-denied","POST","/api/supply-chain/capacity-plans",403,plan,"plan-http-key",corr1,bearer="noperm")
call("invalid-correlation","POST","/api/supply-chain/capacity-plans",400,plan,"plan-http-key","not-a-uuid")
created,h1=call("create-plan","POST","/api/supply-chain/capacity-plans",201,plan,"plan-http-key",corr1)
plan_id=created["capacityPlanId"]
corr2=str(uuid.uuid4())
replayed,h2=call("replay-plan","POST","/api/supply-chain/capacity-plans",201,plan,"plan-http-key",corr2)
assert replayed==created and h1["X-Correlation-Id"]==corr1 and h2["X-Correlation-Id"]==corr2
changed=dict(plan);changed["name"]="Changed"
conflict,_=call("changed-plan-payload","POST","/api/supply-chain/capacity-plans",409,changed,"plan-http-key",str(uuid.uuid4()))
assert conflict["error"]["code"]=="IDEMPOTENCY_KEY_REUSED"
call("get-plan","GET",f"/api/supply-chain/capacity-plans/{plan_id}",200,corr=str(uuid.uuid4()))
call("foreign-plan-hidden","GET",f"/api/supply-chain/capacity-plans/{plan_id}",404,corr=str(uuid.uuid4()),bearer="foreign")
call("query-scope-override","GET",f"/api/supply-chain/capacity-plans/{plan_id}?tenantId={TENANT}",400,corr=str(uuid.uuid4()))

scenario={"name":"Add weekend shift","constraintRefs":[{"constraintId":"line-4-hours","source":"SUPPLY-CONSTRAINTS","sourceVersion":"8"}],
          "adjustments":[{"resourceRef":"line-4","period":"2027-W03","availableCapacityDelta":"80.000","uomId":"HOUR"}]}
scenario_created,_=call("create-scenario","POST",f"/api/supply-chain/capacity-plans/{plan_id}/scenarios",201,scenario,"scenario-http-key",str(uuid.uuid4()))
scenario_id=scenario_created["scenarioId"]
scenario_replay,_=call("replay-scenario","POST",f"/api/supply-chain/capacity-plans/{plan_id}/scenarios",201,scenario,"scenario-http-key",str(uuid.uuid4()))
assert scenario_replay==scenario_created
call("get-scenario","GET",f"/api/supply-chain/capacity-plans/{plan_id}/scenarios/{scenario_id}",200,corr=str(uuid.uuid4()))

evaluation={"evaluationMode":"Finite","resourceRefs":["line-4"]}
accepted,_=call("evaluate","POST",f"/api/supply-chain/capacity-plans/{plan_id}/scenarios/{scenario_id}/evaluations",202,evaluation,"eval-http-key",str(uuid.uuid4()))
evaluation_id=accepted["evaluationId"]
eval_replay,_=call("replay-evaluate","POST",f"/api/supply-chain/capacity-plans/{plan_id}/scenarios/{scenario_id}/evaluations",202,evaluation,"eval-http-key",str(uuid.uuid4()))
assert eval_replay==accepted
terminal=None
for attempt in range(20):
    terminal,_=call(f"get-evaluation-{attempt}","GET",f"/api/supply-chain/capacity-plans/{plan_id}/evaluations/{evaluation_id}",200,corr=str(uuid.uuid4()))
    if terminal["status"] in ("Completed","Failed"): break
    time.sleep(.5)
assert terminal["status"]=="Completed" and terminal["bottlenecks"][0]["shortfall"]=="40.000"
print(json.dumps({"summary":"PASS","planId":plan_id,"scenarioId":scenario_id,"evaluationId":evaluation_id},sort_keys=True))
