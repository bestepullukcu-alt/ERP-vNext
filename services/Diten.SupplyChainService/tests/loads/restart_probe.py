#!/usr/bin/env python3
"""Create in process one, restart the API, then replay and list through process two."""
import json, os, pathlib, subprocess, sys, tempfile, threading, time, uuid
from urllib.parse import urlsplit, urlunsplit
from runtime_probe import API_DLL, ROOT, ReferenceHandler, fingerprints, request, token, SECRET

def main():
    if len(sys.argv)!=3: raise SystemExit("usage: restart_probe.py isolated-mongo-uri evidence.json")
    mongo=sys.argv[1]; evidence=pathlib.Path(sys.argv[2])
    if "127.0.0.1:27785" not in mongo: raise SystemExit("Only isolated test Mongo port 27785 is allowed.")
    ReferenceHandler.shipment_id=str(uuid.uuid4()); ReferenceHandler.carrier_id=str(uuid.uuid4())
    from http.server import ThreadingHTTPServer
    mock=ThreadingHTTPServer(("127.0.0.1",0),ReferenceHandler); threading.Thread(target=mock.serve_forever,daemon=True).start()
    # Production port is fixed by the approved service contract. No other DB or listener is used.
    api_url="http://127.0.0.1:5066"; env=os.environ.copy(); env.update({"ASPNETCORE_URLS":api_url,"Mongo__ConnectionString":mongo,"Mongo__DatabaseName":"diten_mod0185_restart_probe","JwtSettings__Secret":SECRET.decode(),"JwtSettings__Issuer":"mod0185-runtime","JwtSettings__Audience":"mod0185-runtime","Loads__ReferenceBaseUrl":f"http://127.0.0.1:{mock.server_port}/"})
    logs=tempfile.TemporaryFile(mode="w+b"); processes=[]
    try:
        tenant,le,actor,correlation=[str(uuid.uuid4()) for _ in range(4)]; shipment=ReferenceHandler.shipment_id; carrier=ReferenceHandler.carrier_id
        auth={"Authorization":"Bearer "+token(tenant,le,actor),"X-Tenant-Id":tenant,"X-Legal-Entity-Id":le,"X-Correlation-Id":correlation,"Idempotency-Key":"restart-key"}
        body={"carrierId":carrier,"shipmentIds":[shipment],"mode":"Road","plannedDepartAt":"2030-01-01T00:00:00Z","stops":[{"sequence":1,"locationReferenceId":"","action":"Pickup"},{"sequence":2,"locationReferenceId":"dest","action":"Delivery"}]}
        def start():
            proc=subprocess.Popen(["dotnet",str(API_DLL)],cwd=ROOT,env=env,stdout=logs,stderr=subprocess.STDOUT); processes.append(proc)
            deadline=time.time()+35
            while time.time()<deadline:
                if proc.poll() is not None: raise RuntimeError("API exited during restart probe")
                try:
                    if request(api_url+"/health")[0]==200: return proc
                except Exception: time.sleep(.25)
            raise RuntimeError("API did not become healthy")
        first=start(); status,_,raw,_=request(api_url+"/api/shipment-bundle/loads","POST",body,auth); created=json.loads(raw)
        assert status==201 and created["idempotentReplay"] is False
        first.terminate(); first.wait(timeout=10)
        second=start(); replay_status,_,replay_raw,_=request(api_url+"/api/shipment-bundle/loads","POST",body,auth); replay=json.loads(replay_raw)
        list_status,_,list_raw,_=request(api_url+"/api/shipment-bundle/loads?status=Draft&carrierId="+carrier,"GET",None,{k:v for k,v in auth.items() if k!="Idempotency-Key"}); listed=json.loads(list_raw)
        assert replay_status==201 and replay["idempotentReplay"] is True and replay["loadId"]==created["loadId"]
        assert list_status==200 and any(x["loadId"]==created["loadId"] for x in listed["items"])
        js=f'JSON.stringify(db.loads_outbox.find({{TenantId:"{tenant}",LegalEntityId:"{le}",LoadId:"{created["loadId"]}"}}).toArray())'
        parsed=urlsplit(mongo); db_uri=urlunsplit((parsed.scheme,parsed.netloc,"/diten_mod0185_restart_probe",parsed.query,parsed.fragment))
        persisted=subprocess.run(["mongosh",db_uri,"--quiet","--eval",js],check=True,text=True,capture_output=True)
        pending=json.loads(persisted.stdout)
        assert len(pending)==1 and pending[0]["Status"]=="Pending"
        calls=ReferenceHandler.captures
        assert len(calls)==2 and all(c["method"]=="GET" and c["requestBytes"]==0 for c in calls)
        assert all(c["tenant"]==tenant and c["legalEntity"]==le and c["correlation"]==correlation and c["authorizationPresent"] for c in calls)
        evidence.write_text(json.dumps({"level":"E3-process-restart","fingerprints":fingerprints(),"processIds":[first.pid,second.pid],"createStatus":status,"replayStatus":replay_status,"listStatus":list_status,"loadId":created["loadId"],"replayedHistoricalResult":replay["idempotentReplay"],"listedAfterRestart":True,"dependencyCalls":calls,"outboxAfterRestart":{"count":len(pending),"status":pending[0]["Status"],"loadId":pending[0]["LoadId"]}},indent=2)+"\n")
        print(json.dumps({"result":"PASS","processIds":[first.pid,second.pid],"evidence":str(evidence)}))
    finally:
        for proc in reversed(processes):
            if proc.poll() is None: proc.terminate(); proc.wait(timeout=10)
        mock.shutdown(); mock.server_close(); logs.close()
if __name__=="__main__": main()
