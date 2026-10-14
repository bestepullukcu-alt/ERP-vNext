"""WP-VW-W4-CFG — grant the W4 keys (doctor insights, report v2 aggregate, MDM competitor products + the MDM Workflow
call) to tenant-97c5 roles.

Keys (W4-BE-c / W4-BE-a / W4-BE-b2, Auth catalog, tenant scope):
  crm.doctor-insights.read | manage | read-all | read-aggregate | thresholds.manage
  crm.visit-report.read-aggregate
  mdm.competitor-products.read | manage | propose
  mdm.gskus.read                                  (pharmacy stock / sales rows + sample GSKU list, W4-WEB-b)
  platform.workflow.instances.start | view, platform.workflow.tasks.approve | reject | cancel
                                                  (MDM starts MDM-COMP-PROPOSAL; the approver decides in the Work Center)

crm.doctor-insights.read-all is on the Auth ExplicitGrantOnly list (decision 2026-10-13: no automatic path gives it,
Admins must still see every doctor) → this script is the ONLY way it reaches Admin. Do not drop it.

Grants:
  * the admin role (default "Admin")              → every key above that it does not hold yet;
  * --rep-role NAME (optional; live: CrmUser)     → mdm.competitor-products.read | propose, crm.doctor-insights.read,
                                                    mdm.gskus.read;
  * --marketing-role NAME (optional)              → crm.doctor-insights.read-aggregate | thresholds.manage,
                                                    crm.visit-report.read-aggregate, mdm.competitor-products.read | manage.
  A role named on the command line that does not exist → ABORT (this script never creates roles). Local 97c5 has no rep
  or marketing role today (2026-10-13: Admin, DocumentMasterRegisterLinker, GQD, QADocumentation, Viewer) — run it for
  Admin only and decide the rep / marketing roles first.

Safety — identical to grant_w3_authoring_rbac_97c5.py (memory mongo-guid-subtype-write-recipe /
rolepermission-guid-subtype-login-500):
  * every GUID is written as BSON binary subtype-4 (UuidStandard) — subtype-3 breaks ALL logins;
  * date fields are COPIED VERBATIM from a native 97c5 rolePermissions doc (CreatedAt is a [ticks,offset] array,
    AssignedAt a plain BSON DateTime — blanket-setting one format breaks login);
  * roles and permissions are NEVER created (missing → abort with a message);
  * idempotent (an existing non-deleted grant is skipped); every inserted row carries CreatedBy/AssignedBy marker
    `manual-grant-w4-report-rbac` so it can be found and rolled back.
  * local tenant 97c5 ONLY — the live tenant (be823006) was configured on 2026-10-13 by scripts/golive/*; never run there.

Usage (dry-run by default; nothing is written without --apply):
  py -3 scripts/rbac/grant_w4_report_rbac_97c5.py                                   # Admin, dry-run
  py -3 scripts/rbac/grant_w4_report_rbac_97c5.py --apply
  py -3 scripts/rbac/grant_w4_report_rbac_97c5.py --rep-role "CrmUser" --marketing-role "Marketing" --apply
Rollback:
  db.rolePermissions.deleteMany({CreatedBy: "manual-grant-w4-report-rbac"})
Users holding the role must log in again (permissions are minted into the JWT).
"""
import argparse
import sys
import uuid

from bson.binary import Binary, UuidRepresentation
from pymongo import MongoClient

MARKER = "manual-grant-w4-report-rbac"
TENANT_97C5 = uuid.UUID("97c59330-dbc4-4665-b29c-0c26dbb5cc93")
ALL_KEYS = [
    "crm.doctor-insights.read",
    "crm.doctor-insights.manage",
    "crm.doctor-insights.read-all",          # ExplicitGrantOnly — only this script gives it to Admin
    "crm.doctor-insights.read-aggregate",
    "crm.doctor-insights.thresholds.manage",
    "crm.visit-report.read-aggregate",
    "mdm.competitor-products.read",
    "mdm.competitor-products.manage",
    "mdm.competitor-products.propose",
    "mdm.gskus.read",
    "platform.workflow.instances.start",
    "platform.workflow.instances.view",
    "platform.workflow.tasks.approve",
    "platform.workflow.tasks.reject",
    "platform.workflow.tasks.cancel",
]
REP_KEYS = [
    "mdm.competitor-products.read",
    "mdm.competitor-products.propose",
    "crm.doctor-insights.read",
    "mdm.gskus.read",
]
MARKETING_KEYS = [
    "crm.doctor-insights.read-aggregate",
    "crm.doctor-insights.thresholds.manage",
    "crm.visit-report.read-aggregate",
    "mdm.competitor-products.read",
    "mdm.competitor-products.manage",
]


def std(g: uuid.UUID) -> Binary:
    return Binary.from_uuid(g, UuidRepresentation.STANDARD)  # subtype 4


def find_role(db, name):
    return db.roles.find_one({"TenantId": TENANT_97C5, "Name": name, "IsDeleted": False})


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--admin-role", default="Admin", help="tenant-97c5 role that gets every key (default: Admin)")
    ap.add_argument("--rep-role", default=None, help="tenant-97c5 field-rep role (live: CrmUser; default: none)")
    ap.add_argument("--marketing-role", default=None, help="tenant-97c5 marketing role (default: none)")
    ap.add_argument("--mongo", default="mongodb://localhost:27017")
    ap.add_argument("--db", default="diten_auth_v3")
    ap.add_argument("--apply", action="store_true", help="actually write (default: dry-run)")
    args = ap.parse_args()

    db = MongoClient(args.mongo, uuidRepresentation="standard")[args.db]

    plan = [(args.admin_role, ALL_KEYS)]
    if args.rep_role and args.rep_role != args.admin_role:
        plan.append((args.rep_role, REP_KEYS))
    if args.marketing_role and args.marketing_role != args.admin_role:
        plan.append((args.marketing_role, MARKETING_KEYS))

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
        print(f"ABORT: permission(s) not in catalog: {missing}. Restart AuthService so DataSeeder seeds them (W4-BE-c / BE-a / BE-b2).")
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
    if not args.rep_role or not args.marketing_role:
        print("NOTE: no --rep-role / --marketing-role given — only the admin role is planned "
              "(local 97c5 has no rep / marketing role; decide them before granting).")
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
