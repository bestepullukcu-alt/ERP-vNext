"""WP-VW-W3 live E4 — dev-only: switch tenant 97c5's visit evidence policy location rule.

There is no settings screen yet (W3-WEB-d) and the CRM API needs a user token, so the live test writes the tenant's
VisitEvidencePolicy document directly, with exactly the fields the CRM class map knows (unknown fields break reads).

Usage (from the repo root):
    py scripts/dev/w3-evidence-policy-location.py            # show the current state (read only)
    py scripts/dev/w3-evidence-policy-location.py --info     # location "info": a visit starts without a location fix (flagged)
    py scripts/dev/w3-evidence-policy-location.py --revert   # delete the document this script wrote → defaults again
                                                             # (location "required", photo optional, 300 m / 150 m)
No restart needed: the policy is read on every start.
"""
import datetime
import sys
import uuid

from pymongo import MongoClient

MONGO = "mongodb://localhost:27017"
DB = "DitenERP_Dev"
COLLECTION = "visit_evidence_policies"
TENANT_ID = "97c59330-dbc4-4665-b29c-0c26dbb5cc93"
MARKER = "ct-e4-w3-temp"


def main() -> int:
    col = MongoClient(MONGO, serverSelectionTimeoutMS=5000)[DB][COLLECTION]
    current = col.find_one({"TenantId": TENANT_ID, "IsDeleted": False})

    if "--revert" in sys.argv:
        result = col.delete_many({"TenantId": TENANT_ID, "UpdatedBy": MARKER})
        print(f"reverted: {result.deleted_count} document(s) deleted → tenant defaults (location required).")
        return 0

    if "--info" in sys.argv:
        if current is not None:
            print(f"STOP: a policy document already exists (UpdatedBy={current.get('UpdatedBy')}, "
                  f"LocationMode={current.get('LocationMode')}). Nothing written.")
            return 1
        now = datetime.datetime.now(datetime.timezone.utc)
        ticks = int((now - datetime.datetime(1, 1, 1, tzinfo=datetime.timezone.utc)).total_seconds() * 10_000_000)
        doc = {
            "_id": str(uuid.uuid4()), "TenantId": TENANT_ID, "IsDeleted": False, "DeletedAt": None,
            "CreatedAt": [ticks, 0], "UpdatedAt": None, "Version": 0,
            "LocationMode": "info", "RadiusMeters": 300, "MaxAccuracyMeters": 150, "PhotoMode": "optional",
            "DoctorConfirmation": True, "DurationConsistency": True, "ShortVisitSeconds": 120,
            "AccountWithoutLocation": "allow_flagged", "UpdatedBy": MARKER,
        }
        col.insert_one(doc)
        print(f"written: LocationMode=info (document {doc['_id']}). Revert with --revert after the test.")
        return 0

    if current is None:
        print("no policy document → tenant defaults apply (location required, photo optional, 300 m / 150 m).")
    else:
        print({k: current.get(k) for k in ("LocationMode", "RadiusMeters", "MaxAccuracyMeters", "PhotoMode",
                                            "DoctorConfirmation", "ShortVisitSeconds", "AccountWithoutLocation",
                                            "UpdatedBy")})
    return 0


if __name__ == "__main__":
    sys.exit(main())
