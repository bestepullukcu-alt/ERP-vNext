#!/usr/bin/env python3
"""Q122 read-only lane-DB assertion for one or more claims (CU-VS1 root/Pending outbox, CU-12 exact empty strings,
CU-10 carrierId, CU-20/CU-22 status). Reads the lane Supply Chain DB only; never writes.
Output: ids, statuses, stored texts and correlation roots only (no secret).
Args: <label> <lane mongo port> <db suffix> <out json (new file)> <claimId>[,<claimId>...]"""
import json, os, subprocess, sys

LABEL, MPORT, SFX, OUT, IDS = sys.argv[1:6]
if MPORT == "27017":
    sys.exit("refusing 27017")
if os.path.exists(OUT):
    sys.exit("refusing to overwrite " + OUT)
JS = r"""
const a=JSON.parse(process.env.FX); const port=db.adminCommand({getCmdLineOpts:1}).parsed.net.port;
if(port===27017||String(port)!==a.port){print(JSON.stringify({error:'not lane port'}));quit(2);}
const d=db.getSiblingDB('DitenSupplyChain_'+a.sfx); const out={label:a.label,claims:{}};
const s=(x)=>x===null||x===undefined?null:(typeof x==='object'&&x.toString?x.toString():x);
for (const id of a.ids) {
  const c=d.claims.findOne({_id:id});
  const rec=d.claims_receipts.find({ClaimId:id}).toArray(); const au=d.claims_audit.find({ClaimId:id}).toArray(); const ob=d.claims_outbox.find({ClaimId:id}).toArray();
  out.claims[id]={ found:!!c, status:c?s(c.Status):null, claimNumber:c?s(c.ClaimNumber):null, claimedAmount:c?s(c.ClaimedAmount):null, approvedAmount:c?s(c.ApprovedAmount):null,
    currency:c?s(c.Currency):null, reasonCode:c?s(c.ReasonCode):null, evidenceReferenceIds:c&&c.EvidenceReferenceIds?c.EvidenceReferenceIds.map(s):null,
    carrierId:c?s(c.CarrierId):null, correlationRoot:c?s(c.CorrelationRoot):null, isDeleted:c?c.IsDeleted===true:null, version:c?s(c.Version):null,
    receipts:rec.map(r=>({statusCode:s(r.StatusCode),resultingStatus:s(r.ResultingStatus),correlationRoot:s(r.CorrelationRoot)})),
    audit:au.map(r=>({version:s(r.Version),correlationRoot:s(r.CorrelationRoot),resolutionCode:s(r.ResolutionCode),note:s(r.Note)})),
    outbox:ob.map(r=>({status:s(r.Status),eventType:r.Envelope?s(r.Envelope.eventType):null,correlationId:r.Envelope?s(r.Envelope.correlationId):null})) };
}
print(JSON.stringify(out));
"""
r = subprocess.run(["mongosh", "--quiet", f"mongodb://127.0.0.1:{MPORT}/?directConnection=true", "--eval", JS], capture_output=True, text=True,
                   env={"PATH": "/opt/homebrew/bin:/usr/bin:/bin", "HOME": "/tmp", "FX": json.dumps({"port": MPORT, "sfx": SFX, "label": LABEL, "ids": IDS.split(",")})})
lines = [l for l in r.stdout.splitlines() if l.startswith("{")]
if r.returncode != 0 or not lines:
    sys.exit(f"mongosh failed rc={r.returncode} {r.stderr[-300:]}")
res = json.loads(lines[-1])
open(OUT, "w").write(json.dumps(res, indent=2) + "\n"); print(json.dumps(res)[:2000])
