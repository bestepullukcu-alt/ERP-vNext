#!/usr/bin/env python3
"""A12 runtime VER-02 fixture preparation (run by lane_supervisor.py `run`, which supplies ACTOR_PW_A/B/LEB in env).

1. bcrypt(12) hashes of the actor passwords and of a fixture-admin password generated HERE (in memory only),
   via `htpasswd -niBC 12` over stdin; written into the lane Auth DB by auth_fixture.js (hash passed via env).
2. Real Auth login of the fixture-admin through the lane Gateway; with its Auth-issued token, the product APIs create
   LE-A and LE-B (MDM, activated), one org unit per LE, one position per org unit (Platform), and position assignments:
   actor-a, actor-b -> LE-A; actor-le-b -> LE-B.
3. Real Auth login of each actor; token decoded in memory to confirm tenant, legal_entity_id and shipment permissions.
Evidence (stdout JSON) holds ids, statuses, claim names and booleans only — no password, hash, token or cookie.
"""
import base64, json, os, secrets, subprocess, sys, urllib.error, urllib.request, uuid

GW = "http://127.0.0.1:5400"
TENANT = "97c59330-dbc4-4665-b29c-0c26dbb5cc93"
HERE = os.path.dirname(os.path.abspath(__file__))
PW = {"A": os.environ["ACTOR_PW_A"], "B": os.environ["ACTOR_PW_B"], "LEB": os.environ["ACTOR_PW_LEB"], "ADM": secrets.token_urlsafe(24)}
EMAIL = {"A": "john.doe.t97@diten.com", "B": "jane.smith.t97@diten.com", "LEB": "bob.johnson.t97@diten.com", "ADM": "alice.williams.t97@diten.com"}
out = {"tenant": TENANT, "steps": []}


def bcrypt12(pw):
    r = subprocess.run(["htpasswd", "-niBC", "12", "x"], input=pw + "\n", capture_output=True, text=True, check=True)
    h = r.stdout.strip().split(":", 1)[1]
    assert h.startswith("$2y$12$")
    return "$2a$" + h[4:]  # same algorithm; $2a$ prefix exactly as Auth PasswordHasher (BCrypt.Net) writes it


def call(method, path, body=None, token=None, extra=None):
    h = {"Content-Type": "application/json", "X-Tenant-Id": TENANT, "X-Correlation-Id": str(uuid.uuid4())}
    if token:
        h["Authorization"] = "Bearer " + token
    h.update(extra or {})
    req = urllib.request.Request(GW + path, data=None if body is None else json.dumps(body).encode(), headers=h, method=method)
    try:
        with urllib.request.urlopen(req, timeout=30) as r:
            raw = r.read().decode(); return r.status, (json.loads(raw) if raw else None)
    except urllib.error.HTTPError as e:
        raw = e.read().decode()
        try:
            return e.code, json.loads(raw)
        except Exception:
            return e.code, {"unparsed": raw[:300]}


def claims(token):
    p = token.split(".")[1]; p += "=" * (-len(p) % 4)
    return json.loads(base64.urlsafe_b64decode(p))


def login(k):
    st, body = call("POST", "/api/tenant-auth/login", {"email": EMAIL[k], "password": PW[k], "rememberMe": False})
    if st != 200:
        raise SystemExit(json.dumps({"login": k, "status": st, "body": {kk: vv for kk, vv in (body or {}).items() if kk != "data"}}))
    tok = body["data"]["accessToken"]; c = claims(tok)
    perms = c.get("permission", []); perms = [perms] if isinstance(perms, str) else perms
    summary = {"actor": k, "status": st, "claimNames": sorted(c.keys()), "tenantMatches": c.get("tenant_id") == TENANT,
               "legalEntityId": c.get("legal_entity_id"), "shipmentPermissions": sorted(p for p in perms if p.startswith("supplychain.shipments")),
               "otherPermissionCount": len([p for p in perms if not p.startswith("supplychain.shipments")])}
    return tok, summary


def step(name, st, body, ok=(200, 201)):
    rec = {"step": name, "status": st, "ok": st in ok}
    if isinstance(body, dict):
        d = body.get("data")
        rec["dataId"] = d if isinstance(d, str) else (d.get("id") if isinstance(d, dict) else None)
        if st not in ok:
            rec["body"] = {k: v for k, v in body.items() if k not in ("data",)}
    out["steps"].append(rec)
    if st not in ok:
        print(json.dumps(out, indent=2)); sys.exit(1)
    return rec.get("dataId")


# 0. lane Platform tenant record (Auth login needs Platform login-settings for the tenant)
r = subprocess.run(["mongosh", "--quiet", "mongodb://127.0.0.1:34994/?directConnection=true", "--file", os.path.join(HERE, "platform_tenant_fixture.js")],
                   capture_output=True, text=True)
if r.returncode != 0:
    print(r.stdout[-2000:], r.stderr[-2000:]); sys.exit(1)
out["platformTenantFixture"] = json.loads(r.stdout.strip().splitlines()[-1])

# 1. identities + roles in the lane Auth DB
env = dict(os.environ); env.update({f"HASH_{k}": bcrypt12(v) for k, v in PW.items()})
r = subprocess.run(["mongosh", "--quiet", "mongodb://127.0.0.1:34994/?directConnection=true", "--file", os.path.join(HERE, "auth_fixture.js")],
                   env=env, capture_output=True, text=True)
env = None
if r.returncode != 0:
    print(r.stdout[-2000:], r.stderr[-2000:]); sys.exit(1)
out["authFixture"] = json.loads(r.stdout.strip().splitlines()[-1])
uid = {k: v["userId"] for k, v in out["authFixture"]["users"].items()}

# 2. fixture-admin creates LE / org / position / assignment data through the product APIs
adm, s = login("ADM"); out["fixtureAdminLogin"] = s
les = {}
RUN = uuid.uuid4().hex[:6]; out["codeSuffix"] = RUN
for tag in ("A", "B"):
    body = {"code": f"A12V02-LE-{tag}-{RUN}", "legalName": f"A12 VER-02 Legal Entity {tag}", "displayName": f"LE-{tag}", "legalFormCode": "LLC",
            "organizationRoleCode": None, "countryCode": "TR", "baseCurrencyCode": "TRY", "statutoryStatus": None}
    st, b = call("POST", "/api/legal-entities", body, adm)
    les[tag] = step(f"mdm-create-le-{tag}", st, b)
    st, b = call("PATCH", f"/api/legal-entities/{les[tag]}/activate", {}, adm)
    step(f"mdm-activate-le-{tag}", st, b, ok=(200, 204))
    st, b = call("GET", f"/api/legal-entities/{les[tag]}/lookup-validation", None, adm)
    step(f"mdm-lookup-validation-le-{tag}", st, b)
    out[f"le{tag}Referenceable"] = (b or {}).get("data", {}).get("referenceable") if isinstance((b or {}).get("data"), dict) else None
ous, pos = {}, {}
for tag in ("A", "B"):
    st, b = call("POST", "/api/platform/organization-units", {"code": f"A12V02-OU-{tag}-{RUN}", "name": f"A12 VER-02 OU {tag}", "legalEntityId": les[tag], "parentOrganizationUnitId": None}, adm)
    ous[tag] = step(f"platform-create-ou-{tag}", st, b)
# one position per actor (a position admits one primary assignment): actor-a, actor-b under OU-A; actor-le-b under OU-B
for actor, tag in (("A", "A"), ("B", "A"), ("LEB", "B")):
    st, b = call("POST", "/api/platform/positions", {"code": f"A12V02-POS-{actor}-{RUN}", "name": f"A12 VER-02 Position {actor}", "organizationUnitId": ous[tag], "reportsToPositionId": None}, adm)
    pos[actor] = step(f"platform-create-position-{actor}-in-OU-{tag}", st, b)
    st, b = call("POST", "/api/platform/position-assignments", {"positionId": pos[actor], "userId": uid[actor], "effectiveFrom": "2026-01-01T00:00:00Z", "effectiveTo": None}, adm)
    step(f"platform-assign-{actor}-to-LE-{tag}", st, b)
out["organization"] = {"orgUnits": ous, "positions": pos}
adm = None
out["legalEntities"] = {"LE-A": les["A"], "LE-B": les["B"]}

# 3. actor logins (real Auth via Gateway) — claims only
out["actorLogins"] = {}
for k in ("A", "B", "LEB"):
    _, s = login(k); out["actorLogins"][k] = s
print(json.dumps(out, indent=2))
