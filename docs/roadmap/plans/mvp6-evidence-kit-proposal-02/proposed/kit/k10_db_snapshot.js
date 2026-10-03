// CANDIDATE — NOT ACTIVE. MVP6 evidence kit v1.1 phase K10: DB before/after snapshot (mongosh script).
// v1.1 (G5): also records dbTotals — the document count of EVERY collection of the database — so a pair can assert the
// whole-DB delta (outbox, audit, receipts …) of a setup mutation, not only the actor-under-test zero-write pairs.
// This is the A12 VER-02 lane's lane-scripts/db_snapshot.js fingerprint, generalised. Write the output with k10_snap.sh
// (attempt-suffixed file name, never overwritten — G3).
// Generalised from the a08-a09-a12-01 lane's scripts/db-snapshot.js (sha256 3e5f7bed…c9f): same idea — per-collection
// counts for the scoped record plus the few state fields the acceptance row asserts — but database, collections,
// filter and fields are parameters, and it refuses to read from anything but the lane port.
//
// Usage: EK_SNAP='{"label":"a08-before","db":"DitenSupplyChain_Lane01","lanePort":37994,
//          "collections":{"sce_shipments":{"_id":"<id>"},"sce_shipment_history":{"ShipmentId":"<id>"}},
//          "stateFrom":"sce_shipments","stateFilter":{"_id":"<id>"},"fields":["Status","Version","IsDeleted"],
//          "totals":["sce_shipment_outbox"]}' \
//        mongosh --quiet "mongodb://127.0.0.1:<port>/?replicaSet=rs<SFX>" --file k10_db_snapshot.js > raw/<label>.json
// Filters are EJSON, so {"$uuid":"…"} works for UUID-typed keys.
const a = EJSON.parse(process.env.EK_SNAP);
const port = db.adminCommand({ getCmdLineOpts: 1 }).parsed.net.port;
if (port === 27017 || port !== a.lanePort) {
  print(JSON.stringify({ error: 'refused: server port ' + port + ' is not the lane port' }));
  quit(2);
}
const d = db.getSiblingDB(a.db);
const counts = {};
for (const [name, filter] of Object.entries(a.collections || {})) counts[name] = d.getCollection(name).countDocuments(filter);
const totals = {};
for (const name of a.totals || []) totals[name] = d.getCollection(name).countDocuments({});
const dbTotals = {};
for (const name of d.getCollectionNames().sort()) dbTotals[name] = d.getCollection(name).countDocuments({});
let state = null;
if (a.stateFrom) {
  const doc = d.getCollection(a.stateFrom).findOne(a.stateFilter || {});
  if (doc) { state = {}; for (const f of a.fields || []) state[f] = doc[f] === undefined ? null : doc[f]; }
}
print(EJSON.stringify({ capturedAt: new Date().toISOString(), label: a.label, db: a.db, port, counts, totals, dbTotals, state }, { relaxed: true }));
