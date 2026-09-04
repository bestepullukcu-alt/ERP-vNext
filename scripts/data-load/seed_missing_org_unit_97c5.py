"""
DEV seed — create the missing tenant-97c5 organization unit `55a2dfcb-5db7-4960-be61-154d056c8e44`.

WHY: The tenant-97c5 CEO position (c109d881...) — and every seeded role position (medical-representative,
area-manager, regional-manager, ...) — points to OrganizationUnitId 55a2dfcb..., but no organization_units
document with that id exists. The Tasks server (MOD-0024) resolves a task's organization unit from the
assignee's Position; with the org unit missing it returns `ORGANIZATION_UNIT_UNRESOLVED` (400) and no task
can be created — even a self-assigned one. Creating this one org unit makes the whole seeded position
hierarchy resolvable.

The existing PositionAssignment (CEO c109d881 -> user c5769c62 = bestepullukcu@gmail.com @ 97c5) is already
correct, so nothing else is needed.

GUID-safe: pymongo with uuidRepresentation="standard" writes binary subtype-4 (Standard), matching every
other Platform doc — do NOT use another representation (subtype-3 GUIDs break Platform login).

Run:  py scripts/data-load/seed_missing_org_unit_97c5.py
Idempotent: skips if the org unit already exists.
"""
import time
import uuid

from pymongo import MongoClient

DB = "diten_personalization_dev"
TENANT = uuid.UUID("97c59330-dbc4-4665-b29c-0c26dbb5cc93")
ORG_UNIT = uuid.UUID("55a2dfcb-5db7-4960-be61-154d056c8e44")   # the id every seeded role position expects
CEO_POSITION = uuid.UUID("c109d881-1cd5-4503-b108-3df2d37bfd53")
LEGAL_ENTITY = uuid.UUID("e4eb36f8-fde8-47fc-bca7-bc1aa0be6f23")  # same legal entity the working org unit uses


def main():
    cli = MongoClient("mongodb://localhost:27017", uuidRepresentation="standard", serverSelectionTimeoutMS=3000)
    units = cli[DB]["organization_units"]

    if units.find_one({"_id": ORG_UNIT}):
        print("Org unit 55a2dfcb already exists — nothing to do.")
        return

    # .NET DateTimeOffset is stored as [ticks, offsetMinutes]; ticks = (unix_seconds + 62135596800) * 1e7.
    now_ticks = int((time.time() + 62135596800) * 10_000_000)
    doc = {
        "_id": ORG_UNIT,
        "TenantId": TENANT,
        "Code": "HQ",
        "Name": "Genel Merkez",
        "Description": None,
        "OrgUnitType": 0,
        "Status": 0,
        "ParentOrganizationUnitId": None,
        "ManagerPositionId": CEO_POSITION,
        "LegalEntityId": LEGAL_ENTITY,
        "CostCenterCode": None,
        "LocationCode": None,
        "EffectiveFrom": None,
        "EffectiveTo": None,
        "IsArchived": False,
        "IsDeleted": False,
        "DeletedAt": None,
        "CreatedAt": [now_ticks, 0],
        "CreatedBy": "system-dev-seed",
        "UpdatedAt": None,
        "UpdatedBy": None,
        "Version": 1,
    }
    units.insert_one(doc)
    back = units.find_one({"_id": ORG_UNIT})
    print(f"Inserted org unit 55a2dfcb — Code={back['Code']} Name={back['Name']} Tenant={back['TenantId']}")
    print("Now the tenant-97c5 CEO/role positions resolve an org unit; task creation should succeed.")
    print("If Platform caches org units, restart the Platform service to be safe.")


if __name__ == "__main__":
    main()
