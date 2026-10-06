import jwt, time, uuid, requests, json
SECRET="LocalDevelopmentSharedJwtSigningSecretForDitenErpVNext2026_AtLeast32Chars!"
# M-3: the legal entity is NOT a token claim; it travels in X-Legal-Entity-Id and the service proves it with MDM (via this gateway).
PERMS=["manufacturing.bom.read","manufacturing.bom.create","manufacturing.bom.update","manufacturing.bom.release","manufacturing.bom.delete","manufacturing.bom.export","mdm.legal-entities.read"]
MDM=["mdm.legal-entities.create","mdm.legal-entities.read","mdm.legal-entities.update"]
def token(t,perms=PERMS):
    return jwt.encode({"iss":"diten-auth-service","aud":"diten-erp","sub":str(uuid.uuid4()),"name":"Smoke Tester","tenant_id":t,
        "permission":perms,"exp":int(time.time())+600},SECRET,algorithm="HS256")
GW="http://127.0.0.1:5000"
T="00000000-0000-0000-0000-000000000001"  # a tenant MDM serves locally
def call(m,path,body=None,t=T,le=None,perms=PERMS,raw=False):
    h={"Authorization":"Bearer "+token(t,perms),"X-Tenant-Id":t,"X-Correlation-Id":str(uuid.uuid4())}
    if le: h["X-Legal-Entity-Id"]=le
    r=requests.request(m,GW+path,json=body,headers=h,timeout=15)
    return r if raw else (r.status_code, (r.json() if r.content and r.headers.get("content-type","").startswith("application/json") else r.text))
def active_le():
    s,b=call("POST","/api/legal-entities",{"code":"MFG-"+uuid.uuid4().hex[:6].upper(),"legalName":"BOM E2E A.Ş.","displayName":"BOM E2E","legalFormCode":"AS","countryCode":"TR","baseCurrencyCode":"TRY"},perms=MDM)
    assert s==201,(s,b); assert call("PATCH",f"/api/legal-entities/{b['data']}/activate",perms=MDM,raw=True).status_code==204
    return b["data"]
LE,LE2=active_le(),active_le()
_call=call
def call(m,path,body=None,t=T,le="default",perms=PERMS,raw=False): return _call(m,path,body,t,LE if le=="default" else le,perms,raw)
item,c1,c2=str(uuid.uuid4()),str(uuid.uuid4()),str(uuid.uuid4())
draft={"itemId":item,"description":"Potassium citrate syrup","components":[{"componentItemId":c1,"quantity":"2.000","uomId":"EA","position":10},{"componentItemId":c2,"quantity":"0.500","uomId":"L","position":20}],"routing":{"steps":[{"stepNo":10,"operation":"Karıştırma","workCenter":"WC-01"}]}}
out=[]
s,b=call("POST","/api/bom/versions",draft); out.append(("create v1",s,b["status"],b["version"])); v1=b
s,b=call("POST",f"/api/bom/version/{v1['bomVersionId']}/release",{"changeControlRef":"CC-2026-100","rowVersion":v1["rowVersion"]}); out.append(("release v1",s,b["status"]))
s,b=call("POST","/api/bom/versions",draft); v2=b; out.append(("create v2",s,b["version"]))
s,b=call("POST",f"/api/bom/version/{v2['bomVersionId']}/release",{"changeControlRef":"CC-2026-101","rowVersion":v2["rowVersion"]}); out.append(("release v2",s,b["status"]))
s,b=call("GET",f"/api/bom/version/{v1['bomVersionId']}"); out.append(("v1 now",s,b["status"],b["effectiveTo"]==call("GET",f"/api/bom/version/{v2['bomVersionId']}")[1]["effectiveFrom"]))
s,b=call("GET",f"/api/bom/{item}/current"); out.append(("current",s,b["version"],sorted(b.keys())))
s,b=call("POST","/api/bom/explode",{"itemId":item,"quantity":"100.000"}); out.append(("explode",s,[(r["requiredQuantity"],r["uomId"]) for r in b["requirements"]]))
s,b=call("GET","/api/bom/versions?status=Superseded&status=Effective&orderBy=version&orderDir=asc"); out.append(("list",s,b["data"]["total"],b["data"]["filteredTotal"],[i["status"] for i in b["data"]["items"]]))
r=call("GET","/api/bom/versions/export?format=xlsx&columns=version,status",raw=True); out.append(("export xlsx",r.status_code,r.headers.get("content-type"),r.content[:2]==b"PK"))
s,b=call("GET",f"/api/bom/version/{v1['bomVersionId']}/history"); out.append(("history v1",s,[e["operation"] for e in b["entries"]]))
s,b=call("GET",f"/api/bom/version/{v1['bomVersionId']}",t=str(uuid.uuid4())); out.append(("other tenant",s,b["error"]["code"]))
s,b=call("GET",f"/api/bom/version/{v1['bomVersionId']}",le=LE2); out.append(("other active LE, same tenant",s,b["error"]["code"]))
s,b=call("GET",f"/api/bom/version/{v1['bomVersionId']}",le=str(uuid.uuid4())); out.append(("LE unknown to MDM",s,b["error"]["code"]))
s,b=call("GET",f"/api/bom/version/{v1['bomVersionId']}",le=None); out.append(("no LE",s,b["error"]["code"]))
s,b=call("POST","/api/bom/versions",draft,perms=["manufacturing.bom.read"]); out.append(("create w/o permission",s))
for o in out: print(*o)
