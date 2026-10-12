#!/usr/bin/env python3
"""Q88b lane fixture 1 (K07pre) — derived from the Q64b/Q84b tenant_fixture.py (Platform tenant records, lane DB only), extended:

  A  Platform DB: tenant records for the two seeded Auth tenants (as before) AND for the Capacity FIXTURE tenant
     19200000-0000-4000-8000-000000000001 (cloned from a seeded tenant document; same method as before).
  B  Auth DB: four users cloned from seeded tenant-97c5 users into the fixture tenant (new _id, TenantId, e-mail; everything
     else — incl. the seed password hash, which K07 then rotates — unchanged).

Why (deviation D-2 / finding F-Q88b-1): the Capacity backend accepts a plan, a scenario and an evaluation ONLY in the bounded
fixture scope hard-coded in DemandFixtureReader.cs:6-7 / ConstraintFixtureReader.cs (tenant 19200000-…-01, legal entity
19200000-…-02). No seeded tenant has that id, so without this lane-only test data every create returns 422.
Never 27017; refuses any port other than the lane port. Args: <lane mongo port> <db suffix> <out json path>
Output: ids, e-mails and created/existing flags only — never a hash or password.
"""
import json, subprocess, sys

port, suffix, out = sys.argv[1], sys.argv[2], sys.argv[3]
if port == "27017":
    sys.exit("refusing 27017")
JS = r"""
const a = JSON.parse(process.env.FX);
const port = db.adminCommand({getCmdLineOpts:1}).parsed.net.port;
if (port === 27017 || String(port) !== a.port) { print(JSON.stringify({error:'not the lane port '+port})); quit(2); }
const p = db.getSiblingDB('DitenPlatform_' + a.suffix);
const res = {port, db: 'DitenPlatform_' + a.suffix, seededTenantCount: p.tenants.countDocuments({}), tenants: {}, users: {}};
const tpl = p.tenants.findOne({});
for (const [tag, id, code] of [['T1','97c59330-dbc4-4665-b29c-0c26dbb5cc93','T97C5'], ['T2','00000000-0000-0000-0000-000000000001','TDEF1'], ['TF','19200000-0000-4000-8000-000000000001','TCAP192']]) {
  const T = UUID(id);
  const existing = p.tenants.findOne({_id: T});
  if (existing) { res.tenants[tag] = {id, created: false, status: existing.Status, isDeleted: existing.IsDeleted === true}; continue; }
  if (!tpl) { res.tenants[tag] = {id, created: false, error: 'no template tenant document in lane Platform'}; continue; }
  p.tenants.insertOne(Object.assign({}, tpl, {_id: T, CreatedBy: 'q88b-lane-fixture', Code: code, Slug: code.toLowerCase(),
    Name: 'Lane tenant ' + code + ' (Q88b)', DisplayName: 'Lane tenant ' + code + ' (Q88b)', Domain: code.toLowerCase() + '.lane.invalid',
    Status: 1, TenantType: 0, IsDeleted: false}));
  const t = p.tenants.findOne({_id: T});
  res.tenants[tag] = {id, created: true, status: t.Status, tenantType: t.TenantType, isDeleted: t.IsDeleted === true};
}
// B: Auth users for the fixture tenant
const d = db.getSiblingDB('DitenAuth_' + a.suffix); const TF = UUID('19200000-0000-4000-8000-000000000001'); const T1 = UUID('97c59330-dbc4-4665-b29c-0c26dbb5cc93');
for (const [src, email] of a.users) {
  const have = d.users.countDocuments({Email: email, TenantId: TF});
  if (have > 0) { res.users[email] = {created: false, existing: have}; continue; }
  const u = d.users.findOne({Email: src, TenantId: T1, IsDeleted: false});
  if (!u) { res.users[email] = {created: false, error: 'seed user ' + src + ' missing'}; continue; }
  const c = Object.assign({}, u, {_id: UUID(), TenantId: TF, Email: email, CreatedBy: 'q88b-lane-fixture'});
  const changed = ['_id', 'TenantId', 'Email', 'CreatedBy'];
  for (const k of Object.keys(c)) if (typeof c[k] === 'string' && k !== 'Email' && c[k].toLowerCase() === src.toLowerCase()) { c[k] = (c[k] === c[k].toUpperCase()) ? email.toUpperCase() : email; changed.push(k); }
  d.users.insertOne(c);
  res.users[email] = {created: true, clonedFrom: src, fieldsChanged: changed, fieldNames: Object.keys(c).filter((k) => !/hash|secret|token/i.test(k)).length};
}
print(JSON.stringify(res));
"""
USERS = [["john.doe.t97@diten.com", "cap.full.q88b@diten.com"], ["jane.smith.t97@diten.com", "cap.readonly.q88b@diten.com"],
         ["charlie.brown.t97@diten.com", "cap.noread.q88b@diten.com"], ["alice.williams.t97@diten.com", "cap.admin.q88b@diten.com"]]
r = subprocess.run(["mongosh", "--quiet", f"mongodb://127.0.0.1:{port}/?directConnection=true", "--eval", JS],
                   capture_output=True, text=True, env={"PATH": "/opt/homebrew/bin:/usr/bin:/bin", "HOME": "/tmp",
                                                        "FX": json.dumps({"port": port, "suffix": suffix, "users": USERS})})
line = [l for l in r.stdout.splitlines() if l.startswith("{")]
res = json.loads(line[-1]) if line else {"error": "no output", "rc": r.returncode, "stderr": r.stderr[-500:]}
open(out, "w").write(json.dumps(res, indent=2) + "\n")
print(json.dumps(res))
bad = "error" in res or any("error" in v for v in res.get("tenants", {}).values()) or any("error" in v for v in res.get("users", {}).values())
sys.exit(1 if bad else 0)
