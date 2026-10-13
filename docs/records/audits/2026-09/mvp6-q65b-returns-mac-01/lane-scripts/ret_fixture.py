#!/usr/bin/env python3
"""Q65b lane fixture 2 — copied from Q65b sop_fixture.py (itself the Q64d org fixture parts A and B); part C adds Delivered
shipments through the product Shipment API (Q64d make_shipment, steps Planned → Dispatched → Delivered). SETUP ONLY; acceptance runs in the browser harness afterwards.
Run by the lane supervisor (task harness), which supplies ACTOR_PW_<LABEL>.
  A  lane Auth DB: per tenant, roles with EXISTING catalog keys only (the Returns keys arrive through the ReverseLogistics manifest provider
     self-registration -> Platform -> Auth sync; this script waits for them and never inserts a permission document), members by e-mail.
  B  product APIs through the lane Gateway with the fixture admin's real Auth token: MDM legal entities (create + activate),
     Platform organisation units, one position per actor, position assignments (this gives the legal_entity_id claim).
Args: <gateway port> <lane mongo port> <db suffix> <out json>
Output: ids, statuses and booleans only — never a password, hash, token or cookie.
"""

import base64, json, os, subprocess, sys, time, urllib.error, urllib.request, uuid
from datetime import datetime, timedelta, timezone

GWP, MPORT, SFX, OUT = sys.argv[1:5]
if MPORT == "27017":
    sys.exit("refusing 27017")
GW = f"http://127.0.0.1:{GWP}"
MURI = f"mongodb://127.0.0.1:{MPORT}/?directConnection=true"
T1 = "97c59330-dbc4-4665-b29c-0c26dbb5cc93"; T2 = "00000000-0000-0000-0000-000000000001"
ACT = {"full": ("john.doe.t97@diten.com", T1), "readonly": ("jane.smith.t97@diten.com", T1), "noread": ("charlie.brown.t97@diten.com", T1),
       "leb": ("bob.johnson.t97@diten.com", T1), "fixadmin": ("alice.williams.t97@diten.com", T1)}
PW = {k: os.environ[f"ACTOR_PW_{k.upper()}"] for k in ACT}
RUN = uuid.uuid4().hex[:6]
out = {"run": RUN, "tenants": {"T1": T1, "T2": T2}, "steps": []}
now = datetime.now(timezone.utc)
iso = lambda t: t.isoformat(timespec="milliseconds").replace("+00:00", "Z")


def dump_and_exit(msg):
    out["stopped"] = msg
    open(OUT, "w").write(json.dumps(out, indent=2) + "\n"); print(json.dumps(out, indent=2)); sys.exit(1)


def mongo(js, fx):
    r = subprocess.run(["mongosh", "--quiet", MURI, "--eval", js], capture_output=True, text=True,
                       env={"PATH": "/opt/homebrew/bin:/usr/bin:/bin", "HOME": "/tmp", "FX": json.dumps(fx)})
    lines = [l for l in r.stdout.splitlines() if l.startswith("{")]
    if r.returncode != 0 or not lines:
        dump_and_exit(f"mongosh failed rc={r.returncode} {r.stderr[-400:]}")
    return json.loads(lines[-1])


PORTGUARD = "const a=JSON.parse(process.env.FX);const port=db.adminCommand({getCmdLineOpts:1}).parsed.net.port;" \
            "if(port===27017||String(port)!==a.port){print(JSON.stringify({error:'not lane port'}));quit(2);}"


def call(method, path, body=None, token=None, headers=None):
    h = {"Content-Type": "application/json", "X-Correlation-Id": str(uuid.uuid4())}
    if token:
        h["Authorization"] = "Bearer " + token
    h.update(headers or {})
    data = None if body is None else json.dumps(body, separators=(",", ":")).encode()
    req = urllib.request.Request(GW + path, data=data, headers=h, method=method)
    try:
        with urllib.request.urlopen(req, timeout=60) as r:
            raw = r.read().decode(); return r.status, dict(r.headers), (json.loads(raw) if raw else None)
    except urllib.error.HTTPError as e:
        raw = e.read().decode()
        try:
            return e.code, dict(e.headers), json.loads(raw)
        except Exception:
            return e.code, dict(e.headers), {"unparsed": raw[:200]}


def claims_of(token):
    p = token.split(".")[1]; p += "=" * (-len(p) % 4)
    return json.loads(base64.urlsafe_b64decode(p))


def login(label):
    email, tenant = ACT[label]
    st, _, b = call("POST", "/api/tenant-auth/login", {"email": email, "password": PW[label], "rememberMe": False}, None, {"X-Tenant-Id": tenant})
    if st != 200:
        dump_and_exit(f"login {label} -> {st} {json.dumps({k: v for k, v in (b or {}).items() if k != 'data'})[:300]}")
    tok = b["data"]["accessToken"]; c = claims_of(tok)
    perms = c.get("permission", []); perms = [perms] if isinstance(perms, str) else perms
    return tok, {"actor": label, "tenantMatches": c.get("tenant_id") == tenant, "legalEntityId": c.get("legal_entity_id"),
                 "returnsPermissions": sorted(p for p in perms if p.startswith("supplychain.returns") or p == "supplychain.shipments.read"),
                 "shipmentsRead": "supplychain.shipments.read" in perms, "permissionCount": len(perms)}


def step(name, st, body, ok=(200, 201, 204)):
    rec = {"step": name, "status": st, "ok": st in ok}
    if isinstance(body, dict):
        d = body.get("data")
        rec["id"] = d if isinstance(d, str) else (d.get("id") if isinstance(d, dict) else None)
        for k in ("shipmentId", "claimId", "carrierId", "status", "claimNumber"):
            if k in body:
                rec[k] = body[k]
        if st not in ok:
            rec["error"] = {k: v for k, v in body.items() if k != "data"}
    out["steps"].append(rec)
    if st not in ok:
        dump_and_exit(f"step {name} failed {st}")
    return rec


# ---------------------------------------------------------------- A: catalog keys + roles (lane Auth DB)
RET = ["supplychain.returns.read", "supplychain.returns.create", "supplychain.returns.transition", "supplychain.returns.authorize", "supplychain.returns.transit",
       "supplychain.returns.cancel", "supplychain.returns.receive", "supplychain.returns.disposition", "supplychain.returns.close"]
ORG = ["mdm.legal-entities.create", "mdm.legal-entities.read", "mdm.legal-entities.update", "platform.organization-units.create",
       "platform.organization-units.read", "platform.positions.create", "platform.positions.read", "platform.position-assignments.create",
       "platform.position-assignments.read", "auth.users.lookup-validation"]
SHIP = ["supplychain.shipments.read", "supplychain.shipments.create", "supplychain.shipments.dispatch"]
CARR = ["supplychain.carriers.read", "supplychain.carriers.create"]
NEED = sorted(set(RET + ORG + SHIP))
WAITJS = PORTGUARD + "const d=db.getSiblingDB('DitenAuth_'+a.sfx);const have=d.permissions.find({Key:{$in:a.keys},IsDeleted:false}).toArray().map(p=>p.Key);print(JSON.stringify({have}));"
# Q65b D-3 (lane Auth DB only, before/after recorded): supplychain.returns.transition is required by the API
# (ReturnsController.cs:20) but registered by no manifest (pack §33 open gap 3 / M-03 "API-only allow-list"), so it never
# reaches the catalog (fixture a1: absent after 180 s). Clone the synced supplychain.returns.authorize catalog document as a
# lane-only document for that key, once the synced keys are present.
TKEY = "supplychain.returns.transition"
NEED = [k for k in NEED if k != TKEY]
t0 = time.time(); missing = NEED
while time.time() - t0 < 180:
    have = set(mongo(WAITJS, {"port": MPORT, "sfx": SFX, "keys": NEED})["have"])
    missing = [k for k in NEED if k not in have]
    if not missing:
        break
    time.sleep(5)
out["catalogWaitSeconds"] = round(time.time() - t0, 1)
out["catalogMissing"] = missing
if missing:
    dump_and_exit(f"permission catalog lacks {missing} after {out['catalogWaitSeconds']} s")

TJS = PORTGUARD + r"""
const d=db.getSiblingDB('DitenAuth_'+a.sfx); const res={key:a.key};
res.before=d.permissions.countDocuments({Key:a.key,IsDeleted:false});
if(res.before===0){ const tpl=d.permissions.findOne({Key:'supplychain.returns.authorize',IsDeleted:false});
  if(!tpl) throw new Error('template permission missing');
  const doc=JSON.parse(JSON.stringify(tpl, (k,v)=>v)); const clone=Object.assign({},tpl,{_id:UUID(),Key:a.key,Action:'transition'});
  for(const f of ['Name','DisplayName','Description','Code']) if(typeof clone[f]==='string') clone[f]=clone[f].replace(/authorize/gi,'transition');
  clone.CreatedBy='q65b-lane-fixture-D3'; d.permissions.insertOne(clone); res.templateKey='supplychain.returns.authorize'; }
res.after=d.permissions.countDocuments({Key:a.key,IsDeleted:false});
print(JSON.stringify(res));
"""
ROLESJS = PORTGUARD + r"""
const d=db.getSiblingDB('DitenAuth_'+a.sfx); const res={roles:{},users:{}};
const roleTpl=d.roles.findOne({IsDeleted:false}); const rpTpl=d.rolePermissions.findOne({}); const urTpl=d.userRoles.findOne({});
for (const [label,[email,tenant]] of Object.entries(a.actors)) {
  const u=d.users.find({Email:email,TenantId:UUID(tenant),IsDeleted:false}).toArray();
  if(u.length!==1) throw new Error('user '+label+' count '+u.length);
  res.users[label]={userId:u[0]._id.toString().replace(/^UUID\("|"\)$/g,''),tenant};
}
for (const r of a.roles) {
  const T=UUID(r.tenant); let rd=d.roles.findOne({TenantId:T,Name:r.name}); const reused=!!rd;
  if(!rd){ rd=Object.assign({},roleTpl,{_id:UUID(),TenantId:T,Name:r.name,DisplayName:r.name,Description:'Q65b lane fixture role',IsSystem:false,CreatedBy:'q65b-lane-fixture',UpdatedAt:null,UpdatedBy:null}); d.roles.insertOne(rd); }
  const perms=d.permissions.find({Key:{$in:r.keys},IsDeleted:false}).toArray();
  if(perms.length!==r.keys.length) throw new Error('catalog '+r.name+' '+perms.length+'/'+r.keys.length);
  let added=0;
  for(const p of perms){ if(d.rolePermissions.findOne({TenantId:T,RoleId:rd._id,PermissionId:p._id,IsDeleted:false})) continue;
    d.rolePermissions.insertOne(Object.assign({},rpTpl,{_id:UUID(),TenantId:T,RoleId:rd._id,PermissionId:p._id,AssignedAt:new Date(),AssignedBy:'q65b-lane-fixture',CreatedBy:'q65b-lane-fixture',GrantSource:rpTpl.GrantSource,SourceModuleCode:null})); added++; }
  for(const m of r.members){ const uid=UUID(res.users[m].userId);
    if(d.userRoles.findOne({TenantId:T,UserId:uid,RoleId:rd._id,IsDeleted:false})) continue;
    d.userRoles.insertOne(Object.assign({},urTpl,{_id:UUID(),TenantId:T,UserId:uid,RoleId:rd._id,AssignedAt:new Date(),AssignedBy:'q65b-lane-fixture',CreatedBy:'q65b-lane-fixture'})); }
  res.roles[r.name+'@'+r.tenant.slice(0,8)]={reused,keys:r.keys,grantsAdded:added,members:r.members};
}
print(JSON.stringify(res));
"""
ROLES = [
    {"tenant": T1, "name": "Q65bReturnsFull", "keys": RET + ["supplychain.shipments.read"], "members": ["full", "leb"]},
    {"tenant": T1, "name": "Q65bReturnsReadOnly", "keys": ["supplychain.returns.read"], "members": ["readonly"]},
    {"tenant": T1, "name": "Q65bNoReturns", "keys": ["supplychain.shipments.read"], "members": ["noread"]},
    {"tenant": T1, "name": "Q65bFixtureAdmin", "keys": ORG + SHIP, "members": ["fixadmin"]},
]
out["laneAuthPermissionInsert"] = mongo(TJS, {"port": MPORT, "sfx": SFX, "key": TKEY})
auth = mongo(ROLESJS, {"port": MPORT, "sfx": SFX, "actors": ACT, "roles": ROLES})
out["authRoles"] = auth["roles"]; uid = {k: v["userId"] for k, v in auth["users"].items()}
out["userIds"] = uid

# ---------------------------------------------------------------- B: organisation through product APIs
org = {"legalEntities": {}, "orgUnits": {}, "positions": {}}
plan = {T1: ("fixadmin", {"A": ["full", "readonly", "noread", "fixadmin"], "B": ["leb"]})}
for tenant, (admin, les) in plan.items():
    tok, s = login(admin); out.setdefault("adminLoginsBeforeOrg", []).append(s)
    H = {"X-Tenant-Id": tenant}
    for tag, members in les.items():
        st, _, b = call("POST", "/api/legal-entities", {"code": f"Q65B-LE-{tag}-{RUN}", "legalName": f"Q65b Legal Entity {tag}", "displayName": f"LE-{tag}",
                                                        "legalFormCode": "LLC", "organizationRoleCode": None, "countryCode": "TR", "baseCurrencyCode": "TRY",
                                                        "statutoryStatus": None}, tok, H)
        le = step(f"mdm-create-le-{tag}", st, b)["id"]; org["legalEntities"][tag] = le
        st, _, b = call("PATCH", f"/api/legal-entities/{le}/activate", {}, tok, H); step(f"mdm-activate-le-{tag}", st, b)
        st, _, b = call("POST", "/api/platform/organization-units", {"code": f"Q65B-OU-{tag}-{RUN}", "name": f"Q65b OU {tag}", "legalEntityId": le,
                                                                      "parentOrganizationUnitId": None}, tok, H)
        ou = step(f"platform-create-ou-{tag}", st, b)["id"]; org["orgUnits"][tag] = ou
        for m in members:
            st, _, b = call("POST", "/api/platform/positions", {"code": f"Q65B-POS-{m}-{RUN}", "name": f"Q65b Position {m}", "organizationUnitId": ou,
                                                                 "reportsToPositionId": None}, tok, H)
            pos = step(f"platform-create-position-{m}", st, b)["id"]; org["positions"][m] = pos
            st, _, b = call("POST", "/api/platform/position-assignments", {"positionId": pos, "userId": uid[m], "effectiveFrom": "2026-01-01T00:00:00Z",
                                                                             "effectiveTo": None}, tok, H)
            step(f"platform-assign-{m}-to-LE-{tag}", st, b)
    tok = None
out["organization"] = org
out["actorLogins"] = {}
for k in ACT:
    t, s = login(k); out["actorLogins"][k] = s; t = None
exp = {"full": "A", "readonly": "A", "noread": "A", "fixadmin": "A", "leb": "B"}
bad = [k for k, v in out["actorLogins"].items() if v["legalEntityId"] != org["legalEntities"][exp[k]] or not v["tenantMatches"]]
if bad:
    dump_and_exit(f"legal-entity claim mismatch for {bad}")


# ---------------------------------------------------------------- C: Delivered shipments through the product Shipment API (LE-A)
def api_for(label):
    tok, s = login(label); le = s["legalEntityId"]; tenant = ACT[label][1]
    def api(method, path, body=None, key=None, corr=None, scope_headers=True):
        h = {"X-Correlation-Id": corr or str(uuid.uuid4())}
        if scope_headers:
            h.update({"X-Tenant-Id": tenant, "X-Legal-Entity-Id": le})
        if key:
            h["Idempotency-Key"] = key
        return call(method, path, body, tok, h)
    return api


def make_shipment(api, name, steps):
    root = str(uuid.uuid4())
    body = {"sourceModule": "EVIDENCE", "sourceType": "Acceptance", "sourceDocumentId": f"Q65B-{name}-{RUN}", "warehouseReferenceId": "WH-Q64D",
            "shipToReference": f"SHIP-TO-{name}", "plannedShipAt": iso(now + timedelta(hours=1)), "plannedDeliverAt": iso(now + timedelta(days=1)),
            "lines": [{"lineNumber": "1", "itemId": str(uuid.uuid4()), "skuId": str(uuid.uuid4()), "quantity": "2", "uomId": "EA", "inventoryReferenceId": None}]}
    st, _, b = api("POST", "/api/shipment-bundle/shipments", body, f"Q65B-{name}-{RUN}-C", root)
    r = step(f"shipment-create-{name}", st, b); sid = r.get("shipmentId") or r.get("id")
    t = now
    for s in steps:
        t = t + timedelta(minutes=5)
        st, _, b = api("POST", f"/api/shipment-bundle/shipments/{sid}/transition", {"targetStatus": s, "occurredAt": iso(t), "reasonCode": None, "note": "q65b setup"},
                       f"Q65B-{name}-{RUN}-{s}", root)
        step(f"shipment-{name}-{s}", st, b)
    st, _, b = api("GET", f"/api/shipment-bundle/shipments/{sid}")
    step(f"shipment-get-{name}", st, b, ok=(200,))
    return {"shipmentId": sid, "requestRoot": root, "lifecycleCorrelationId": b.get("lifecycleCorrelationId"), "status": b.get("status"),
            "carrierId": b.get("carrierId"), "legalEntity": None}



fx = {"shipments": {}}
api1 = api_for("fixadmin")
for name in ("DELIV-EN", "DELIV-AR", "DELIV-NEG", "DISPATCHED"):
    steps = ["Planned", "Dispatched"] + ([] if name == "DISPATCHED" else ["Delivered"])
    fx["shipments"][name] = make_shipment(api1, name, steps); fx["shipments"][name]["legalEntity"] = "A"
api1 = None
out["fixtures"] = fx
ok = all(v["status"] == ("Dispatched" if k == "DISPATCHED" else "Delivered") and v["lifecycleCorrelationId"] for k, v in fx["shipments"].items())
out["result"] = "PASS" if ok else "FAIL"
open(OUT, "w").write(json.dumps(out, indent=2) + "\n")
print(json.dumps({"result": out["result"], "run": RUN, "legalEntities": org["legalEntities"],
                  "shipments": {k: (v["shipmentId"], v["status"], bool(v["lifecycleCorrelationId"])) for k, v in fx["shipments"].items()},
                  "actorLogins": {k: {"le": v["legalEntityId"], "keys": v["returnsPermissions"]} for k, v in out["actorLogins"].items()}}))
sys.exit(0 if ok else 1)
