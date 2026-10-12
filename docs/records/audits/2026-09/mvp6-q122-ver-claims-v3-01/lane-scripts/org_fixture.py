#!/usr/bin/env python3
"""Q122 lane fixture 2 (copy of the Q64d fixture: names Q122, fixture-admin role also holds claims.decide as in Q64e, extra OPEN2 claim for CU-25c) — identities' grants, organisation, shipments, carrier and seed claims (SETUP ONLY).

Run by the lane supervisor (task harness), which supplies ACTOR_PW_<LABEL> (K07 already rotated these passwords into
the lane Auth DB). Nothing here is acceptance; acceptance runs in the browser harness afterwards.
Method (adapted from A12 VER-02 scripts/fixture_prepare.py + auth_fixture.js + lane-scripts/shipments_fixture.py):
  A  lane Auth DB (no product API grants roles to a tenant user): per tenant, roles with EXISTING catalog keys only
     (the keys arrive through module self-registration → Platform → Auth sync; this script waits for them and never
     inserts a permission document), members by e-mail.
  B  product APIs through the lane Gateway with each fixture admin's real Auth token: MDM legal entities (create +
     activate), Platform organisation units, one position per actor, position assignments (this gives the
     legal_entity_id claim).
  C  product APIs through the lane Gateway with the T1/T2 fixture admins' real tokens: one carrier (Carrier API);
     shipments (Shipment API: create → Planned → Dispatched); seed claims (Claims API, correlation = shipment root).
  D  lane Supply Chain DB, setup-only, each with a before/after value in the output (no product API exists in this
     composition): (1) link the carrier to shipment CARRIER (Shipment.CarrierId; the Loads producer uptake that would set
     it is not in this recipe); (2) soft-delete shipment SOFTDEL (IsDeleted/DeletedAt) for the CU-14 soft-deleted case.
Args: <gateway port> <lane mongo port> <db suffix> <out json>
Output: ids, roots, statuses, claim names and booleans only — never a password, hash, token or cookie.
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
       "leb": ("bob.johnson.t97@diten.com", T1), "fixadmin": ("alice.williams.t97@diten.com", T1),
       "t2user": ("john.doe.def@diten.com", T2), "t2admin": ("alice.williams.def@diten.com", T2)}
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
                 "claimsPermissions": sorted(p for p in perms if p.startswith("supplychain.claims")),
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
CLAIMS = ["supplychain.claims.read", "supplychain.claims.create", "supplychain.claims.investigate", "supplychain.claims.decide", "supplychain.claims.settle"]
ORG = ["mdm.legal-entities.create", "mdm.legal-entities.read", "mdm.legal-entities.update", "platform.organization-units.create",
       "platform.organization-units.read", "platform.positions.create", "platform.positions.read", "platform.position-assignments.create",
       "platform.position-assignments.read", "auth.users.lookup-validation"]
SHIP = ["supplychain.shipments.read", "supplychain.shipments.create", "supplychain.shipments.dispatch"]
CARR = ["supplychain.carriers.read", "supplychain.carriers.create"]
NEED = sorted(set(CLAIMS + ORG + SHIP + CARR))
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
  if(!rd){ rd=Object.assign({},roleTpl,{_id:UUID(),TenantId:T,Name:r.name,DisplayName:r.name,Description:'Q122 lane fixture role',IsSystem:false,CreatedBy:'q122-lane-fixture',UpdatedAt:null,UpdatedBy:null}); d.roles.insertOne(rd); }
  const perms=d.permissions.find({Key:{$in:r.keys},IsDeleted:false}).toArray();
  if(perms.length!==r.keys.length) throw new Error('catalog '+r.name+' '+perms.length+'/'+r.keys.length);
  let added=0;
  for(const p of perms){ if(d.rolePermissions.findOne({TenantId:T,RoleId:rd._id,PermissionId:p._id,IsDeleted:false})) continue;
    d.rolePermissions.insertOne(Object.assign({},rpTpl,{_id:UUID(),TenantId:T,RoleId:rd._id,PermissionId:p._id,AssignedAt:new Date(),AssignedBy:'q122-lane-fixture',CreatedBy:'q122-lane-fixture',GrantSource:rpTpl.GrantSource,SourceModuleCode:null})); added++; }
  for(const m of r.members){ const uid=UUID(res.users[m].userId);
    if(d.userRoles.findOne({TenantId:T,UserId:uid,RoleId:rd._id,IsDeleted:false})) continue;
    d.userRoles.insertOne(Object.assign({},urTpl,{_id:UUID(),TenantId:T,UserId:uid,RoleId:rd._id,AssignedAt:new Date(),AssignedBy:'q122-lane-fixture',CreatedBy:'q122-lane-fixture'})); }
  res.roles[r.name+'@'+r.tenant.slice(0,8)]={reused,keys:r.keys,grantsAdded:added,members:r.members};
}
print(JSON.stringify(res));
"""
ROLES = [
    {"tenant": T1, "name": "Q122ClaimsFull", "keys": CLAIMS + ["supplychain.shipments.read"], "members": ["full", "leb"]},
    {"tenant": T1, "name": "Q122ClaimsReadOnly", "keys": ["supplychain.claims.read"], "members": ["readonly"]},
    {"tenant": T1, "name": "Q122ShipmentsReadOnly", "keys": ["supplychain.shipments.read"], "members": ["noread"]},
    {"tenant": T1, "name": "Q122FixtureAdmin", "keys": ORG + SHIP + CARR + CLAIMS[:4], "members": ["fixadmin"]},
    {"tenant": T2, "name": "Q122ClaimsFull", "keys": CLAIMS + ["supplychain.shipments.read"], "members": ["t2user"]},
    {"tenant": T2, "name": "Q122FixtureAdmin", "keys": ORG + SHIP + CARR + CLAIMS[:4], "members": ["t2admin"]},
]
auth = mongo(ROLESJS, {"port": MPORT, "sfx": SFX, "actors": ACT, "roles": ROLES})
out["authRoles"] = auth["roles"]; uid = {k: v["userId"] for k, v in auth["users"].items()}
out["userIds"] = uid

# ---------------------------------------------------------------- B: organisation through product APIs
org = {"legalEntities": {}, "orgUnits": {}, "positions": {}}
plan = {T1: ("fixadmin", {"A": ["full", "readonly", "noread", "fixadmin"], "B": ["leb"]}), T2: ("t2admin", {"T2": ["t2user", "t2admin"]})}
for tenant, (admin, les) in plan.items():
    tok, s = login(admin); out.setdefault("adminLoginsBeforeOrg", []).append(s)
    H = {"X-Tenant-Id": tenant}
    for tag, members in les.items():
        st, _, b = call("POST", "/api/legal-entities", {"code": f"Q122-LE-{tag}-{RUN}", "legalName": f"Q122 Legal Entity {tag}", "displayName": f"LE-{tag}",
                                                        "legalFormCode": "LLC", "organizationRoleCode": None, "countryCode": "TR", "baseCurrencyCode": "TRY",
                                                        "statutoryStatus": None}, tok, H)
        le = step(f"mdm-create-le-{tag}", st, b)["id"]; org["legalEntities"][tag] = le
        st, _, b = call("PATCH", f"/api/legal-entities/{le}/activate", {}, tok, H); step(f"mdm-activate-le-{tag}", st, b)
        st, _, b = call("POST", "/api/platform/organization-units", {"code": f"Q122-OU-{tag}-{RUN}", "name": f"Q122 OU {tag}", "legalEntityId": le,
                                                                      "parentOrganizationUnitId": None}, tok, H)
        ou = step(f"platform-create-ou-{tag}", st, b)["id"]; org["orgUnits"][tag] = ou
        for m in members:
            st, _, b = call("POST", "/api/platform/positions", {"code": f"Q122-POS-{m}-{RUN}", "name": f"Q122 Position {m}", "organizationUnitId": ou,
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
exp = {"full": "A", "readonly": "A", "noread": "A", "fixadmin": "A", "leb": "B", "t2user": "T2", "t2admin": "T2"}
bad = [k for k, v in out["actorLogins"].items() if v["legalEntityId"] != org["legalEntities"][exp[k]] or not v["tenantMatches"]]
if bad:
    dump_and_exit(f"legal-entity claim mismatch for {bad}")

# ---------------------------------------------------------------- C: carrier, shipments, seed claims (product APIs)
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
    body = {"sourceModule": "EVIDENCE", "sourceType": "Acceptance", "sourceDocumentId": f"Q122-{name}-{RUN}", "warehouseReferenceId": "WH-Q122",
            "shipToReference": f"SHIP-TO-{name}", "plannedShipAt": iso(now + timedelta(hours=1)), "plannedDeliverAt": iso(now + timedelta(days=1)),
            "lines": [{"lineNumber": "1", "itemId": str(uuid.uuid4()), "skuId": str(uuid.uuid4()), "quantity": "2", "uomId": "EA", "inventoryReferenceId": None}]}
    st, _, b = api("POST", "/api/shipment-bundle/shipments", body, f"Q122-{name}-{RUN}-C", root)
    r = step(f"shipment-create-{name}", st, b); sid = r.get("shipmentId") or r.get("id")
    t = now
    for s in steps:
        t = t + timedelta(minutes=5)
        st, _, b = api("POST", f"/api/shipment-bundle/shipments/{sid}/transition", {"targetStatus": s, "occurredAt": iso(t), "reasonCode": None, "note": "q122 setup"},
                       f"Q122-{name}-{RUN}-{s}", root)
        step(f"shipment-{name}-{s}", st, b)
    st, _, b = api("GET", f"/api/shipment-bundle/shipments/{sid}")
    step(f"shipment-get-{name}", st, b, ok=(200,))
    return {"shipmentId": sid, "requestRoot": root, "lifecycleCorrelationId": b.get("lifecycleCorrelationId"), "status": b.get("status"),
            "carrierId": b.get("carrierId"), "legalEntity": None}


def make_claim(api, name, ship, amount, target=None):
    root = ship["lifecycleCorrelationId"]
    st, _, b = api("POST", "/api/shipment-bundle/claims", {"shipmentId": ship["shipmentId"], "reasonCode": f"SEED-{name}", "claimedAmount": amount, "currency": "EUR"},
                   f"Q122-CL-{name}-{RUN}", root, scope_headers=False)
    r = step(f"claim-create-{name}", st, b); cid = r.get("claimId")
    rec = {"claimId": cid, "claimNumber": r.get("claimNumber"), "shipmentId": ship["shipmentId"], "status": r.get("status"), "claimedAmount": amount}
    # Q64d: target may be one status or a list of (status, approvedAmount-or-None) steps
    for tgt, appr in ([(target, None)] if isinstance(target, str) else (target or [])):
        body = {"targetStatus": tgt, "occurredAt": iso(datetime.now(timezone.utc))}
        if appr is not None:
            body["approvedAmount"] = appr
        st, _, b = api("POST", f"/api/shipment-bundle/claims/{cid}/transition", body, f"Q122-CL-{name}-{RUN}-{tgt}", root, scope_headers=False)
        rec["status"] = step(f"claim-{name}-{tgt}", st, b).get("status")
    return rec


fx = {"shipments": {}, "claims": {}, "carrier": None}
api1 = api_for("fixadmin")
st, _, b = api1("POST", "/api/shipment-bundle/carriers", {"carrierCode": f"Q122-CAR-{RUN}", "displayName": "Q122 Carrier", "supportedModes": ["Road"]}, f"Q122-CAR-{RUN}")
fx["carrier"] = step("carrier-create", st, b).get("carrierId")
for name, steps in (("DISPATCHED", ["Planned", "Dispatched"]), ("CARRIER", ["Planned", "Dispatched"]), ("DRAFT", []),
                    ("SEED", ["Planned", "Dispatched"]), ("SOFTDEL", ["Planned", "Dispatched"]),
                    # Q64d: CU-18 root seam (root edited in the lane DB by the CU-18 phase) and CU-19/CU-20/CU-22 targets
                    ("NULLROOT", ["Planned", "Dispatched"]), ("BADROOT", ["Planned", "Dispatched"]), ("FLOW", ["Planned", "Dispatched"])):
    fx["shipments"][name] = make_shipment(api1, name, steps); fx["shipments"][name]["legalEntity"] = "A"
fx["claims"]["OPEN1"] = make_claim(api1, "OPEN1", fx["shipments"]["SEED"], "120.00")
fx["claims"]["INV1"] = make_claim(api1, "INV1", fx["shipments"]["SEED"], "300.00", "Investigating")
fx["claims"]["SOFTDEL1"] = make_claim(api1, "SOFTDEL1", fx["shipments"]["SEED"], "75.00")
fx["claims"]["OPEN2"] = make_claim(api1, "OPEN2", fx["shipments"]["SEED"], "130.00")  # Q122: CU-25c keyboard target (as Q64e)
# Q64d: CU-20 approval-amount targets (claimed 250.00, Investigating) and CU-22 settle target (Approved)
for n in ("APPR0", "APPRNEG0", "APPR250", "APPRBAD"):
    fx["claims"][n] = make_claim(api1, n, fx["shipments"]["FLOW"], "250.00", "Investigating")
fx["claims"]["SETTLE1"] = make_claim(api1, "SETTLE1", fx["shipments"]["FLOW"], "250.00", [("Investigating", None), ("Approved", "200.00")])
api1 = None
api2 = api_for("t2admin")
fx["shipments"]["T2SHIP"] = make_shipment(api2, "T2SHIP", ["Planned", "Dispatched"]); fx["shipments"]["T2SHIP"]["legalEntity"] = "T2"
fx["claims"]["T2CLAIM"] = make_claim(api2, "T2CLAIM", fx["shipments"]["T2SHIP"], "999.00")
api2 = None

# ---------------------------------------------------------------- D: lane-DB setup writes (no product API in this recipe)
DJS = PORTGUARD + r"""
const d=db.getSiblingDB('DitenSupplyChain_'+a.sfx); const res={};
const byId=(c,id)=>({$or:[{_id:UUID(id)},{_id:id}]});
const s=d.sce_shipments; const beforeC=s.findOne(byId(s,a.carrierShip));
res.carrierLink={shipmentId:a.carrierShip,before:beforeC?beforeC.CarrierId??null:'missing'};
const r1=s.updateOne(byId(s,a.carrierShip),{$set:{CarrierId:UUID(a.carrier)}});
const afterC=s.findOne(byId(s,a.carrierShip)); res.carrierLink.modified=r1.modifiedCount; res.carrierLink.after=afterC.CarrierId?afterC.CarrierId.toString():null;
if(r1.modifiedCount!==1){ const r1b=s.updateOne(byId(s,a.carrierShip),{$set:{CarrierId:a.carrier}}); res.carrierLink.retryAsString=r1b.modifiedCount; }
const beforeD=s.findOne(byId(s,a.softShip)); res.softDelete={shipmentId:a.softShip,before:{IsDeleted:beforeD.IsDeleted===true}};
const r2=s.updateOne(byId(s,a.softShip),{$set:{IsDeleted:true,DeletedAt:new Date()}});
const afterD=s.findOne(byId(s,a.softShip)); res.softDelete.modified=r2.modifiedCount; res.softDelete.after={IsDeleted:afterD.IsDeleted===true,DeletedAtSet:!!afterD.DeletedAt};
const c=d.claims; const beforeK=c.findOne(byId(c,a.softClaim)); res.claimSoftDelete={claimId:a.softClaim,before:{IsDeleted:beforeK?beforeK.IsDeleted===true:'missing'}};
const r3=c.updateOne(byId(c,a.softClaim),{$set:{IsDeleted:true,DeletedAt:new Date()}});
const afterK=c.findOne(byId(c,a.softClaim)); res.claimSoftDelete.modified=r3.modifiedCount; res.claimSoftDelete.after={IsDeleted:afterK.IsDeleted===true};
print(JSON.stringify(res));
"""
out["laneDbSetup"] = mongo(DJS, {"port": MPORT, "sfx": SFX, "carrierShip": fx["shipments"]["CARRIER"]["shipmentId"], "carrier": fx["carrier"],
                                 "softShip": fx["shipments"]["SOFTDEL"]["shipmentId"], "softClaim": fx["claims"]["SOFTDEL1"]["claimId"]})
api1 = api_for("fixadmin")
st, _, b = api1("GET", f"/api/shipment-bundle/shipments/{fx['shipments']['CARRIER']['shipmentId']}")
fx["shipments"]["CARRIER"]["carrierId"] = (b or {}).get("carrierId"); fx["shipments"]["CARRIER"]["getAfterLink"] = st
st, _, b = api1("GET", f"/api/shipment-bundle/shipments/{fx['shipments']['SOFTDEL']['shipmentId']}")
fx["shipments"]["SOFTDEL"]["getAfterSoftDelete"] = st; fx["shipments"]["SOFTDEL"]["errorCode"] = ((b or {}).get("error") or {}).get("code")
api1 = None
out["fixtures"] = fx
ok = fx["shipments"]["CARRIER"]["carrierId"] == fx["carrier"] and fx["shipments"]["SOFTDEL"]["getAfterSoftDelete"] == 404
out["result"] = "PASS" if ok else "FAIL"
open(OUT, "w").write(json.dumps(out, indent=2) + "\n")
print(json.dumps({"result": out["result"], "run": RUN, "shipments": {k: v["shipmentId"] for k, v in fx["shipments"].items()},
                  "claims": {k: v["claimId"] for k, v in fx["claims"].items()}}))
sys.exit(0 if ok else 1)
