#!/usr/bin/env python3
import base64,hashlib,hmac,json,os,time,urllib.error,urllib.request,uuid
BASE=os.environ["CAPACITY_BASE"];SECRET=os.environ["CAPACITY_JWT_SECRET"].encode()
PLAN=os.environ["CAPACITY_PLAN_ID"];SCENARIO=os.environ["CAPACITY_SCENARIO_ID"];EVALUATION=os.environ["CAPACITY_EVALUATION_ID"]
def b64(v):return base64.urlsafe_b64encode(v).rstrip(b"=").decode()
header={"alg":"HS256","typ":"JWT"};payload={"iss":"mod0192-evidence","aud":"mod0192-api","exp":int(time.time())+3600,"iat":int(time.time()),
 "sub":"19200000-0000-4000-8000-000000000004","tenant_id":"19200000-0000-4000-8000-000000000001","legal_entity_id":"19200000-0000-4000-8000-000000000002",
 "permission":["supplychain.capacity-plans.read","supplychain.capacity-plans.evaluate"]}
unsigned=b64(json.dumps(header,separators=(",",":")).encode())+"."+b64(json.dumps(payload,separators=(",",":")).encode())
TOKEN=unsigned+"."+b64(hmac.new(SECRET,unsigned.encode(),hashlib.sha256).digest())
def call(name,method,path,expected,body=None,key=None):
 headers={"Authorization":"Bearer "+TOKEN,"X-Correlation-Id":str(uuid.uuid4()),"Accept":"application/json"}
 data=None
 if body is not None:data=json.dumps(body,separators=(",",":")).encode();headers["Content-Type"]="application/json";headers["Idempotency-Key"]=key
 req=urllib.request.Request(BASE+path,data=data,headers=headers,method=method)
 try:
  with urllib.request.urlopen(req,timeout=10) as r:status=r.status;raw=r.read().decode();rh=dict(r.headers)
 except urllib.error.HTTPError as e:status=e.code;raw=e.read().decode();rh=dict(e.headers)
 parsed=json.loads(raw);print(json.dumps({"test":name,"status":status,"expected":expected,"correlation":rh.get("X-Correlation-Id"),"body":parsed},sort_keys=True))
 assert status==expected
 return parsed
call("restart-get-plan","GET",f"/api/supply-chain/capacity-plans/{PLAN}",200)
terminal=call("restart-get-terminal","GET",f"/api/supply-chain/capacity-plans/{PLAN}/evaluations/{EVALUATION}",200)
assert terminal["status"]=="Completed" and terminal["bottlenecks"][0]["shortfall"]=="40.000"
replay=call("restart-replay-evaluation","POST",f"/api/supply-chain/capacity-plans/{PLAN}/scenarios/{SCENARIO}/evaluations",202,{"evaluationMode":"Finite","resourceRefs":["line-4"]},"eval-http-key")
assert replay["evaluationId"]==EVALUATION and replay["status"]=="Accepted"
print(json.dumps({"summary":"PASS","restartPersistence":True,"originalReceiptReplay":True},sort_keys=True))
