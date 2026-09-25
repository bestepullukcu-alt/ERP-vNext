"""WP-MOB-B03 — grant crm.planned-visit.read/manage/confirm to a tenant-97c5 role (field-rep / saha-rep).

Why a script: the AuthService DataSeeder already grants the 3 keys to the tenant-97c5 *Admin* role on every start
(SeedTenant97c5CrmPlannedVisitGrantAsync). A field-rep role is data, not seed, so it is granted here — by the user.

Safety (memory mongo-guid-subtype-write-recipe / rolepermission-guid-subtype-login-500):
  * every GUID is written as BSON binary subtype-4 (UuidStandard) — subtype-3 breaks ALL logins;
  * date fields are COPIED VERBATIM from a native 97c5 rolePermissions doc (CreatedAt is a [ticks,offset] array,
    AssignedAt a plain BSON DateTime — blanket-setting one format breaks login);
  * the role is NEVER created and permissions are NEVER created (missing → abort with a message);
  * idempotent (existing non-deleted grant → skipped); every inserted row carries CreatedBy/AssignedBy marker
    `manual-grant-planned-visit-rbac` so it can be found and rolled back.

Usage (dry-run by default; nothing is written without --apply):
  py -3 scripts/rbac/grant_planned_visit_rbac_97c5.py --role "<RoleName>"
  py -3 scripts/rbac/grant_planned_visit_rbac_97c5.py --role "<RoleName>" --apply
Rollback:
  db.rolePermissions.deleteMany({CreatedBy: "manual-grant-planned-visit-rbac"})
Users holding the role must log in again (permissions are minted into the JWT).
"""
import argparse
import sys
import uuid

from bson.binary import Binary, UuidRepresentation
from pymongo import MongoClient

MARKER = "manual-grant-planned-visit-rbac"
TENANT_97C5 = uuid.UUID("97c59330-dbc4-4665-b29c-0c26dbb5cc93")
KEYS = ["crm.planned-visit.read", "crm.planned-visit.manage", "crm.planned-visit.confirm"]


def std(g: uuid.UUID) -> Binary:
    return Binary.from_uuid(g, UuidRepresentation.STANDARD)  # subtype 4


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--role", required=True, help="exact tenant-97c5 role name (e.g. the field-rep role)")
    ap.add_argument("--mongo", default="mongodb://localhost:27017")
    ap.add_argument("--db", default="diten_auth_v3")
    ap.add_argument("--apply", action="store_true", help="actually write (default: dry-run)")
    args = ap.parse_args()

    db = MongoClient(args.mongo, uuidRepresentation="standard")[args.db]

    role = db.roles.find_one({"TenantId": TENANT_97C5, "Name": args.role, "IsDeleted": False})
    if role is None:
        names = sorted(r["Name"] for r in db.roles.find({"TenantId": TENANT_97C5, "IsDeleted": False}, {"Name": 1}))
        print(f"ABORT: role '{args.role}' not found in tenant 97c5 (this script never creates roles). Existing: {names}")
        return 2

    perms = list(db.permissions.find({"Key": {"$in": KEYS}, "IsDeleted": False}))
    missing = sorted(set(KEYS) - {p["Key"] for p in perms})
    if missing:
        print(f"ABORT: permission(s) not in catalog: {missing}. Restart AuthService so DataSeeder seeds them.")
        return 3

    # Native template: a real 97c5 grant NOT written by an ad-hoc script, to copy date-field BSON types verbatim.
    template = db.rolePermissions.find_one(
        {"TenantId": TENANT_97C5, "IsDeleted": False, "CreatedBy": {"$not": {"$regex": "^manual-grant"}}})
    if template is None:
        print("ABORT: no native tenant-97c5 rolePermissions doc to copy date-field types from.")
        return 4

    todo = []
    for p in perms:
        if db.rolePermissions.find_one({"TenantId": TENANT_97C5, "RoleId": role["_id"],
                                        "PermissionId": p["_id"], "IsDeleted": False}):
            print(f"skip (already granted): {p['Key']}")
            continue
        # Explicit field list — NOT a full template copy: a template carrying GrantSource=Module/SourceModuleCode
        # would make the entitlement sync treat this manual grant as revocable. Omitted GrantSource = System (0).
        doc = {
            "_id": std(uuid.uuid4()),
            "CreatedAt": template["CreatedAt"],   # [ticks, offset] array — verbatim BSON type
            "CreatedBy": MARKER,
            "UpdatedAt": None,
            "UpdatedBy": None,
            "IsDeleted": False,
            "TenantId": std(TENANT_97C5),
            "RoleId": std(role["_id"]),
            "PermissionId": std(p["_id"]),
            "AssignedAt": template["AssignedAt"],  # plain BSON DateTime — verbatim BSON type
            "AssignedBy": MARKER,
        }
        todo.append((p["Key"], doc))

    print(f"role '{args.role}' ({role['_id']}): {len(todo)} grant(s) to insert: {[k for k, _ in todo]}")
    if not args.apply:
        print("DRY-RUN — nothing written. Re-run with --apply.")
        return 0

    for _, doc in todo:
        db.rolePermissions.insert_one(doc)

    # Verify: every GUID field of every marker row is subtype 4.
    raw = MongoClient(args.mongo)[args.db]  # default codec → raw Binary, so the subtype is visible
    bad = [d["_id"] for d in raw.rolePermissions.find({"CreatedBy": MARKER})
           if any(getattr(d.get(f), "subtype", 4) != 4 for f in ("_id", "TenantId", "RoleId", "PermissionId"))]
    if bad:
        print(f"ERROR: non-subtype-4 GUIDs on {bad} — roll back with the command in the docstring NOW.")
        return 5
    print(f"OK: {len(todo)} inserted, all GUIDs subtype-4. Role holders must log in again.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
