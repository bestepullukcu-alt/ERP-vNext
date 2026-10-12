#!/usr/bin/env python3
"""Q64b (Q97 v2.1) lane fixture 1 — Platform tenant records (lane DB only).

Why: Auth tenant login asks Platform for the tenant's login settings; the lane Platform seed may lack the two seeded
Auth tenants (A12 VER-02 SOP-22 §7.5: "97c5 was missing from the lane Platform, so login returned 'temporarily
unavailable'"). There is no product API that creates a tenant record for a seeded Auth tenant, so this writes the
record into the LANE Platform DB only, cloned from a seeded tenant document (same method as A12 VER-02
scripts/platform_tenant_fixture.js). Never 27017; refuses any port other than the lane port.
Run by the lane supervisor (task harness).  Args: <lane mongo port> <db suffix> <out json path>
Output: tenant ids + created/existing flags only.
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
const res = {port, db: 'DitenPlatform_' + a.suffix, seededTenantCount: p.tenants.countDocuments({}), tenants: {}};
const tpl = p.tenants.findOne({});
for (const [tag, id, code] of [['T1','97c59330-dbc4-4665-b29c-0c26dbb5cc93','T97C5'], ['T2','00000000-0000-0000-0000-000000000001','TDEF1']]) {
  const T = UUID(id);
  const existing = p.tenants.findOne({_id: T});
  if (existing) { res.tenants[tag] = {id, created: false, status: existing.Status, isDeleted: existing.IsDeleted === true}; continue; }
  if (!tpl) { res.tenants[tag] = {id, created: false, error: 'no template tenant document in lane Platform'}; continue; }
  p.tenants.insertOne(Object.assign({}, tpl, {_id: T, CreatedBy: 'q64b-lane-fixture', Code: code, Slug: code.toLowerCase(),
    Name: 'Lane tenant ' + code + ' (Q64b)', DisplayName: 'Lane tenant ' + code + ' (Q64b)', Domain: code.toLowerCase() + '.lane.invalid',
    Status: 1, TenantType: 0, IsDeleted: false}));
  const t = p.tenants.findOne({_id: T});
  res.tenants[tag] = {id, created: true, status: t.Status, tenantType: t.TenantType, isDeleted: t.IsDeleted === true};
}
print(JSON.stringify(res));
"""
r = subprocess.run(["mongosh", "--quiet", f"mongodb://127.0.0.1:{port}/?directConnection=true", "--eval", JS],
                   capture_output=True, text=True, env={"PATH": "/opt/homebrew/bin:/usr/bin:/bin", "HOME": "/tmp",
                                                        "FX": json.dumps({"port": port, "suffix": suffix})})
line = [l for l in r.stdout.splitlines() if l.startswith("{")]
res = json.loads(line[-1]) if line else {"error": "no output", "rc": r.returncode, "stderr": r.stderr[-500:]}
open(out, "w").write(json.dumps(res, indent=2) + "\n")
print(json.dumps(res))
sys.exit(0 if "error" not in res and all("error" not in v for v in res.get("tenants", {}).values()) else 1)
