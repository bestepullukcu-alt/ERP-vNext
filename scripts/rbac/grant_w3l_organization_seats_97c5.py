"""WP-VW-W3L W3-BE-d (CFG) — grant platform.organization-seats.read to tenant-97c5 roles.

Key (PLT-ORG-SEAT, Platform manifest, tenant-assignable — K-L8):
  platform.organization-seats.read   read ANOTHER user's current primary seat (position → organization unit → legal entity)

Why: the CRM resolves a rep's legal entity evidence layer from the rep's Platform primary seat. A rep reading their OWN
seat (the visit start) needs no key; the evidence settings page (the scope list "applies to N reps", the rep roster, a
rep's effective rules) reads OTHER users' seats with the signed-in user's token, so its readers need this key. Without it
the legal entity layer is skipped with "platform-unavailable" (K-L5) — nothing breaks, the layer below applies.

Grants:
  * the admin role (default "Admin")              → platform.organization-seats.read;
  * --manager-role NAME (optional)                → the same key (a manager who opens the evidence settings page).
  A role named on the command line that does not exist → ABORT (this script never creates roles).

Safety — identical to grant_w3e_evidence_rbac_97c5.py (memory mongo-guid-subtype-write-recipe /
rolepermission-guid-subtype-login-500):
  * every GUID is written as BSON binary subtype-4 (UuidStandard) — subtype-3 breaks ALL logins;
  * date fields are COPIED VERBATIM from a native 97c5 rolePermissions doc (CreatedAt is a [ticks,offset] array,
    AssignedAt a plain BSON DateTime — blanket-setting one format breaks login);
  * roles and permissions are NEVER created (missing → abort with a message: the key exists only after PLT-ORG-SEAT is
    on the branch and AuthService has re-seeded the catalog);
  * idempotent (an existing non-deleted grant is skipped); every inserted row carries CreatedBy/AssignedBy marker
    `manual-grant-w3l-organization-seats` so it can be found and rolled back;
  * local tenant 97c5 ONLY — never run against the live tenant (be823006); live grants are a go-live checklist item
    (execution/domains/commercial-suite/work-packs/NOTE-golive-checklist.md A6).

Usage (dry-run by default; nothing is written without --apply):
  py -3 scripts/rbac/grant_w3l_organization_seats_97c5.py                      # Admin, dry-run
  py -3 scripts/rbac/grant_w3l_organization_seats_97c5.py --apply
  py -3 scripts/rbac/grant_w3l_organization_seats_97c5.py --manager-role "Team Leader" --apply
Rollback:
  db.rolePermissions.deleteMany({CreatedBy: "manual-grant-w3l-organization-seats"})
Users holding the role must log in again (permissions are minted into the JWT).
"""
import argparse
import sys
import uuid

from bson.binary import Binary, UuidRepresentation
from pymongo import MongoClient

MARKER = "manual-grant-w3l-organization-seats"
TENANT_97C5 = uuid.UUID("97c59330-dbc4-4665-b29c-0c26dbb5cc93")
ALL_KEYS = [
    "platform.organization-seats.read",
]
MANAGER_KEYS = [
    "platform.organization-seats.read",
]


def std(g: uuid.UUID) -> Binary:
    return Binary.from_uuid(g, UuidRepresentation.STANDARD)  # subtype 4


def find_role(db, name):
    return db.roles.find_one({"TenantId": TENANT_97C5, "Name": name, "IsDeleted": False})


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--admin-role", default="Admin", help="tenant-97c5 role that gets the key (default: Admin)")
    ap.add_argument("--manager-role", default=None, help="tenant-97c5 manager role (default: none)")
    ap.add_argument("--mongo", default="mongodb://localhost:27017")
    ap.add_argument("--db", default="diten_auth_v3")
    ap.add_argument("--apply", action="store_true", help="actually write (default: dry-run)")
    args = ap.parse_args()

    db = MongoClient(args.mongo, uuidRepresentation="standard")[args.db]

    plan = [(args.admin_role, ALL_KEYS)]
    if args.manager_role and args.manager_role != args.admin_role:
        plan.append((args.manager_role, MANAGER_KEYS))

    roles = {}
    for name, _ in plan:
        role = find_role(db, name)
        if role is None:
            names = sorted(r["Name"] for r in db.roles.find({"TenantId": TENANT_97C5, "IsDeleted": False}, {"Name": 1}))
            print(f"ABORT: role '{name}' not found in tenant 97c5 (this script never creates roles). Existing: {names}")
            return 2
        roles[name] = role

    perms = {p["Key"]: p for p in db.permissions.find({"Key": {"$in": ALL_KEYS}, "IsDeleted": False})}
    missing = sorted(set(ALL_KEYS) - set(perms))
    if missing:
        print(f"ABORT: permission(s) not in catalog: {missing}. PLT-ORG-SEAT must be on the branch and AuthService "
              "restarted so DataSeeder seeds the Platform manifest key, then re-run.")
        return 3

    # Native template: a real 97c5 grant NOT written by an ad-hoc script, to copy date-field BSON types verbatim.
    template = db.rolePermissions.find_one(
        {"TenantId": TENANT_97C5, "IsDeleted": False, "CreatedBy": {"$not": {"$regex": "^manual-grant"}}})
    if template is None:
        print("ABORT: no native tenant-97c5 rolePermissions doc to copy date-field types from.")
        return 4

    todo = []
    for name, keys in plan:
        role = roles[name]
        for key in keys:
            p = perms[key]
            if db.rolePermissions.find_one({"TenantId": TENANT_97C5, "RoleId": role["_id"],
                                            "PermissionId": p["_id"], "IsDeleted": False}):
                print(f"skip (already granted): {name} <- {key}")
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
            todo.append((name, key, doc))

    for name, _ in plan:
        planned = [k for n, k, _ in todo if n == name]
        print(f"role '{name}' ({roles[name]['_id']}): {len(planned)} grant(s) to insert: {planned}")
    if not args.manager_role:
        print("NOTE: no --manager-role given — only the admin role is planned.")
    if not args.apply:
        print("DRY-RUN — nothing written. Re-run with --apply.")
        return 0

    for _, _, doc in todo:
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
