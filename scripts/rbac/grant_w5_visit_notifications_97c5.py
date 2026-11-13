"""WP-VW-W5 (W5-BE + W5-WEB) — grant the visit notification keys to tenant-97c5 roles.

Keys (CRM manifest page CRM_VISIT_NOTIFICATIONS + action SETTINGS_MANAGE, tenant-assignable — K-N9):
  crm.visit-notifications.read              the "Sent visit e-mails" list + preview (rep: own visits; manager: team)
  crm.visit-notifications.settings.manage   the notification settings tab (PV / MSL mailboxes, switches) — sees the tenant

Grants:
  * the admin role (default "Admin")   → both keys;
  * --manager-role NAME (optional)     → crm.visit-notifications.read (a manager sees the team's e-mails);
  * --rep-role NAME (optional)         → crm.visit-notifications.read (a rep sees the e-mails of their own visits).
  A role named on the command line that does not exist → ABORT (this script never creates roles).

Safety — identical to grant_w3l_organization_seats_97c5.py (memory mongo-guid-subtype-write-recipe /
rolepermission-guid-subtype-login-500):
  * every GUID is written as BSON binary subtype-4 (UuidStandard) — subtype-3 breaks ALL logins;
  * date fields are COPIED VERBATIM from a native 97c5 rolePermissions doc;
  * roles and permissions are NEVER created (missing → abort: the keys exist only after W5-WEB is on the branch and
    AuthService has re-seeded the catalog from the CRM manifest — start the fleet once first);
  * idempotent; every inserted row carries CreatedBy/AssignedBy marker `manual-grant-w5-visit-notifications`;
  * local tenant 97c5 ONLY — never run against the live tenant (be823006); live grants are a go-live checklist item.

Usage (dry-run by default; nothing is written without --apply):
  py -3 scripts/rbac/grant_w5_visit_notifications_97c5.py                      # Admin, dry-run
  py -3 scripts/rbac/grant_w5_visit_notifications_97c5.py --apply
  py -3 scripts/rbac/grant_w5_visit_notifications_97c5.py --manager-role "Team Leader" --rep-role "Medical Representative" --apply
Rollback:
  db.rolePermissions.deleteMany({CreatedBy: "manual-grant-w5-visit-notifications"})
Users holding the role must log in again (permissions are minted into the JWT).
"""
import argparse
import sys
import uuid

from bson.binary import Binary, UuidRepresentation
from pymongo import MongoClient

MARKER = "manual-grant-w5-visit-notifications"
TENANT_97C5 = uuid.UUID("97c59330-dbc4-4665-b29c-0c26dbb5cc93")
ALL_KEYS = [
    "crm.visit-notifications.read",
    "crm.visit-notifications.settings.manage",
]
READ_KEYS = [
    "crm.visit-notifications.read",
]


def std(g: uuid.UUID) -> Binary:
    return Binary.from_uuid(g, UuidRepresentation.STANDARD)  # subtype 4


def find_role(db, name):
    return db.roles.find_one({"TenantId": TENANT_97C5, "Name": name, "IsDeleted": False})


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--admin-role", default="Admin", help="tenant-97c5 role that gets the key (default: Admin)")
    ap.add_argument("--manager-role", default=None, help="tenant-97c5 manager role (default: none)")
    ap.add_argument("--rep-role", default=None, help="tenant-97c5 representative role (default: none)")
    ap.add_argument("--mongo", default="mongodb://localhost:27017")
    ap.add_argument("--db", default="diten_auth_v3")
    ap.add_argument("--apply", action="store_true", help="actually write (default: dry-run)")
    args = ap.parse_args()

    db = MongoClient(args.mongo, uuidRepresentation="standard")[args.db]

    plan = [(args.admin_role, ALL_KEYS)]
    for extra in (args.manager_role, args.rep_role):
        if extra and extra not in [n for n, _ in plan]:
            plan.append((extra, READ_KEYS))

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
        print(f"ABORT: permission(s) not in catalog: {missing}. W5-WEB must be on the branch and the fleet started once "
              "(the CRM manifest registers the page / action keys), then re-run.")
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
    if not args.manager_role and not args.rep_role:
        print("NOTE: no --manager-role / --rep-role given — only the admin role is planned.")
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
