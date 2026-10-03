#!/usr/bin/env python3
"""Q122 matrix setup (SETUP ONLY) for the rows whose controlling text needs partial grants or extra states
(CU-07 create without shipments.read, CU-08 action gating matrix, CU-17 Planned/Cancelled).
  seed  A lane Auth DB: add the EXISTING catalog key supplychain.shipments.cancel to Q122FixtureAdmin@T1 (Draft -> Cancelled).
        B product APIs through the lane Gateway with the fixture admin's real token: shipments PLANNED (Planned) and
          CANCELLED (Cancelled); claims on shipment SEED: MX_OPEN1..4 (Open), MX_INV1..2 (Investigating),
          MX_APPR1 (Approved 100.00), MX_REJ1..2 (Rejected).
  role <label> <key,key,...>
        lane Auth DB: role Q122Matrix-<label>@T1 with exactly those EXISTING catalog keys; the `readonly` actor's other
        Q122* memberships are soft-deleted (IsDeleted=true) and this one is added. Before/after keys are recorded.
Run by the lane supervisor (task harness; ACTOR_PW_FIXADMIN only used for the seed login). Never 27017.
Args: seed <gateway port> <mongo port> <suffix> <out json>  |  role <label> <keys> <mongo port> <suffix> <out json>
Output: ids, statuses and keys only — never a password, token or cookie."""
import base64, json, os, subprocess, sys, urllib.error, urllib.request, uuid
from datetime import datetime, timedelta, timezone

T1 = "97c59330-dbc4-4665-b29c-0c26dbb5cc93"
PORTGUARD = "const a=JSON.parse(process.env.FX);const port=db.adminCommand({getCmdLineOpts:1}).parsed.net.port;" \
            "if(port===27017||String(port)!==a.port){print(JSON.stringify({error:'not lane port'}));quit(2);}"


def mongo(mport, js, fx):
    if str(mport) == "27017":
        sys.exit("refusing 27017")
    r = subprocess.run(["mongosh", "--quiet", f"mongodb://127.0.0.1:{mport}/?directConnection=true", "--eval", js], capture_output=True, text=True,
                       env={"PATH": "/opt/homebrew/bin:/usr/bin:/bin", "HOME": "/tmp", "FX": json.dumps(fx)})
    lines = [l for l in r.stdout.splitlines() if l.startswith("{")]
    if r.returncode != 0 or not lines:
        sys.exit(f"mongosh failed rc={r.returncode} {r.stderr[-300:]}")
    res = json.loads(lines[-1])
    if "error" in res:
        sys.exit(json.dumps(res))
    return res


def write(out, res):
    if os.path.exists(out):
        sys.exit("refusing to overwrite " + out)
    open(out, "w").write(json.dumps(res, indent=2) + "\n"); print(json.dumps(res)[:1500])


ROLEJS = PORTGUARD + r"""
const d=db.getSiblingDB('DitenAuth_'+a.sfx); const T=UUID(a.tenant);
const keysOf=(uid)=>{ const rs=d.userRoles.find({TenantId:T,UserId:uid,IsDeleted:false}).toArray().map(x=>x.RoleId);
  const pids=d.rolePermissions.find({RoleId:{$in:rs},IsDeleted:false}).toArray().map(x=>x.PermissionId);
  return d.permissions.find({_id:{$in:pids}}).toArray().map(p=>p.Key).filter(k=>/^supplychain\./.test(k)).sort(); };
const u=d.users.findOne({Email:a.email,TenantId:T,IsDeleted:false}); if(!u){print(JSON.stringify({error:'user'}));quit(3);}
const before=keysOf(u._id); const tplR=d.roles.findOne({TenantId:T,Name:'Q122ClaimsReadOnly'}); const rpTpl=d.rolePermissions.findOne({}); const urTpl=d.userRoles.findOne({});
let r=d.roles.findOne({TenantId:T,Name:a.role});
if(!r){ r=Object.assign({},tplR,{_id:UUID(),Name:a.role,DisplayName:a.role,Description:'Q122 matrix role',CreatedBy:'q122-matrix'}); d.roles.insertOne(r); }
const perms=d.permissions.find({Key:{$in:a.keys},IsDeleted:false}).toArray(); if(perms.length!==a.keys.length){print(JSON.stringify({error:'catalog '+perms.length+'/'+a.keys.length}));quit(4);}
for(const p of perms){ if(!d.rolePermissions.findOne({RoleId:r._id,PermissionId:p._id,IsDeleted:false}))
  d.rolePermissions.insertOne(Object.assign({},rpTpl,{_id:UUID(),TenantId:T,RoleId:r._id,PermissionId:p._id,AssignedAt:new Date(),AssignedBy:'q122-matrix',CreatedBy:'q122-matrix'})); }
const q122=d.roles.find({TenantId:T,Name:/^Q122/}).toArray().map(x=>x._id);
const off=d.userRoles.updateMany({TenantId:T,UserId:u._id,RoleId:{$in:q122.filter(x=>x.toString()!==r._id.toString())},IsDeleted:false},{$set:{IsDeleted:true,UpdatedAt:new Date(),UpdatedBy:'q122-matrix'}});
// Q122 a2: userRoles has a unique (UserId, RoleId, TenantId) index that ignores IsDeleted -> reactivate an existing membership
const ex=d.userRoles.findOne({TenantId:T,UserId:u._id,RoleId:r._id});
if(ex) d.userRoles.updateOne({_id:ex._id},{$set:{IsDeleted:false,UpdatedAt:new Date(),UpdatedBy:'q122-matrix'}});
else d.userRoles.insertOne(Object.assign({},urTpl,{_id:UUID(),TenantId:T,UserId:u._id,RoleId:r._id,IsDeleted:false,AssignedAt:new Date(),AssignedBy:'q122-matrix',CreatedBy:'q122-matrix'}));
print(JSON.stringify({actor:'readonly',role:a.role,requestedKeys:a.keys.slice().sort(),supplychainKeysBefore:before,supplychainKeysAfter:keysOf(u._id),membershipsSoftDeleted:off.modifiedCount}));
"""

CANCELJS = PORTGUARD + r"""
const d=db.getSiblingDB('DitenAuth_'+a.sfx); const T=UUID(a.tenant);
const r=d.roles.findOne({TenantId:T,Name:'Q122FixtureAdmin'}); const p=d.permissions.findOne({Key:'supplychain.shipments.cancel',IsDeleted:false});
if(!r||!p){print(JSON.stringify({error:'role or key missing'}));quit(3);}
const before=d.rolePermissions.countDocuments({RoleId:r._id,IsDeleted:false}); const tpl=d.rolePermissions.findOne({RoleId:r._id});
if(!d.rolePermissions.findOne({RoleId:r._id,PermissionId:p._id,IsDeleted:false}))
  d.rolePermissions.insertOne(Object.assign({},tpl,{_id:UUID(),PermissionId:p._id,AssignedAt:new Date(),AssignedBy:'q122-matrix',CreatedBy:'q122-matrix'}));
print(JSON.stringify({role:'Q122FixtureAdmin@T1',grantAdded:'supplychain.shipments.cancel',grantsBefore:before,grantsAfter:d.rolePermissions.countDocuments({RoleId:r._id,IsDeleted:false})}));
"""


def call(gw, method, path, body=None, token=None, headers=None):
    h = {"Content-Type": "application/json", "X-Correlation-Id": str(uuid.uuid4())}
    if token:
        h["Authorization"] = "Bearer " + token
    h.update(headers or {})
    data = None if body is None else json.dumps(body, separators=(",", ":")).encode()
    req = urllib.request.Request(gw + path, data=data, headers=h, method=method)
    try:
        with urllib.request.urlopen(req, timeout=60) as r:
            raw = r.read().decode(); return r.status, (json.loads(raw) if raw else None)
    except urllib.error.HTTPError as e:
        raw = e.read().decode()
        try:
            return e.code, json.loads(raw)
        except Exception:
            return e.code, {"unparsed": raw[:200]}


def seed(gwp, mport, sfx, out):
    res = {"authGrant": mongo(mport, CANCELJS, {"port": mport, "sfx": sfx, "tenant": T1}), "steps": [], "shipments": {}, "claims": {}}
    fx = json.load(open(os.path.join(os.path.dirname(out), "org-fixture.json")))["fixtures"]
    gw = f"http://127.0.0.1:{gwp}"
    st, b = call(gw, "POST", "/api/tenant-auth/login", {"email": "alice.williams.t97@diten.com", "password": os.environ["ACTOR_PW_FIXADMIN"], "rememberMe": False}, None, {"X-Tenant-Id": T1})
    if st != 200:
        sys.exit(f"login {st}")
    tok = b["data"]["accessToken"]; p = tok.split(".")[1]; p += "=" * (-len(p) % 4); le = json.loads(base64.urlsafe_b64decode(p)).get("legal_entity_id")
    run = uuid.uuid4().hex[:6]; now = datetime.now(timezone.utc); iso = lambda t: t.isoformat(timespec="milliseconds").replace("+00:00", "Z")

    def api(method, path, body, key, corr, scope=True):
        h = {"X-Correlation-Id": corr, "Idempotency-Key": key}
        if scope:
            h.update({"X-Tenant-Id": T1, "X-Legal-Entity-Id": le})
        st, b = call(gw, method, path, body, tok, h); res["steps"].append({"step": f"{method} {path.split('/')[-1] if len(path) > 60 else path}", "status": st})
        if st not in (200, 201):
            write(out, res); sys.exit(f"step failed {st} {path} {json.dumps(b)[:300]}")
        return b
    for name, steps in (("PLANNED", ["Planned"]), ("CANCELLED", ["Cancelled"])):
        root = str(uuid.uuid4())
        body = {"sourceModule": "EVIDENCE", "sourceType": "Acceptance", "sourceDocumentId": f"Q122-MX-{name}-{run}", "warehouseReferenceId": "WH-Q122",
                "shipToReference": f"SHIP-TO-{name}", "plannedShipAt": iso(now + timedelta(hours=1)), "plannedDeliverAt": iso(now + timedelta(days=1)),
                "lines": [{"lineNumber": "1", "itemId": str(uuid.uuid4()), "skuId": str(uuid.uuid4()), "quantity": "2", "uomId": "EA", "inventoryReferenceId": None}]}
        b = api("POST", "/api/shipment-bundle/shipments", body, f"Q122-MX-{name}-{run}-C", root); sid = b.get("shipmentId") or b.get("data")
        for s in steps:
            b = api("POST", f"/api/shipment-bundle/shipments/{sid}/transition", {"targetStatus": s, "occurredAt": iso(now + timedelta(minutes=5)), "reasonCode": None, "note": "q122 matrix"}, f"Q122-MX-{name}-{run}-{s}", root)
        res["shipments"][name] = {"shipmentId": sid, "status": b.get("status")}
    ship = fx["shipments"]["SEED"]; root = ship["lifecycleCorrelationId"]
    plan = [("MX_OPEN1", []), ("MX_OPEN2", []), ("MX_OPEN3", []), ("MX_OPEN4", []), ("MX_INV1", [("Investigating", None)]), ("MX_INV2", [("Investigating", None)]),
            ("MX_APPR1", [("Investigating", None), ("Approved", "100.00")]), ("MX_REJ1", [("Investigating", None), ("Rejected", None)]), ("MX_REJ2", [("Investigating", None), ("Rejected", None)])]
    for name, steps in plan:
        b = api("POST", "/api/shipment-bundle/claims", {"shipmentId": ship["shipmentId"], "reasonCode": f"SEED-{name}", "claimedAmount": "150.00", "currency": "EUR"}, f"Q122-{name}-{run}", root, scope=False)
        cid = b["claimId"]; status = b["status"]
        for tgt, appr in steps:
            body = {"targetStatus": tgt, "occurredAt": iso(datetime.now(timezone.utc))}
            if appr:
                body["approvedAmount"] = appr
            status = api("POST", f"/api/shipment-bundle/claims/{cid}/transition", body, f"Q122-{name}-{run}-{tgt}", root, scope=False)["status"]
        res["claims"][name] = {"claimId": cid, "shipmentId": ship["shipmentId"], "status": status}
    tok = None
    res["seedShipment"] = ship["shipmentId"]; res["result"] = "PASS"
    write(out, res)


def carrier(gwp, out):
    """Q122 a2: one more carrier (Carrier API, fixture admin) so CU-10 can POST an EXISTING carrier that differs from the shipment's."""
    gw = f"http://127.0.0.1:{gwp}"
    st, b = call(gw, "POST", "/api/tenant-auth/login", {"email": "alice.williams.t97@diten.com", "password": os.environ["ACTOR_PW_FIXADMIN"], "rememberMe": False}, None, {"X-Tenant-Id": T1})
    if st != 200:
        sys.exit(f"login {st}")
    tok = b["data"]["accessToken"]; p = tok.split(".")[1]; p += "=" * (-len(p) % 4); le = json.loads(base64.urlsafe_b64decode(p)).get("legal_entity_id")
    run = uuid.uuid4().hex[:6]
    st, b = call(gw, "POST", "/api/shipment-bundle/carriers", {"carrierCode": f"Q122-CAR2-{run}", "displayName": "Q122 Carrier 2", "supportedModes": ["Road"]}, tok,
                 {"X-Tenant-Id": T1, "X-Legal-Entity-Id": le, "Idempotency-Key": f"Q122-CAR2-{run}"})
    tok = None
    write(out, {"step": "carrier-create-2", "status": st, "carrierId": (b or {}).get("carrierId"), "result": "PASS" if st == 201 else "FAIL"})


if sys.argv[1] == "carrier":
    carrier(sys.argv[2], sys.argv[3])
elif sys.argv[1] == "seed":
    seed(*sys.argv[2:6])
elif sys.argv[1] == "role":
    label, keys, mport, sfx, out = sys.argv[2:7]
    write(out, mongo(mport, ROLEJS, {"port": mport, "sfx": sfx, "tenant": T1, "email": "jane.smith.t97@diten.com", "role": f"Q122Matrix-{label}", "keys": keys.split(",")}))
else:
    sys.exit("usage")
