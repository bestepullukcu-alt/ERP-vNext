"""WP-VW-W6 W6-CFG — grant the pharmacy pre-order (MOD-0168) and pricing (MOD-0156) keys to tenant-97c5 roles.

The matrix is scripts/rbac/w6_cfg_role_matrix.json (K-W6-ROLES, 2026-10-21; the live script
scripts/golive/w6-cfg-grant-roles-live.js carries the same one — a test fails when they differ):
  CrmUser             commercial.pre-orders.read / create / submit / cancel + crm.account.read, crm.contact.read,
                      crm.account-contact.read, crm.visit-plan.read (CRM-ROLE-1) + mdm.product-master.read (product picker)
  Team Leader         commercial.pre-orders.read / approve / cancel (the approving line manager)
  Department Manager  the approver keys + sales operations (read-all, record-depot-response, retry-transmission,
                      pre-order-settings.*) + price-lists.*, pricing-rules.*, fx-rates.* (four-eyes publishing included)
  Admin               commercial.*
The commercial.* keys are read from the Platform CrmManifestProvider source (never a hand list): a '.*' row is every manifest
key under it; a manifest key no role gets is printed under "NOT GRANTED" (never skipped silently).
The matrix's "workflow_needs" (platform.workflow.instances.start / .view, tasks.approve / .reject per role) is REPORT ONLY:
the CRM starts and decides the approval rounds with the caller's token, so a role without them cannot submit / decide —
but they are MOD-0023's keys and this script never grants them (decision K13).

Local 97c5 has only some of these roles (measured 2026-10-21: Admin, DocumentMasterRegisterLinker, GQD, QADocumentation,
Viewer). A matrix role that is not there is reported and skipped; --role-map "CrmUser=Some Local Role" grants a matrix row to an
existing local role instead. --strict-roles turns a missing role into an abort.

Safety — identical to grant_w5_visit_notifications_97c5.py (memory mongo-guid-subtype-write-recipe):
  * every GUID is written as BSON binary subtype-4 (UuidStandard) — subtype-3 breaks ALL logins;
  * date fields are COPIED VERBATIM from a native 97c5 rolePermissions doc;
  * roles and permissions are NEVER created (a key missing from the catalog → ABORT: the commercial.* keys exist only after
    W6-BE-a / BE-b / WEB-a are on the branch and AuthService has re-seeded the catalog from the CRM manifest — start the
    fleet once first);
  * idempotent — a key the role already holds is not inserted again; every inserted row carries CreatedBy/AssignedBy
    marker `manual-grant-w6-cfg`;
  * the Auth default role template (DefaultRolePermissionTemplate) is not touched (follow-up CRM-ROLE-TEMPLATES);
  * local tenant 97c5 ONLY — the live tenant (be823006) is scripts/golive/w6-cfg-grant-roles-live.js.

Usage (dry-run by default; nothing is written without --apply):
  py -3 scripts/rbac/grant_w6_cfg_rbac_97c5.py
  py -3 scripts/rbac/grant_w6_cfg_rbac_97c5.py --role-map "CrmUser=Viewer" --apply
Rollback:
  db.rolePermissions.deleteMany({CreatedBy: "manual-grant-w6-cfg"})
Users holding the role must log in again (permissions are minted into the JWT).
"""
from __future__ import annotations

import argparse
import json
import os
import re
import sys
import uuid

SCRIPTS = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
REPO = os.path.dirname(SCRIPTS)
MATRIX_PATH = os.path.join(SCRIPTS, "rbac", "w6_cfg_role_matrix.json")
MANIFEST_PATH = os.path.join(
    REPO, "services", "Diten.Platform", "src", "Diten.Platform.Application", "Features", "Crm", "SelfRegistration",
    "CrmManifestProvider.cs")

TENANT_97C5 = uuid.UUID("97c59330-dbc4-4665-b29c-0c26dbb5cc93")
NAMESPACE = "commercial."

# A page / action / permission row of the manifest that carries a commercial.* key. The event-code constants further down the
# file (commercial.preorder.approvalrequested …) are not permission keys and are not matched.
_MANIFEST_ROW = re.compile(r'new\s+ModuleManifest(?:Page|Action|Permission)\s*\(.*?"(commercial\.[a-z0-9.\-]+)"')


def load_matrix(path: str = MATRIX_PATH) -> dict:
    with open(path, encoding="utf-8") as f:
        return json.load(f)


def manifest_commercial_keys(path: str = MANIFEST_PATH) -> list:
    """Every commercial.* permission key the Platform CrmManifestProvider declares, sorted."""
    keys = set()
    with open(path, encoding="utf-8") as f:
        for line in f:
            m = _MANIFEST_ROW.search(line)
            if m:
                keys.add(m.group(1))
    return sorted(keys)


def resolve(matrix: dict, universe: list) -> tuple:
    """(plan, errors): plan = [(matrix role, [keys])] with every '.*' row expanded over the manifest's commercial keys.

    Errors: a commercial key the manifest does not declare (a typo would grant nothing), a '.*' row that matches nothing."""
    plan, errors = [], []
    known = set(universe)
    for row in matrix["roles"]:
        keys = []
        for entry in row["keys"]:
            if entry.endswith(".*"):
                prefix = entry[:-1]
                hit = [k for k in universe if k.startswith(prefix)]
                if not hit:
                    errors.append(f"{row['role']}: '{entry}' matches no manifest key")
                keys.extend(hit)
            elif entry.startswith(NAMESPACE) and entry not in known:
                errors.append(f"{row['role']}: '{entry}' is not a manifest key")
            else:
                keys.append(entry)
        seen = set()
        plan.append((row["role"], [k for k in keys if not (k in seen or seen.add(k))]))
    return plan, errors


def not_granted(plan: list, universe: list) -> list:
    """Manifest commercial keys that no matrix role gets."""
    granted = {k for _, keys in plan for k in keys}
    return [k for k in universe if k not in granted]


def admin_only(plan: list, universe: list) -> list:
    """Manifest commercial keys that only the Admin row gets (e.g. the export key)."""
    others = {k for role, keys in plan if role != "Admin" for k in keys}
    return [k for k in universe if k not in others]


class MongoStore:
    """The few reads / one write the script needs, on diten_auth_v3 (tenant 97c5)."""

    def __init__(self, uri: str, db_name: str):
        from pymongo import MongoClient  # lazy: the unit tests run without pymongo
        self._uri, self._db_name = uri, db_name
        self._db = MongoClient(uri, uuidRepresentation="standard")[db_name]

    @staticmethod
    def std(g: uuid.UUID):
        from bson.binary import Binary, UuidRepresentation
        return Binary.from_uuid(g, UuidRepresentation.STANDARD)  # subtype 4

    def roles(self) -> list:
        return list(self._db.roles.find({"TenantId": TENANT_97C5, "IsDeleted": False}, {"Name": 1}))

    def permissions(self, keys: list) -> dict:
        return {p["Key"]: p for p in self._db.permissions.find({"Key": {"$in": keys}, "IsDeleted": False})}

    def held_permission_ids(self, role_id) -> set:
        return {g["PermissionId"] for g in self._db.rolePermissions.find(
            {"TenantId": TENANT_97C5, "RoleId": role_id, "IsDeleted": False}, {"PermissionId": 1})}

    def native_template(self):
        # A real 97c5 grant NOT written by an ad-hoc script, to copy the date-field BSON types verbatim.
        return self._db.rolePermissions.find_one(
            {"TenantId": TENANT_97C5, "IsDeleted": False, "CreatedBy": {"$not": {"$regex": "^manual-grant"}}})

    def new_grant(self, role_id, permission_id, template: dict, marker: str) -> dict:
        # Explicit field list — NOT a full template copy: a template carrying GrantSource=Module/SourceModuleCode would make
        # the entitlement sync treat this manual grant as revocable. Omitted GrantSource = System (0).
        return {
            "_id": self.std(uuid.uuid4()),
            "CreatedAt": template["CreatedAt"],   # [ticks, offset] array — verbatim BSON type
            "CreatedBy": marker,
            "UpdatedAt": None,
            "UpdatedBy": None,
            "IsDeleted": False,
            "TenantId": self.std(TENANT_97C5),
            "RoleId": self.std(role_id),
            "PermissionId": self.std(permission_id),
            "AssignedAt": template["AssignedAt"],  # plain BSON DateTime — verbatim BSON type
            "AssignedBy": marker,
        }

    def insert(self, doc: dict) -> None:
        self._db.rolePermissions.insert_one(doc)

    def non_subtype4(self, marker: str) -> list:
        from pymongo import MongoClient
        raw = MongoClient(self._uri)[self._db_name]  # default codec → raw Binary, so the subtype is visible
        return [d["_id"] for d in raw.rolePermissions.find({"CreatedBy": marker})
                if any(getattr(d.get(f), "subtype", 4) != 4 for f in ("_id", "TenantId", "RoleId", "PermissionId"))]


def run(store, matrix: dict, universe: list, role_map: dict, apply: bool, strict_roles: bool = False, out=print) -> int:
    marker = matrix["marker"]
    plan, errors = resolve(matrix, universe)
    if errors:
        for e in errors:
            out(f"ABORT: matrix ↔ manifest: {e}")
        return 6

    out(f"commercial.* keys in the manifest: {len(universe)}")
    missing_everywhere = not_granted(plan, universe)
    out(f"NOT GRANTED (manifest key no role gets): {missing_everywhere or 'none'}")
    out(f"Admin only: {admin_only(plan, universe) or 'none'}")

    local_roles = {r["Name"].strip(): r for r in store.roles()}
    present, absent = [], []
    for matrix_role, keys in plan:
        local = role_map.get(matrix_role, matrix_role)
        if local in local_roles:
            present.append((matrix_role, local, local_roles[local], keys))
        else:
            absent.append((matrix_role, local))
    for matrix_role, local in absent:
        out(f"role '{local}' (matrix row {matrix_role}) is not in tenant 97c5 — SKIPPED (use --role-map to map it). "
            f"Existing: {sorted(local_roles)}")
    if absent and strict_roles:
        out("ABORT: --strict-roles and a matrix role is missing (this script never creates roles).")
        return 2

    needed = sorted({k for _, _, _, keys in present for k in keys})
    workflow_needs = matrix.get("workflow_needs", {})
    perms = store.permissions(needed + sorted({k for keys in workflow_needs.values() for k in keys}))
    missing = [k for k in needed if k not in perms]
    if missing:
        out(f"ABORT: permission(s) not in the catalog: {missing}. W6-BE-a / BE-b / WEB-a must be on the branch and the fleet "
            "started once (the CRM manifest registers the keys), then re-run.")
        return 3

    template = store.native_template()
    if template is None:
        out("ABORT: no native tenant-97c5 rolePermissions doc to copy date-field types from.")
        return 4

    todo, pairs = [], set()  # pairs: two matrix rows mapped onto one local role never insert a key twice
    for matrix_role, local, role, keys in present:
        held = store.held_permission_ids(role["_id"])
        planned = []
        for key in keys:
            if perms[key]["_id"] in held:
                out(f"skip (already granted): {local} <- {key}")
                continue
            if (role["_id"], perms[key]["_id"]) in pairs:
                continue
            pairs.add((role["_id"], perms[key]["_id"]))
            planned.append(key)
            todo.append(store.new_grant(role["_id"], perms[key]["_id"], template, marker))
        out(f"role '{local}' ({role['_id']}): {len(planned)} grant(s) to insert: {planned}")
        lacks = [k for k in workflow_needs.get(matrix_role, []) if k not in perms or perms[k]["_id"] not in held]
        if lacks:
            out(f"  INFO (not granted here — MOD-0023 keys, decision K13): '{local}' lacks {lacks}; the CRM starts / decides "
                "the approval round with the caller's token, so without them a submit answers 503 workflow_unavailable and a decision 503 / 403.")

    if not apply:
        out(f"DRY-RUN — nothing written ({len(todo)} grant(s) planned). Re-run with --apply.")
        return 0

    for doc in todo:
        store.insert(doc)
    bad = store.non_subtype4(marker)
    if bad:
        out(f"ERROR: non-subtype-4 GUIDs on {bad} — roll back with the command in the docstring NOW.")
        return 5
    out(f"OK: {len(todo)} inserted, all GUIDs subtype-4. Role holders must log in again.")
    return 0


def parse_role_map(pairs: list) -> dict:
    result = {}
    for pair in pairs or []:
        if "=" not in pair:
            raise SystemExit(f"--role-map needs MATRIX=LOCAL, got '{pair}'")
        k, v = pair.split("=", 1)
        result[k.strip()] = v.strip()
    return result


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--role-map", action="append", default=[], metavar="MATRIX=LOCAL",
                    help="grant a matrix row to an existing local role (repeatable), e.g. \"CrmUser=Viewer\"")
    ap.add_argument("--strict-roles", action="store_true", help="abort when a matrix role is missing (default: skip + report)")
    ap.add_argument("--mongo", default="mongodb://localhost:27017")
    ap.add_argument("--db", default="diten_auth_v3")
    ap.add_argument("--apply", action="store_true", help="actually write (default: dry-run)")
    args = ap.parse_args()
    return run(MongoStore(args.mongo, args.db), load_matrix(), manifest_commercial_keys(), parse_role_map(args.role_map),
               apply=args.apply, strict_roles=args.strict_roles)


if __name__ == "__main__":
    sys.exit(main())
