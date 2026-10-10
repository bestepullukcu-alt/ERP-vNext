#!/usr/bin/env python3
"""Q362 lane fixture (method of Q185 nav_fixture.py parts A + B; users created by Auth's own invite endpoint).
Prints ids, statuses and booleans only — never a password, hash, token or cookie. Secrets live in run/*.pw (mode 600)."""
import base64, json, os, secrets, string, subprocess, sys, urllib.error, urllib.request, uuid

E = os.path.dirname(os.path.abspath(__file__))
GW = "http://127.0.0.1:5000"; AUTH = "http://127.0.0.1:5056"
MPORT = "57362"; T = "00000000-0000-0000-0000-000000000001"
os.umask(0o077)
KEY = json.load(open(E + "/src/services/Diten.AuthService/src/Diten.AuthService.Api/appsettings.Development.json"))["InternalEventAuth"]["ApiKey"]
USERS = {"admin": "q362-admin@example.test", "fix": "q362-fix@example.test", "nokey": "q362-nokey@example.test"}
ORG = ["mdm.legal-entities.create", "mdm.legal-entities.read", "mdm.legal-entities.update", "platform.organization-units.create",
       "platform.organization-units.read", "platform.positions.create", "platform.positions.read", "platform.position-assignments.create",
       "platform.position-assignments.read", "auth.users.lookup-validation"]
out = {"steps": []}


def call(base, method, path, body=None, token=None, headers=None):
    h = {"Content-Type": "application/json", "X-Correlation-Id": str(uuid.uuid4())}
    if token: h["Authorization"] = "Bearer " + token
    h.update(headers or {})
    data = None if body is None else json.dumps(body).encode()
    req = urllib.request.Request(base + path, data=data, headers=h, method=method)
    try:
        with urllib.request.urlopen(req, timeout=60) as r:
            raw = r.read().decode(); return r.status, (json.loads(raw) if raw else None)
    except urllib.error.HTTPError as e:
        raw = e.read().decode()
        try: return e.code, json.loads(raw)
        except Exception: return e.code, {"unparsed": raw[:200]}


def step(name, st, body, ok=(200, 201, 204)):
    rec = {"step": name, "status": st, "ok": st in ok}
    if isinstance(body, dict):
        d = body.get("data")
        rec["id"] = d if isinstance(d, str) else (d.get("id") if isinstance(d, dict) else None)
        if st not in ok: rec["error"] = {k: v for k, v in body.items() if k not in ("data", "temporaryPassword", "accessToken", "refreshToken")}
    out["steps"].append(rec)
    if st not in ok:
        out["stopped"] = name; print(json.dumps(out, indent=1)); sys.exit(1)
    return rec


def claims(tok):
    p = tok.split(".")[1]; p += "=" * (-len(p) % 4); return json.loads(base64.urlsafe_b64decode(p))


def newpw():
    a = string.ascii_letters + string.digits
    return "Q3!" + "".join(secrets.choice(a) for _ in range(20)) + "a9Z#"


def mongo(js):
    r = subprocess.run(["mongosh", "--quiet", "--port", MPORT, "--eval", js], capture_output=True, text=True)
    lines = [l for l in r.stdout.splitlines() if l.startswith("{")]
    if r.returncode != 0 or not lines: out["stopped"] = "mongosh " + r.stderr[-300:]; print(json.dumps(out)); sys.exit(1)
    return json.loads(lines[-1])


def login(label):
    st, b = call(GW, "POST", "/api/tenant-auth/login", {"email": USERS[label], "password": open(f"{E}/run/{label}.pw").read(), "rememberMe": False},
                 None, {"X-Tenant-Id": T})
    step(f"login-{label}", st, b)
    tok = b["data"]["accessToken"]; c = claims(tok)
    perms = c.get("permission", []); perms = [perms] if isinstance(perms, str) else perms
    return tok, {"legal_entity_id": c.get("legal_entity_id"), "pwd_change_required": c.get("pwd_change_required"),
                 "shipmentKeys": sorted(p for p in perms if p.startswith("supplychain.shipments")), "permissionCount": len(perms)}


# 1. users through Auth's invite endpoint (the call Platform makes for a tenant admin); temp password straight into a 600 file
for label, email in USERS.items():
    st, b = call(AUTH, "POST", "/internal/events/tenant-admin-invited",
                 {"tenantId": T, "adminUserId": str(uuid.uuid4()), "tenantCode": "SYSTEM", "tenantName": "Platform Admin Tenant",
                  "email": email, "name": "Q362 " + label}, None, {"X-Internal-Api-Key": KEY})
    step(f"invite-{label}", st, b)
    open(f"{E}/run/{label}.pw", "w").write(b["temporaryPassword"])
    # 2. forced first-login change through the gateway, as a user would; new password generated here, never printed
    st, b = call(GW, "POST", "/api/tenant-auth/login", {"email": email, "password": open(f"{E}/run/{label}.pw").read(), "rememberMe": False}, None, {"X-Tenant-Id": T})
    step(f"first-login-{label}", st, b)
    tok = b["data"]["accessToken"]; out.setdefault("firstLogin", {})[label] = {"requiresPasswordChange": b["data"].get("requiresPasswordChange")}
    np = newpw()
    st, b = call(GW, "POST", "/api/tenant-auth/change-password/forced", {"currentPassword": open(f"{E}/run/{label}.pw").read(), "newPassword": np, "rememberMe": False}, tok, {"X-Tenant-Id": T})
    step(f"forced-change-{label}", st, b)
    open(f"{E}/run/{label}.pw", "w").write(np); np = None; tok = None

# 3. part A (Q185 method, lane Auth DB): fixture role with existing catalog keys for the org setup; nokey loses its Admin role
A = mongo("""
const d=db.getSiblingDB('q362_auth'); const T=UUID('%s'); const res={};
const uid=e=>d.users.findOne({Email:e,TenantId:T,IsDeleted:false})._id;
const roleTpl=d.roles.findOne({IsDeleted:false}); const rpTpl=d.rolePermissions.findOne({}); const urTpl=d.userRoles.findOne({});
let r=d.roles.findOne({TenantId:T,Name:'Q362FixtureAdmin'});
if(!r){ r=Object.assign({},roleTpl,{_id:UUID(),TenantId:T,Name:'Q362FixtureAdmin',DisplayName:'Q362FixtureAdmin',Description:'Q362 lane fixture role',IsSystem:false,CreatedBy:'q362-lane-fixture',UpdatedAt:null,UpdatedBy:null}); d.roles.insertOne(r); }
const perms=d.permissions.find({Key:{$in:%s},IsDeleted:false}).toArray(); res.orgKeys=perms.length;
for(const p of perms){ if(!d.rolePermissions.findOne({TenantId:T,RoleId:r._id,PermissionId:p._id})) d.rolePermissions.insertOne(Object.assign({},rpTpl,{_id:UUID(),TenantId:T,RoleId:r._id,PermissionId:p._id,AssignedAt:new Date(),AssignedBy:'q362-lane-fixture',CreatedBy:'q362-lane-fixture',SourceModuleCode:null})); }
const fu=uid('%s'); if(!d.userRoles.findOne({TenantId:T,UserId:fu,RoleId:r._id})) d.userRoles.insertOne(Object.assign({},urTpl,{_id:UUID(),TenantId:T,UserId:fu,RoleId:r._id,AssignedAt:new Date(),AssignedBy:'q362-lane-fixture',CreatedBy:'q362-lane-fixture'}));
const admin=d.roles.findOne({TenantId:T,Name:'Admin'}); const nk=uid('%s');
res.nokeyAdminRolesRemoved=d.userRoles.deleteMany({TenantId:T,UserId:nk,RoleId:admin._id}).deletedCount;
res.nokeyRolesLeft=d.userRoles.countDocuments({TenantId:T,UserId:nk,IsDeleted:{$ne:true}});
print(JSON.stringify(res));
""" % (T, json.dumps(ORG), USERS["fix"], USERS["nokey"]))
out["partA"] = A

# 4. part B (Q185 method): LE in MDM, OU + position + assignment in Platform, through the gateway with the fixture admin's real token
uids = mongo("const d=db.getSiblingDB('q362_auth');const T=UUID('%s');print(JSON.stringify(Object.fromEntries(%s.map(([l,e])=>[l,d.users.findOne({Email:e,TenantId:T})._id.toString().replace(/^UUID\\(\"|\"\\)$/g,'')]))))"
             % (T, json.dumps(list(USERS.items()))))
tok, s = login("fix"); out["fixLogin"] = s
H = {"X-Tenant-Id": T}; RUN = uuid.uuid4().hex[:6]
st, b = call(GW, "POST", "/api/legal-entities", {"code": f"Q362-LE-{RUN}", "legalName": "Q362 Legal Entity", "displayName": "Q362 LE", "legalFormCode": "LLC",
                                                 "organizationRoleCode": None, "countryCode": "TR", "baseCurrencyCode": "TRY", "statutoryStatus": None}, tok, H)
le = step("mdm-create-le", st, b)["id"]
st, b = call(GW, "PATCH", f"/api/legal-entities/{le}/activate", {}, tok, H); step("mdm-activate-le", st, b)
st, b = call(GW, "POST", "/api/platform/organization-units", {"code": f"Q362-OU-{RUN}", "name": "Q362 OU", "legalEntityId": le, "parentOrganizationUnitId": None}, tok, H)
ou = step("platform-create-ou", st, b)["id"]
for m in ("admin", "nokey"):
    st, b = call(GW, "POST", "/api/platform/positions", {"code": f"Q362-POS-{m}-{RUN}", "name": f"Q362 Position {m}", "organizationUnitId": ou, "reportsToPositionId": None}, tok, H)
    pos = step(f"platform-create-position-{m}", st, b)["id"]
    st, b = call(GW, "POST", "/api/platform/position-assignments", {"positionId": pos, "userId": uids[m], "effectiveFrom": "2026-01-01T00:00:00Z", "effectiveTo": None}, tok, H)
    step(f"platform-assign-{m}", st, b)
tok = None
out["legalEntityId"] = le; out["orgUnitId"] = ou; out["userIds"] = uids
# 5. what each actor's token now carries
out["actorLogins"] = {}
for m in ("admin", "nokey"):
    t, s = login(m); out["actorLogins"][m] = s; t = None
out["result"] = "PASS" if out["actorLogins"]["admin"]["legal_entity_id"] == le and len(out["actorLogins"]["admin"]["shipmentKeys"]) == 5 and not out["actorLogins"]["nokey"]["shipmentKeys"] else "CHECK"
open(E + "/results/fixture.json", "w").write(json.dumps(out, indent=1) + "\n")
print(json.dumps({k: out[k] for k in ("partA", "fixLogin", "legalEntityId", "actorLogins", "result", "firstLogin")}, indent=1))
