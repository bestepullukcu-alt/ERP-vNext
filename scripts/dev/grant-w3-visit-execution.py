"""WP-VW-W3 — dev-only manual grant of the W3 visit execution keys to the tenant 97c5 Admin role.

Why: the keys exist in the Auth catalog (Tenant scope, after W3-AUTH) but the module-entitlement sync did not hand
them to the existing 97c5 Admin role (same as the MOD-0155 visit-report keys, marker manual-grant-mod0155).

Usage (from the repo root):
    py scripts/dev/grant-w3-visit-execution.py            # dry run: shows what would be written
    py scripts/dev/grant-w3-visit-execution.py --apply    # writes, then reads back and verifies
    py scripts/dev/grant-w3-visit-execution.py --revert   # deletes exactly the rows this script wrote (by marker)

Safety:
- Clones a NATIVE rolePermissions row of the same role, so every field keeps its BSON type (CreatedAt array,
  AssignedAt datetime, GUIDs binary subtype 4) — a wrong type breaks every login.
- Idempotent: a key the role already holds is skipped.
- Marker AssignedBy = CreatedBy = "manual-grant-w3-visit-execution" (revert deletes only those).
- After writing, users must log in again (permissions ride the JWT minted at login).
"""
import base64
import datetime
import sys
import uuid

from bson.binary import Binary
from pymongo import MongoClient

MONGO = "mongodb://localhost:27017"
DB = "diten_auth_v3"
TENANT_ID = uuid.UUID("97c59330-dbc4-4665-b29c-0c26dbb5cc93")
# the 97c5 Admin role id as stored (binary subtype 4, base64) — see memory workingcalendar-rbac-grant
ADMIN_ROLE_ID = uuid.UUID(bytes=base64.b64decode("ajFUZ32ASti9dnj492L+ig=="))
MARKER = "manual-grant-w3-visit-execution"
TEMPLATE_KEY = "crm.visit-report.read"

KEYS = [
    # W3-BE-a — visit execution + tenant evidence policy
    "crm.visit-execution.read",
    "crm.visit-execution.record",
    "crm.visit-evidence-policy.manage",
    # W3-BE-b — objection response library
    "crm.objection-response.read",
    "crm.objection-response.manage",
    "crm.objection-response.approve",
    # W3-PLT / W3-AUTH — scope-bound document repository keys (Tenant scope)
    "platform.document-repository.visit-evidence.write",
    "platform.document-repository.visit-evidence.read",
    "platform.document-repository.visit-evidence.read-all",
]


def guid(u: uuid.UUID) -> Binary:
    return Binary(u.bytes, 4)  # UuidStandard (subtype 4)


def main() -> int:
    mode = "apply" if "--apply" in sys.argv else "revert" if "--revert" in sys.argv else "dry"
    db = MongoClient(MONGO, serverSelectionTimeoutMS=5000)[DB]
    role_id = guid(ADMIN_ROLE_ID)

    role = db.roles.find_one({"_id": role_id})
    if role is None:
        print("STOP: the 97c5 Admin role was not found.")
        return 1
    print(f"role: {role.get('Name')}  ({ADMIN_ROLE_ID})")

    if mode == "revert":
        result = db.rolePermissions.delete_many({"AssignedBy": MARKER, "CreatedBy": MARKER})
        print(f"reverted: {result.deleted_count} row(s) deleted (marker {MARKER}). Log in again.")
        return 0

    template_perm = db.permissions.find_one({"Key": TEMPLATE_KEY})
    template = db.rolePermissions.find_one({"RoleId": role_id, "PermissionId": template_perm["_id"], "IsDeleted": False})
    if template is None:
        print(f"STOP: no native row for {TEMPLATE_KEY} on this role to clone.")
        return 1

    held = {bytes(r["PermissionId"]) for r in db.rolePermissions.find({"RoleId": role_id, "IsDeleted": False})}
    to_write = []
    for key in KEYS:
        perm = db.permissions.find_one({"Key": key, "IsDeleted": False})
        if perm is None:
            print(f"  MISSING in catalog: {key}  (restart Platform, then Auth)")
            continue
        if perm.get("Scope") != 0:
            print(f"  NOT TENANT scope ({perm.get('Scope')}): {key}  — restart Auth after Platform; not granting")
            continue
        if bytes(perm["_id"]) in held:
            print(f"  already held: {key}")
            continue
        row = dict(template)
        row["_id"] = guid(uuid.uuid4())
        row["PermissionId"] = perm["_id"]
        row["CreatedBy"] = MARKER
        row["AssignedBy"] = MARKER
        row["UpdatedAt"] = None
        row["UpdatedBy"] = None
        # keep each date field's native BSON type: CreatedAt = [ticks, offset] array, AssignedAt = datetime
        now = datetime.datetime.now(datetime.timezone.utc)
        ticks = int((now - datetime.datetime(1, 1, 1, tzinfo=datetime.timezone.utc)).total_seconds() * 10_000_000)
        row["CreatedAt"] = [ticks, 0] if isinstance(template.get("CreatedAt"), list) else template.get("CreatedAt")
        row["AssignedAt"] = now.replace(tzinfo=None)
        to_write.append((key, row))
        print(f"  will grant: {key}")

    if mode == "dry":
        print(f"\nDRY RUN — {len(to_write)} row(s) would be written. Re-run with --apply.")
        return 0

    for _, row in to_write:
        db.rolePermissions.insert_one(row)

    # read back and verify every GUID is subtype 4 and the date types match the template
    bad = 0
    for raw in db.rolePermissions.find({"AssignedBy": MARKER}):
        for field in ("_id", "TenantId", "RoleId", "PermissionId"):
            if not isinstance(raw.get(field), Binary) or raw[field].subtype != 4:
                bad += 1
                print(f"  BAD GUID {field} on {raw['_id']}")
        if type(raw.get("AssignedAt")).__name__ != "datetime" or not isinstance(raw.get("CreatedAt"), list):
            bad += 1
            print(f"  BAD DATE TYPE on {raw['_id']}")
    total = db.rolePermissions.count_documents({"AssignedBy": MARKER})
    print(f"\nwritten: {len(to_write)} · marker rows now: {total} · problems: {bad}")
    if bad:
        print("PROBLEM — run with --revert NOW, then report.")
        return 2
    print("OK. Log out and log in again.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
