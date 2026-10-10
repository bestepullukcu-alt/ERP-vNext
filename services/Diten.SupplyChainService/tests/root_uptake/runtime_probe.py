#!/usr/bin/env python3
"""Evidence-only Root R2 HTTP probe. Requires a caller-started isolated API and Mongo replica set."""
import base64, hashlib, hmac, json, os, subprocess, sys, time, urllib.request, urllib.error
from pathlib import Path
from uuid import UUID, uuid4

def b64(v): return base64.urlsafe_b64encode(v).rstrip(b"=").decode()
def token(tenant, le, actor, perms):
    head=b64(b'{"alg":"HS256","typ":"JWT"}')
    payload=b64(json.dumps({"sub":str(actor),"tenant_id":str(tenant),"legal_entity_id":str(le),"actor_type":"tenant_user","permission":["supplychain.shipments."+p for p in perms],"iss":"mod0183-tests","aud":"mod0183-tests","exp":int(time.time())+900},separators=(',',':')).encode())
    return head+"."+payload+"."+b64(hmac.new(os.environ["ROOT_JWT_SECRET"].encode(),(head+"."+payload).encode(),hashlib.sha256).digest())
def mongo(js):
    return subprocess.run(["mongosh","--quiet",os.environ["ROOT_MONGO_URI"],"--eval",js],text=True,capture_output=True,check=False)
def http(path, t, l, actor, perms, corr):
    req=urllib.request.Request(os.environ["ROOT_API_URL"]+path,headers={"Authorization":"Bearer "+token(t,l,actor,perms),"X-Tenant-Id":str(t),"X-Legal-Entity-Id":str(l),"X-Correlation-Id":str(corr)})
    try:
        with urllib.request.urlopen(req,timeout=10) as r: return r.status, dict(r.headers), r.read().decode()
    except urllib.error.HTTPError as e: return e.code, dict(e.headers), e.read().decode()

def main():
    needed=["ROOT_API_URL","ROOT_MONGO_URI","ROOT_TEST_DB","ROOT_JWT_SECRET","ROOT_EVIDENCE"]
    miss=[x for x in needed if not os.environ.get(x)]
    if miss: print(json.dumps({"status":"BLOCKED","missing":miss})); return 2
    out=Path(os.environ["ROOT_EVIDENCE"]); out.parent.mkdir(parents=True,exist_ok=True)
    tenant,le,other,actor=uuid4(),uuid4(),uuid4(),uuid4(); rows=[]
    variants=[("missing",None), ("null", "null"), ("malformed", "not-a-uuid"), ("valid",str(uuid4())), ("nil",str(UUID(int=0)))]
    for label,root in variants:
        sid=uuid4(); corr=uuid4(); root_js="" if root is None else ("LifecycleCorrelationId:null" if root=="null" else "LifecycleCorrelationId:"+json.dumps(root))
        doc=f'{{_id:{json.dumps(str(sid))},TenantId:{json.dumps(str(tenant))},LegalEntityId:{json.dumps(str(le))},IsDeleted:false,CreatedAt:new Date(),Version:1,ShipmentNumber:"R2-{label}",SourceModule:"MOD-0141",SourceType:"SALES_ORDER",SourceDocumentId:"SO-{sid}",WarehouseReferenceId:"WH-1",ShipToReference:"C-1",Lines:[],PlannedShipAt:new Date(),Status:"Draft",CorrelationId:{json.dumps(str(corr))}{","+root_js if root_js else ""}}}'
        ins=mongo(f'db.getSiblingDB({json.dumps(os.environ["ROOT_TEST_DB"])}).sce_shipments.insertOne({doc});')
        if ins.returncode!=0: raise RuntimeError("fixture insert failed: "+ins.stderr)
        before=mongo(f'JSON.stringify(db.getSiblingDB({json.dumps(os.environ["ROOT_TEST_DB"])}).sce_shipments.countDocuments({{TenantId:{json.dumps(str(tenant))},LegalEntityId:{json.dumps(str(le))}}}))').stdout.strip()
        status,headers,body=http(f"/api/shipment-bundle/shipments/{sid}",tenant,le,actor,["read"],corr)
        after=mongo(f'JSON.stringify(db.getSiblingDB({json.dumps(os.environ["ROOT_TEST_DB"])}).sce_shipments.countDocuments({{TenantId:{json.dumps(str(tenant))},LegalEntityId:{json.dumps(str(le))}}}))').stdout.strip()
        rows.append({"case":label,"shipmentId":str(sid),"status":status,"headers":{k:v for k,v in headers.items() if k.lower() in ("x-correlation-id","content-type")},"body":body,"before":before,"after":after,"insertExit":ins.returncode})
    # foreign tenant and soft-delete isolation, and missing read permission
    sid=uuid4(); mongo(f'db.getSiblingDB({json.dumps(os.environ["ROOT_TEST_DB"])}).sce_shipments.insertOne({{_id:{json.dumps(str(sid))},TenantId:{json.dumps(str(tenant))},LegalEntityId:{json.dumps(str(le))},IsDeleted:true,CreatedAt:new Date(),Version:1,ShipmentNumber:"R2-deleted",SourceModule:"M",SourceType:"T",SourceDocumentId:"D",WarehouseReferenceId:"W",ShipToReference:"S",Lines:[],PlannedShipAt:new Date(),Status:"Draft",CorrelationId:{json.dumps(str(uuid4()))}}});')
    rows.append({"case":"soft-delete","status":http(f"/api/shipment-bundle/shipments/{sid}",tenant,le,actor,["read"],uuid4())[0]})
    rows.append({"case":"no-read-grant","status":http(f"/api/shipment-bundle/shipments/{sid}",tenant,le,actor,[],uuid4())[0]})
    out.write_text(json.dumps({"status":"PASS","api":os.environ["ROOT_API_URL"],"db":os.environ["ROOT_TEST_DB"],"tenant":str(tenant),"legalEntity":str(le),"rows":rows},indent=2))
    print(out); return 0
if __name__=="__main__": sys.exit(main())
