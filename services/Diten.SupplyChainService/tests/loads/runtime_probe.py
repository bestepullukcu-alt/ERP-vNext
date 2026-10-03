#!/usr/bin/env python3
"""Bounded MOD-0185 HTTP runtime probe. Requires isolated Mongo replica set and built API."""
import argparse, base64, hashlib, hmac, http.server, json, os, pathlib, secrets, socket, subprocess, sys, tempfile, threading, time, urllib.error, urllib.request, uuid

ROOT = pathlib.Path(__file__).resolve().parents[4]
API_DLL = ROOT / "services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/bin/Debug/net8.0/Diten.SupplyChainService.Api.dll"
# Q101 F02: the former static signing secret is treated as exposed and is gone. One value per run, never printed:
# MOD0185_PROBE_JWT_SECRET from the lane environment when an already-running API must accept the tokens,
# otherwise generated here and handed only to the API process this probe starts.
SECRET = (os.environ.get("MOD0185_PROBE_JWT_SECRET") or secrets.token_urlsafe(48)).encode()

class ReferenceHandler(http.server.BaseHTTPRequestHandler):
    captures = []
    carrier_id = str(uuid.uuid4())
    shipment_id = ""
    def do_GET(self):
        received = self.rfile.read(int(self.headers.get("Content-Length", "0"))) if self.headers.get("Content-Length") else b""
        type(self).captures.append({"method": self.command, "path": self.path, "tenant": self.headers.get("X-Tenant-Id"), "legalEntity": self.headers.get("X-Legal-Entity-Id"), "correlation": self.headers.get("X-Correlation-Id"), "authorizationPresent": bool(self.headers.get("Authorization")), "requestBytes": len(received), "requestBodySha256": hashlib.sha256(received).hexdigest()})
        if self.path == "/api/shipment-bundle/carriers?status=Active":
            body = {"items":[{"carrierId":type(self).carrier_id,"carrierCode":"RUNTIME-C","displayName":"Probe Carrier","status":"Active","supportedModes":["Road"]}],"total":1,"contractVersion":"v1"}
        elif self.path == "/api/shipment-bundle/shipments/" + type(self).shipment_id:
            body = {"shipmentId":type(self).shipment_id,"shipmentNumber":"RUNTIME-S","sourceDocumentId":"probe-doc","status":"Draft","carrierId":None,"loadId":None,"plannedShipAt":"2030-01-01T00:00:00Z","sourceModule":"MOD-0183","sourceType":"Shipment","warehouseReferenceId":"probe-wh","shipToReference":"probe-dest","lines":[],"contractVersion":"v1"}
        else:
            self.send_error(404); return
        raw = json.dumps(body).encode(); self.send_response(200); self.send_header("Content-Type","application/json"); self.send_header("Content-Length",str(len(raw))); self.end_headers(); self.wfile.write(raw)
    def log_message(self, *_): pass

def b64(data): return base64.urlsafe_b64encode(data).rstrip(b"=").decode()
def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()
def fingerprints():
    base=ROOT/"services/Diten.SupplyChainService/src"
    owned=[ROOT/"services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/Program.cs"]
    for layer in ["Diten.SupplyChainService.Api","Diten.SupplyChainService.Application","Diten.SupplyChainService.Domain","Diten.SupplyChainService.Persistence","Diten.SupplyChainService.Infrastructure"]:
        owned += sorted((base/layer/"Features/Loads").rglob("*.cs"))
    h=hashlib.sha256()
    for path in sorted(set(owned),key=lambda x:str(x.relative_to(ROOT))):
        rel=str(path.relative_to(ROOT)); h.update(rel.encode()+b"\0"+bytes.fromhex(sha(path)))
    contract_paths={"shipmentBundle":os.getenv("MOD0185_SHIPMENT_BUNDLE_CONTRACT_FILE"),"loadsAnnex":os.getenv("MOD0185_LOADS_ANNEX_FILE"),"carrierAnnex":os.getenv("MOD0185_CARRIER_ANNEX_FILE")}
    if any(not value for value in contract_paths.values()): raise RuntimeError("Set explicit frozen contract file paths for evidence hashing.")
    contracts={name:sha(pathlib.Path(value)) for name,value in contract_paths.items()}
    return {"apiBinarySha256":sha(API_DLL),"implementationSourceSetSha256":h.hexdigest(),"implementationSourceFiles":[str(x.relative_to(ROOT)) for x in sorted(set(owned),key=lambda x:str(x.relative_to(ROOT)))],"frozenContractsSha256":contracts}
def token(tenant, le, actor):
    header=b64(b'{"alg":"HS256","typ":"JWT"}')
    claims={"iss":"mod0185-runtime","aud":"mod0185-runtime","exp":int(time.time())+600,"tenant_id":tenant,"legal_entity_id":le,"sub":actor,"permission":["supplychain.loads.read","supplychain.loads.create","supplychain.loads.transition"]}
    payload=b64(json.dumps(claims,separators=(",",":")).encode()); signing=f"{header}.{payload}".encode()
    return signing.decode()+"."+b64(hmac.new(SECRET,signing,hashlib.sha256).digest())
def request(url, method="GET", body=None, headers=None):
    raw=None if body is None else json.dumps(body,separators=(",",":")).encode()
    hs=dict(headers or {})
    if raw is not None: hs["Content-Type"]="application/json"
    req=urllib.request.Request(url,data=raw,headers=hs,method=method)
    try:
        with urllib.request.urlopen(req,timeout=8) as response: return response.status,response.headers, response.read(), raw
    except urllib.error.HTTPError as e: return e.code,e.headers,e.read(), raw
def free_port():
    with socket.socket() as s: s.bind(("127.0.0.1",0)); return s.getsockname()[1]
def main():
    p=argparse.ArgumentParser(); p.add_argument("--mongo",default=os.getenv("MOD0185_TEST_MONGO")); p.add_argument("--api-base",default="http://127.0.0.1:5061"); p.add_argument("--evidence",required=True); p.add_argument("--start-local",action="store_true"); a=p.parse_args()
    if not a.mongo or "127.0.0.1:27785" not in a.mongo: raise SystemExit("Provide isolated MOD0185_TEST_MONGO on port 27785; no fallback is allowed.")
    if not a.start_local and not os.environ.get("MOD0185_PROBE_JWT_SECRET"): raise SystemExit("external API: set MOD0185_PROBE_JWT_SECRET to the lane signing value (never printed)")
    if not API_DLL.is_file(): raise SystemExit(f"Build API first; missing {API_DLL}")
    mock=None; api=None; logs=None
    try:
        if a.start_local:
            if a.api_base != "http://127.0.0.1:5061": raise SystemExit("--start-local binds only approved service port 5061")
            ReferenceHandler.shipment_id=str(uuid.uuid4()); ReferenceHandler.carrier_id=str(uuid.uuid4())
            mock=http.server.ThreadingHTTPServer(("127.0.0.1",free_port()),ReferenceHandler); mock_thread=threading.Thread(target=mock.serve_forever,daemon=True); mock_thread.start()
            logs=tempfile.TemporaryFile(mode="w+b")
            env=os.environ.copy(); env.update({"ASPNETCORE_URLS":a.api_base,"Mongo__ConnectionString":a.mongo,"Mongo__DatabaseName":"diten_mod0185_runtime_probe","JwtSettings__Secret":SECRET.decode(),"JwtSettings__Issuer":"mod0185-runtime","JwtSettings__Audience":"mod0185-runtime","Loads__ReferenceBaseUrl":f"http://127.0.0.1:{mock.server_port}/"})
            api=subprocess.Popen(["dotnet",str(API_DLL)],cwd=ROOT,env=env,stdout=logs,stderr=subprocess.STDOUT)
        deadline=time.time()+35
        while time.time()<deadline:
            if api and api.poll() is not None: raise RuntimeError("API exited before health check")
            try:
                status,_,_,_=request(a.api_base+"/health");
                if status==200: break
            except Exception: time.sleep(.25)
        else: raise RuntimeError("API health endpoint did not become ready")
        tenant,le,actor,correlation=[str(uuid.uuid4()) for _ in range(4)]; shipment=ReferenceHandler.shipment_id or str(uuid.uuid4()); carrier=ReferenceHandler.carrier_id
        auth={"Authorization":"Bearer "+token(tenant,le,actor),"X-Tenant-Id":tenant,"X-Legal-Entity-Id":le,"X-Correlation-Id":correlation}
        body={"carrierId":carrier,"shipmentIds":[shipment],"mode":"Road","plannedDepartAt":"2030-01-01T00:00:00Z","stops":[{"sequence":1,"locationReferenceId":"","action":"Pickup"},{"sequence":2,"locationReferenceId":"dest","action":"Delivery"}]}
        status,headers,raw,sent_create=request(a.api_base+"/api/shipment-bundle/loads","POST",body,{**auth,"Idempotency-Key":"runtime-probe"}); created=json.loads(raw)
        assert status==201 and created["contractVersion"]=="v1" and set(created)=={"loadId","loadNumber","status","idempotentReplay","contractVersion"}
        assert headers.get("X-Correlation-Id")==correlation and created["status"]=="Draft"
        status_list,list_headers,raw_list,sent_list=request(a.api_base+"/api/shipment-bundle/loads?status=Draft&carrierId="+carrier,"GET",None,auth); listed=json.loads(raw_list)
        assert status_list==200 and listed["total"]>=1 and any(x["loadId"]==created["loadId"] for x in listed["items"])
        status_replay,replay_headers,raw_replay,sent_replay=request(a.api_base+"/api/shipment-bundle/loads","POST",body,{**auth,"Idempotency-Key":"runtime-probe"}); replay=json.loads(raw_replay)
        assert status_replay==201 and replay["idempotentReplay"] is True and replay["loadId"]==created["loadId"]
        calls=ReferenceHandler.captures
        assert len(calls)==2 and all(c["method"]=="GET" and c["requestBytes"]==0 and c["tenant"]==tenant and c["legalEntity"]==le and c["correlation"]==correlation and c["authorizationPresent"] for c in calls)
        def op(method,path,status,headers,sent,response):
            record={"method":method,"path":path,"status":status,"requestBody":None if sent is None else sent.decode(),"requestBytes":0 if sent is None else len(sent),"requestHeaders":{"contentType":headers.get("Content-Type"),"correlation":headers.get("X-Correlation-Id")},"responseBody":response.decode(),"responseBytes":len(response)}
            try:
                parsed=json.loads(response.decode())
                if "idempotentReplay" in parsed: record["idempotentReplay"]=parsed["idempotentReplay"]
            except json.JSONDecodeError: pass
            return record
        result={"level":"E3-runtime-http","apiPid":api.pid if api else None,"apiBase":a.api_base,"fingerprints":fingerprints(),"operations":[op("POST","/api/shipment-bundle/loads",status,headers,sent_create,raw),op("GET","/api/shipment-bundle/loads?status=Draft&carrierId="+carrier,status_list,list_headers,sent_list,raw_list),op("POST","/api/shipment-bundle/loads",status_replay,replay_headers,sent_replay,raw_replay)],"mockCalls":calls,"outboxContract":"Pending-only; persisted-state assertions are in isolated Mongo service tests.","tenant":tenant,"legalEntity":le,"correlationId":correlation}
        pathlib.Path(a.evidence).write_text(json.dumps(result,indent=2)+"\n"); print(json.dumps({"result":"PASS","evidence":a.evidence,"mockCalls":len(calls)}))
    finally:
        if api and api.poll() is None: api.terminate(); api.wait(timeout=10)
        if mock: mock.shutdown(); mock.server_close()
        if logs: logs.close()
if __name__=="__main__": main()
