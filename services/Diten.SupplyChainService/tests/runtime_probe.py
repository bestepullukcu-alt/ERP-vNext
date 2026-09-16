"""Isolated process-level MOD-0183 evidence. Requires built API, mongosh, PyYAML/jsonschema.
Use only a dedicated replica set via MOD0183_TEST_MONGO. Writes evidence to argv[1].
"""
import base64, copy, hashlib, hmac, json, os, pathlib, socket, subprocess, sys, time, urllib.request, urllib.error, uuid
import yaml, jsonschema
ROOT=pathlib.Path(__file__).resolve().parents[3]
OUT=pathlib.Path(sys.argv[1]); OUT.mkdir(parents=True,exist_ok=True)
URI=os.environ["MOD0183_TEST_MONGO"]
DB="diten_mod0183_runtime_tests"
T,L,A,C=[str(uuid.uuid4()) for _ in range(4)]
SECRET=uuid.uuid4().hex+uuid.uuid4().hex
PERMS=["supplychain.shipments."+p for p in ("read","create","dispatch","pod.capture","cancel","reconcile")]
contract=yaml.safe_load(pathlib.Path(sys.argv[2]).read_text())
records=[]
def validate(body,name):
    schema={"$ref":"#/components/schemas/"+name, "components":contract["components"]}
    jsonschema.Draft202012Validator(schema,format_checker=jsonschema.FormatChecker()).validate(body)
def token(tenant=T,le=L,perms=PERMS,secret=SECRET):
    enc=lambda b:base64.urlsafe_b64encode(b).decode().rstrip("=")
    head=enc(json.dumps({"alg":"HS256","typ":"JWT"}).encode())
    payload=enc(json.dumps({"iss":"mod0183-runtime","aud":"mod0183-runtime","sub":A,"tenant_id":tenant,"legal_entity_id":le,
                           "permission":perms,"exp":int(time.time())+600}).encode())
    msg=head+"."+payload
    return msg+"."+enc(hmac.new(secret.encode(),msg.encode(),hashlib.sha256).digest())
def call(method,path="",body=None,key=None,expected=200,schema=None,tenant=T,le=L,perms=PERMS,corr=C,auth=True,bad_signature=False):
    headers={"X-Tenant-Id":tenant,"X-Legal-Entity-Id":le,"Content-Type":"application/json"}
    if auth: headers["Authorization"]="Bearer "+token(tenant,le,perms,SECRET+"bad" if bad_signature else SECRET)
    if corr is not None:headers["X-Correlation-Id"]=corr
    if key is not None:headers["Idempotency-Key"]=key
    req=urllib.request.Request("http://127.0.0.1:5061/api/shipment-bundle/shipments"+path,
        data=json.dumps(body).encode() if body is not None else None,headers=headers,method=method)
    # Snapshot the serialized wire body before later caller mutations can alter evidence.
    request_snapshot=json.loads(req.data) if req.data is not None else None
    try:
        with urllib.request.urlopen(req) as r: status=r.status; raw=r.read()
    except urllib.error.HTTPError as r: status=r.code; raw=r.read()
    result=json.loads(raw) if raw else None
    records.append({"method":method,"path":path,"request":request_snapshot,"idempotencyKey":key,"tenantId":tenant,"legalEntityId":le,
                    "actorId":A if auth else None,"permissions":perms if auth else [],"correlationId":corr,"status":status,"response":result})
    assert status==expected,(method,path,status,expected,result)
    if schema:validate(result,schema)
    elif result and status>=400:validate(result,"Error")
    return result
def snapshot():
    js='const d=db.getSiblingDB('+json.dumps(DB)+');const s={TenantId:'+json.dumps(T)+',LegalEntityId:'+json.dumps(L)+'};let r={};for(const n of ["sce_shipments","sce_shipment_receipts","sce_shipment_history","sce_shipment_audit","sce_shipment_outbox"])r[n]=d.getCollection(n).find(s).sort({_id:1}).toArray();print(EJSON.stringify(r));'
    return json.loads(subprocess.check_output(["mongosh",URI,"--quiet","--eval",js],text=True))
def start():
    env=dict(os.environ, Mongo__ConnectionString=URI,Mongo__DatabaseName=DB,JwtSettings__Secret=SECRET,
             JwtSettings__Issuer="mod0183-runtime",JwtSettings__Audience="mod0183-runtime",ASPNETCORE_URLS="http://127.0.0.1:5061")
    log=open(OUT/"service.log","a")
    p=subprocess.Popen(["dotnet",str(ROOT/"services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/bin/Debug/net8.0/Diten.SupplyChainService.Api.dll")],
                       cwd=ROOT,env=env,stdout=log,stderr=subprocess.STDOUT)
    for _ in range(100):
        if p.poll() is not None:raise RuntimeError("Service exited; inspect service.log")
        try:
            urllib.request.urlopen("http://127.0.0.1:5061/health",timeout=.5).close()
            return p
        except (OSError,urllib.error.URLError):time.sleep(.1)
    p.terminate();p.wait();raise RuntimeError("Startup timeout")
def stop(p):p.terminate();p.wait(timeout=15)
# Never replace an existing listener.
with socket.socket() as s:
    if s.connect_ex(("127.0.0.1",5061))==0:raise RuntimeError("5061 occupied; refusing to interfere")
p=start()
try:
    body=contract["paths"]["/shipments"]["post"]["requestBody"]["content"]["application/json"]["example"]
    body=copy.deepcopy(body)
    validate(body,"CreateShipmentCommand")
    created=call("POST",body=body,key="create",expected=201,schema="ShipmentResponse"); sid=created["shipmentId"]
    call("GET","/"+sid,schema="ShipmentDetail")
    for state,key in [("Planned","plan"),("Dispatched","dispatch")]:
        call("POST","/"+sid+"/transition",{"targetStatus":state,"occurredAt":"2026-09-20T08:15:00Z"},key,schema="ShipmentResponse")
    pod={"recipientName":"Runtime Test Recipient","receivedAt":"2026-09-21T15:42:00Z","evidenceReferenceIds":["test-only-evidence"],"note":"Isolated verification"}
    call("POST","/"+sid+"/pod",pod,"pod",201,"PodResponse")
    call("GET","/"+sid,schema="ShipmentDetail")
    call("POST","/"+sid+"/pod",pod,"pod",201,"PodResponse")
    call("POST","/"+sid+"/pod",pod,"duplicate",409)
    call("POST","/"+sid+"/transition",{"targetStatus":"Closed","occurredAt":"2026-09-21T16:00:00Z"},"close",schema="ShipmentResponse")
    call("GET",schema="ShipmentListResponse")
    call("GET","/"+sid,tenant=str(uuid.uuid4()),expected=404)
    call("GET","/"+sid,le=str(uuid.uuid4()),expected=404)
    call("POST","/"+sid+"/pod",pod,"cross-tenant",tenant=str(uuid.uuid4()),expected=404)
    call("POST","/"+sid+"/transition",{"targetStatus":"Closed","occurredAt":"2026-09-21T16:00:00Z"},"cross-le",le=str(uuid.uuid4()),expected=404)
    call("GET","/"+str(uuid.uuid4()),expected=404)
    call("GET","/"+sid,perms=[],expected=403)
    call("GET","/"+sid,corr=None,expected=400)
    call("GET","/"+sid,corr="invalid",expected=400)
    call("GET","/"+sid,auth=False,expected=401)
    call("GET","/"+sid,bad_signature=True,expected=401)
    before=snapshot()
    assert [len(before[n]) for n in before]==[1,5,5,5,6]
    for event in before["sce_shipment_outbox"]:
        validate(json.loads(event["PayloadJson"]),"LifecycleEventEnvelope")
        assert json.loads(event["PayloadJson"])["correlationId"]==C
    stop(p); p=start()
    assert snapshot()==before,"Process restart changed persisted records"
    result=call("GET","/"+sid,schema="ShipmentDetail");assert result["status"]=="Closed" and result["pod"]["recipientName"]==pod["recipientName"]
    replay=call("POST",body=body,key="create",schema="ShipmentResponse")
    assert replay["idempotentReplay"] and replay["status"]=="Draft"
    assert snapshot()==before,"Replay added durable records"
    (OUT/"persisted-state.json").write_text(json.dumps(before,indent=2))
    (OUT/"runtime.json").write_text(json.dumps({"result":"PASS","database":DB,"tenantId":T,"legalEntityId":L,
        "actorId":A,"correlationId":C,"realProcessRestart":True,"schemaValidation":"Draft202012 + format checker; frozen request example, responses and all six events",
        "records":records},indent=2))
    print("PASS: real process restart, frozen-wire validation, scoped state, HTTP probes; evidence:",OUT)
finally:
    if p.poll() is None:stop(p)
