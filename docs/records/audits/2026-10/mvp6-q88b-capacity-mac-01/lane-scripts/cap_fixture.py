#!/usr/bin/env python3
"""Q88b lane fixture 2 (copied from Q88b sop_fixture.py and adapted): actors live in the Capacity FIXTURE tenant 19200000-…-01
(users cloned by tenant_fixture.py); the legal entity created through the MDM API is CLONED in the lane MDM DB under the fixture
legal-entity id 19200000-…-02 (deviation D-2), so the actors' tokens carry the only scope the Capacity backend accepts. One actor
in the platform system tenant gets the Capacity keys for the nav checks (Q185 D-1).
Q88b text: derived from the Q64d org fixture (mvp6-q64d-claims-runtime-01/lane-scripts/org_fixture.py; parts A and B
unchanged in method, Claims/shipment/carrier parts C and D removed). SETUP ONLY; acceptance runs in the browser harness afterwards.
Run by the lane supervisor (task harness), which supplies ACTOR_PW_<LABEL>.
  A  lane Auth DB: per tenant, roles with EXISTING catalog keys only (the four S&OP keys arrive through the S&OP manifest provider
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
TF = "19200000-0000-4000-8000-000000000001"; FIXLE = "19200000-0000-4000-8000-000000000002"
ACT = {"capfull": ("cap.full.q88b@diten.com", TF), "capro": ("cap.readonly.q88b@diten.com", TF), "capnoread": ("cap.noread.q88b@diten.com", TF),
       "capadmin": ("cap.admin.q88b@diten.com", TF), "navfull": ("john.doe.def@diten.com", T2)}
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
                 "sandopPermissions": sorted(p for p in perms if p.startswith("supplychain.capacity")),
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
SANDOP = ["supplychain.capacity-plans.read", "supplychain.capacity-plans.create", "supplychain.capacity-plans.scenario.create", "supplychain.capacity-plans.evaluate"]  # Capacity keys (variable name kept from the Q88b copy)
ORG = ["mdm.legal-entities.create", "mdm.legal-entities.read", "mdm.legal-entities.update", "platform.organization-units.create",
       "platform.organization-units.read", "platform.positions.create", "platform.positions.read", "platform.position-assignments.create",
       "platform.position-assignments.read", "auth.users.lookup-validation"]
SHIP = ["supplychain.shipments.read", "supplychain.shipments.create", "supplychain.shipments.dispatch"]
CARR = ["supplychain.carriers.read", "supplychain.carriers.create"]
NEED = sorted(set(SANDOP + ORG + ["supplychain.shipments.read"]))
WAITJS = PORTGUARD + "const d=db.getSiblingDB('DitenAuth_'+a.sfx);const have=d.permissions.find({Key:{$in:a.keys},IsDeleted:false}).toArray().map(p=>p.Key);print(JSON.stringify({have}));"
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
  if(!rd){ rd=Object.assign({},roleTpl,{_id:UUID(),TenantId:T,Name:r.name,DisplayName:r.name,Description:'Q88b lane fixture role',IsSystem:false,CreatedBy:'q88b-lane-fixture',UpdatedAt:null,UpdatedBy:null}); d.roles.insertOne(rd); }
  const perms=d.permissions.find({Key:{$in:r.keys},IsDeleted:false}).toArray();
  if(perms.length!==r.keys.length) throw new Error('catalog '+r.name+' '+perms.length+'/'+r.keys.length);
  let added=0;
  for(const p of perms){ if(d.rolePermissions.findOne({TenantId:T,RoleId:rd._id,PermissionId:p._id,IsDeleted:false})) continue;
    d.rolePermissions.insertOne(Object.assign({},rpTpl,{_id:UUID(),TenantId:T,RoleId:rd._id,PermissionId:p._id,AssignedAt:new Date(),AssignedBy:'q88b-lane-fixture',CreatedBy:'q88b-lane-fixture',GrantSource:rpTpl.GrantSource,SourceModuleCode:null})); added++; }
  for(const m of r.members){ const uid=UUID(res.users[m].userId);
    if(d.userRoles.findOne({TenantId:T,UserId:uid,RoleId:rd._id,IsDeleted:false})) continue;
    d.userRoles.insertOne(Object.assign({},urTpl,{_id:UUID(),TenantId:T,UserId:uid,RoleId:rd._id,AssignedAt:new Date(),AssignedBy:'q88b-lane-fixture',CreatedBy:'q88b-lane-fixture'})); }
  res.roles[r.name+'@'+r.tenant.slice(0,8)]={reused,keys:r.keys,grantsAdded:added,members:r.members};
}
print(JSON.stringify(res));
"""
ROLES = [
    {"tenant": TF, "name": "Q88bCapacityFull", "keys": SANDOP, "members": ["capfull"]},
    {"tenant": TF, "name": "Q88bCapacityReadOnly", "keys": ["supplychain.capacity-plans.read"], "members": ["capro"]},
    {"tenant": TF, "name": "Q88bNoCapacity", "keys": ["supplychain.shipments.read"], "members": ["capnoread"]},
    {"tenant": TF, "name": "Q88bFixtureAdmin", "keys": ORG, "members": ["capadmin"]},
    {"tenant": T2, "name": "Q88bCapacityFull", "keys": SANDOP, "members": ["navfull"]},
]
auth = mongo(ROLESJS, {"port": MPORT, "sfx": SFX, "actors": ACT, "roles": ROLES})
out["authRoles"] = auth["roles"]; uid = {k: v["userId"] for k, v in auth["users"].items()}
out["userIds"] = uid

# ---------------------------------------------------------------- B: organisation through product APIs
LEJS = PORTGUARD + r"""
const d=db.getSiblingDB('DitenMdm_'+a.sfx); const res={dst:a.dst, collection:null};
for (const name of d.getCollectionNames()) { const c=d.getCollection(name);
  let doc=c.findOne({_id:a.src}); let asString=true; if(!doc){ try{ doc=c.findOne({_id:UUID(a.src)}); asString=false; }catch(e){} }
  if(!doc) continue;
  res.collection=name; res.idType=asString?'string':'uuid';
  const dstId=asString?a.dst:UUID(a.dst);
  res.before=c.countDocuments({_id:dstId});
  if(res.before===0){ const clone=Object.assign({},doc,{_id:dstId}); const changed=['_id'];
    for(const k of Object.keys(clone)){ if(/^code$/i.test(k) && typeof clone[k]==='string'){ clone[k]=a.code; changed.push(k); } }
    c.insertOne(clone); res.fieldsChanged=changed; }
  else { // a2: fixture a1 added a CreatedBy field the MDM LegalEntity class does not have (GET → 500); remove it from the lane clone
    const u=c.updateOne({_id:dstId, CreatedBy:{$exists:true}},{$unset:{CreatedBy:''}}); res.removedExtraField=u.modifiedCount; }
  res.after=c.countDocuments({_id:dstId}); break; }
print(JSON.stringify(res));
"""
org = {"legalEntities": {}, "orgUnits": {}, "positions": {}}
plan = {TF: ("capadmin", {"A": ["capfull", "capro", "capnoread", "capadmin"]})}
for tenant, (admin, les) in plan.items():
    tok, s = login(admin); out.setdefault("adminLoginsBeforeOrg", []).append(s)
    H = {"X-Tenant-Id": tenant}
    for tag, members in les.items():
        st, _, b = call("POST", "/api/legal-entities", {"code": f"Q88B-LE-{tag}-{RUN}", "legalName": f"Q88b Legal Entity {tag}", "displayName": f"LE-{tag}",
                                                        "legalFormCode": "LLC", "organizationRoleCode": None, "countryCode": "TR", "baseCurrencyCode": "TRY",
                                                        "statutoryStatus": None}, tok, H)
        le = step(f"mdm-create-le-{tag}", st, b)["id"]; org["legalEntities"][tag] = le
        st, _, b = call("PATCH", f"/api/legal-entities/{le}/activate", {}, tok, H); step(f"mdm-activate-le-{tag}", st, b)
        # Q88b D-2: clone the activated legal entity in the lane MDM DB under the fixture id (before/after recorded), then use the clone.
        out["laneMdmLegalEntityClone"] = mongo(LEJS, {"port": MPORT, "sfx": SFX, "src": le, "dst": FIXLE, "code": f"Q88B-LE-FIX-{RUN}"})
        if out["laneMdmLegalEntityClone"].get("after") != 1: dump_and_exit("legal entity clone failed")
        st, _, b = call("GET", f"/api/legal-entities/{FIXLE}", None, tok, H); step("mdm-get-fixture-le", st, b, ok=(200,))
        org["legalEntities"]["apiCreated"] = le; le = FIXLE; org["legalEntities"][tag] = le
        st, _, b = call("POST", "/api/platform/organization-units", {"code": f"Q88B-OU-{tag}-{RUN}", "name": f"Q88b OU {tag}", "legalEntityId": le,
                                                                      "parentOrganizationUnitId": None}, tok, H)
        ou = step(f"platform-create-ou-{tag}", st, b)["id"]; org["orgUnits"][tag] = ou
        for m in members:
            st, _, b = call("POST", "/api/platform/positions", {"code": f"Q88B-POS-{m}-{RUN}", "name": f"Q88b Position {m}", "organizationUnitId": ou,
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
exp = {"capfull": "A", "capro": "A", "capnoread": "A", "capadmin": "A"}
bad = [k for k, v in out["actorLogins"].items() if (k in exp and v["legalEntityId"] != org["legalEntities"][exp[k]]) or not v["tenantMatches"]]
if bad:
    dump_and_exit(f"legal-entity claim mismatch for {bad}")


out["result"] = "PASS"
open(OUT, "w").write(json.dumps(out, indent=2) + "\n")
print(json.dumps({"result": "PASS", "run": RUN, "legalEntities": org["legalEntities"],
                  "actorLogins": {k: {"le": v["legalEntityId"], "sandop": v["sandopPermissions"]} for k, v in out["actorLogins"].items()}}))
sys.exit(0)
