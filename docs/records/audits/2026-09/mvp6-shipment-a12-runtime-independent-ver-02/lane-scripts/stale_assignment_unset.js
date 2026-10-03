// A12 runtime VER-02 — follow-up to scripts/stale_assignment_cleanup.js: remove the DeletedBy field it set (not part of
// the PositionAssignment schema; left in place it could break deserialization). Lane DB only.
if (db.serverCmdLineOpts().parsed.net.port !== 34994) { throw new Error("refusing: not the lane Mongo port"); }
const c = db.getSiblingDB("DitenPlatform_ShipmentA12RtVer02").position_assignments;
const r = c.updateOne({ _id: UUID("ba26c1f5-63ac-4d88-a973-dc70074a0cdd") }, { $unset: { DeletedBy: "" } });
const x = c.findOne({ _id: UUID("ba26c1f5-63ac-4d88-a973-dc70074a0cdd") });
print(JSON.stringify({ modified: r.modifiedCount, isDeleted: x.IsDeleted, hasDeletedBy: "DeletedBy" in x, deletedAtSet: x.DeletedAt != null }));
