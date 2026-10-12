#!/usr/bin/env python3
"""Q122 lane-DB setup steps (SETUP ONLY; no product API exists for either). Each step records its before/after values.
  cu18-roots  lane Supply Chain DB: shipment NULLROOT -> LifecycleCorrelationId null; shipment BADROOT -> malformed text
              'not-a-uuid-q122' (CU-18 root seam; same method as Q64d D-3).
  cu10-grant  lane Auth DB: add the EXISTING catalog key supplychain.carriers.read to role Q122ClaimsFull@T1 (CU-10 run 2
              only; the pack prerequisite is deferred, CT disposition "PASS with lane grant; prerequisite undocumented").
Never 27017; refuses any port other than the lane port. Output: ids and values only (no secret).
Args: <step> <lane mongo port> <db suffix> <fixture json (read)> <out json (new file)>"""
import json, os, subprocess, sys

STEP, MPORT, SFX, FXJSON, OUT = sys.argv[1:6]
if MPORT == "27017":
    sys.exit("refusing 27017")
if os.path.exists(OUT):
    sys.exit("refusing to overwrite " + OUT)
FX = json.load(open(FXJSON))["fixtures"]
MURI = f"mongodb://127.0.0.1:{MPORT}/?directConnection=true"
PORTGUARD = "const a=JSON.parse(process.env.FX);const port=db.adminCommand({getCmdLineOpts:1}).parsed.net.port;" \
            "if(port===27017||String(port)!==a.port){print(JSON.stringify({error:'not lane port'}));quit(2);}"
JS = {
    "cu18-roots": PORTGUARD + r"""
const d=db.getSiblingDB('DitenSupplyChain_'+a.sfx); const s=d.sce_shipments; const res={purpose:'CU-18 lane-DB setup only (no product API sets a root)'};
const byId=(id)=>({$or:[{_id:UUID(id)},{_id:id}]}); const val=(x)=>x===null||x===undefined?null:x.toString();
for (const [k,id,v] of [['nullRoot',a.nullShip,null],['malformedRoot',a.badShip,'not-a-uuid-q122']]) {
  const b=s.findOne(byId(id)); const r=s.updateOne(byId(id),{$set:{LifecycleCorrelationId:v}}); const f=s.findOne(byId(id));
  res[k]={shipmentId:id,before:val(b.LifecycleCorrelationId),after:val(f.LifecycleCorrelationId),modified:r.modifiedCount}; }
print(JSON.stringify(res));""",
    "cu10-grant": PORTGUARD + r"""
const d=db.getSiblingDB('DitenAuth_'+a.sfx); const T=UUID(a.tenant);
const r=d.roles.findOne({TenantId:T,Name:'Q122ClaimsFull',IsDeleted:false}); const p=d.permissions.findOne({Key:'supplychain.carriers.read',IsDeleted:false});
if(!r||!p){print(JSON.stringify({error:'role or catalog key missing'}));quit(3);}
const before=d.rolePermissions.countDocuments({RoleId:r._id,IsDeleted:false});
const had=d.rolePermissions.findOne({RoleId:r._id,PermissionId:p._id,IsDeleted:false});
if(!had){ const tpl=d.rolePermissions.findOne({RoleId:r._id});
  d.rolePermissions.insertOne(Object.assign({},tpl,{_id:UUID(),TenantId:T,RoleId:r._id,PermissionId:p._id,AssignedAt:new Date(),AssignedBy:'q122-lane-setup',CreatedBy:'q122-lane-setup'})); }
print(JSON.stringify({purpose:'CU-10 run 2 only (P2 deferred); lane Auth DB',role:'Q122ClaimsFull@T1',grantAdded:'supplychain.carriers.read',alreadyPresent:!!had,
  grantsBefore:before,grantsAfter:d.rolePermissions.countDocuments({RoleId:r._id,IsDeleted:false})}));""",
}
if STEP not in JS:
    sys.exit("unknown step")
args = {"port": MPORT, "sfx": SFX, "nullShip": FX["shipments"]["NULLROOT"]["shipmentId"], "badShip": FX["shipments"]["BADROOT"]["shipmentId"],
        "tenant": "97c59330-dbc4-4665-b29c-0c26dbb5cc93"}
r = subprocess.run(["mongosh", "--quiet", MURI, "--eval", JS[STEP]], capture_output=True, text=True,
                   env={"PATH": "/opt/homebrew/bin:/usr/bin:/bin", "HOME": "/tmp", "FX": json.dumps(args)})
lines = [l for l in r.stdout.splitlines() if l.startswith("{")]
if r.returncode != 0 or not lines:
    sys.exit(f"mongosh failed rc={r.returncode} {r.stderr[-300:]}")
res = json.loads(lines[-1])
open(OUT, "w").write(json.dumps(res, indent=2) + "\n"); print(json.dumps(res))
sys.exit(1 if "error" in res else 0)
