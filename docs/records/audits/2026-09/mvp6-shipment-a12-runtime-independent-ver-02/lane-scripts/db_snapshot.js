// A12 runtime VER-02 DB snapshot — adapted from evidence-kit candidate k10_db_snapshot.js (itself from template
// a08-a09-a12-01/scripts/db-snapshot.js). Env SNAP = {"label":L,"shipmentId":ID|null}. Refuses anything but lane port 34994.
// Captures: per-shipment counts in the 5 lifecycle collections, asserted state fields, and a WHOLE-DATABASE fingerprint
// (every collection's document count) of the SupplyChain lane DB, so a zero-write assertion covers the entire DB.
const a = JSON.parse(process.env.SNAP);
const port = db.adminCommand({ getCmdLineOpts: 1 }).parsed.net.port;
if (port !== 34994) { print(JSON.stringify({ error: "refused: port " + port })); quit(2); }
const d = db.getSiblingDB("DitenSupplyChain_ShipmentA12RtVer02");
const names = ["sce_shipments", "sce_shipment_history", "sce_shipment_receipts", "sce_shipment_audit", "sce_shipment_outbox"];
const counts = {};
if (a.shipmentId) for (const n of names) counts[n] = d.getCollection(n).countDocuments(n === "sce_shipments" ? { _id: a.shipmentId } : { ShipmentId: a.shipmentId });
const dbTotals = {};
for (const n of d.getCollectionNames().sort()) dbTotals[n] = d.getCollection(n).countDocuments({});
let state = null;
if (a.shipmentId) {
  const s = d.sce_shipments.findOne({ _id: a.shipmentId });
  if (s) state = { status: s.Status, version: s.Version, isDeleted: s.IsDeleted, legalEntityId: s.LegalEntityId, lifecycleCorrelationId: s.LifecycleCorrelationId,
    pod: s.Pod ? { recipientName: s.Pod.RecipientName, evidenceReferenceIds: s.Pod.EvidenceReferenceIds } : null, updatedAt: s.UpdatedAt };
}
print(EJSON.stringify({ capturedAt: new Date().toISOString(), label: a.label, db: d.getName(), port, shipmentId: a.shipmentId, counts, dbTotals, state }, { relaxed: true }));
