// MOD-0357 S28: read-only count of duplicate carried agenda lines, run before the unique index is added (pack §24.31).
// Prints numbers only; no id or content leaves the database. Set dbName to that environment's MongoDbSettings:DatabaseName.
// collectionExists=false means a wrong database name, so a duplicateGroups=0 from that run proves nothing.
const dbName = "<PLATFORM_DB>";
const target = db.getSiblingDB(dbName);
const collectionExists = target.getCollectionNames().includes("meeting_agenda_items");
const carried = { CarriedFromMeetingId: { $type: "binData" }, IsDeleted: false };
const groups = target.meeting_agenda_items.aggregate([
  { $match: carried },
  { $group: { _id: { TenantId: "$TenantId", MeetingId: "$MeetingId", RecordLinkId: "$RecordLinkId" }, n: { $sum: 1 } } },
  { $match: { n: { $gt: 1 } } },
  { $count: "duplicateGroups" }
]).toArray();
print("collectionExists=" + collectionExists);
print("carriedLines=" + target.meeting_agenda_items.countDocuments(carried));
print("duplicateGroups=" + (groups.length ? groups[0].duplicateGroups : 0));
