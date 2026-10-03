// A12 runtime VER-02 — lane Platform tenant record for tenant 97c5 (absent from the lane Platform seed; Auth login needs
// Platform /api/internal/tenants/{id}/login-settings to resolve). Shape cloned from the seeded tenant document. Lane DB only.
if (db.serverCmdLineOpts().parsed.net.port !== 34994) { throw new Error("refusing: not the lane Mongo port"); }
const p = db.getSiblingDB("DitenPlatform_ShipmentA12RtVer02");
const T = UUID("97c59330-dbc4-4665-b29c-0c26dbb5cc93");
let created = false;
if (!p.tenants.findOne({ _id: T })) {
  const tpl = p.tenants.findOne({});
  p.tenants.insertOne(Object.assign({}, tpl, { _id: T, CreatedBy: "a12-rtver02-fixture", Code: "T97C5", Slug: "t97c5", Name: "Tenant 97c5 (A12 VER-02 lane)",
    DisplayName: "Tenant 97c5 (A12 VER-02 lane)", Domain: "t97c5.lane.invalid", Status: 1, TenantType: 0 }));
  created = true;
}
const t = p.tenants.findOne({ _id: T });
print(JSON.stringify({ tenantCreated: created, status: t.Status, tenantType: t.TenantType, isDeleted: t.IsDeleted }));
