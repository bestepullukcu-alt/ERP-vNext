// A12 runtime VER-02 — soft-delete the one stale lane-fixture assignment left by fixture attempt 3 (position A12V02-POS-A-92A799),
// so actor-a resolves to exactly one LE (the product correctly omits legal_entity_id when two are active). Lane DB only.
if (db.serverCmdLineOpts().parsed.net.port !== 34994) { throw new Error("refusing: not the lane Mongo port"); }
const p = db.getSiblingDB("DitenPlatform_ShipmentA12RtVer02");
const r = p.position_assignments.updateOne({ _id: UUID("ba26c1f5-63ac-4d88-a973-dc70074a0cdd"), PositionId: UUID("d78d9330-bc50-46c8-910f-db0dd2f56784"), IsDeleted: false },
  { $set: { IsDeleted: true, DeletedAt: [Long("639260050000000000"), 0], DeletedBy: "a12-rtver02-fixture" } });
print(JSON.stringify({ matched: r.matchedCount, modified: r.modifiedCount }));
