// A12 runtime VER-02 — lane Auth fixture (lane DB on 34994 only). Hashes arrive via env (HASH_A/B/LEB/ADM), never printed.
// 1) rotate the PasswordHash of the four named seeded tenant-97c5 users to bcrypt(12) of lane-generated passwords;
// 2) tenant-97c5 roles: A12VerShipmentOperator (5 shipment keys) -> actor-a/b/le-b; A12VerFixtureAdmin (MDM/Platform org keys) -> fixture-admin.
if (db.getMongo().getURI().indexOf(":34994") < 0 || db.serverCmdLineOpts().parsed.net.port !== 34994) { throw new Error("refusing: not the lane Mongo port"); }
const a = db.getSiblingDB("DitenAuth_ShipmentA12RtVer02");
const T = UUID("97c59330-dbc4-4665-b29c-0c26dbb5cc93");
const users = { A: "john.doe.t97@diten.com", B: "jane.smith.t97@diten.com", LEB: "bob.johnson.t97@diten.com", ADM: "alice.williams.t97@diten.com" };
const control = a.users.findOne({ Email: "charlie.brown.t97@diten.com", TenantId: T });
const out = { users: {}, roles: {} };
for (const [k, email] of Object.entries(users)) {
  const u = a.users.find({ Email: email, TenantId: T, IsDeleted: false }).toArray();
  if (u.length !== 1) throw new Error("expected exactly one user for " + k);
  const x = u[0];
  const pre = { isActive: x.IsActive === true, emailConfirmed: x.EmailConfirmed === true, mustChange: x.MustChangePassword === true, lockedOut: x.LockoutEnd != null };
  if (!pre.isActive || !pre.emailConfirmed || pre.mustChange || pre.lockedOut) throw new Error("precondition failed for " + k);
  const h = process.env["HASH_" + k];
  if (!h || !h.startsWith("$2a$12$")) throw new Error("hash missing for " + k);
  const prevShared = control && x.PasswordHash === control.PasswordHash;
  const r = a.users.updateOne({ _id: x._id }, { $set: { PasswordHash: h, FailedLoginAttempts: 0 } });
  out.users[k] = { email, userId: x._id.toString().replace(/^UUID\("|"\)$/g, ""), preconditions: pre, previousHashWasSharedSeedHash: prevShared, rotated: r.modifiedCount === 1 };
}
const roleTpl = a.roles.findOne({ IsDeleted: false });
const rpTpl = a.rolePermissions.findOne({});
const urTpl = a.userRoles.findOne({});
function role(name, keys, members) {
  let rd = a.roles.findOne({ TenantId: T, Name: name });
  const reused = !!rd;
  if (!rd) {
    rd = Object.assign({}, roleTpl, { _id: UUID(), TenantId: T, Name: name, DisplayName: name, Description: "A12 runtime VER-02 lane fixture", IsSystem: false, CreatedBy: "a12-rtver02-fixture", UpdatedAt: null, UpdatedBy: null });
    a.roles.insertOne(rd);
  }
  const perms = a.permissions.find({ Key: { $in: keys }, IsDeleted: false }).toArray();
  if (perms.length !== keys.length) throw new Error("permission catalog missing keys for " + name + ": " + perms.length + "/" + keys.length);
  let added = 0;
  for (const p of perms) {
    if (a.rolePermissions.findOne({ TenantId: T, RoleId: rd._id, PermissionId: p._id, IsDeleted: false })) continue;
    a.rolePermissions.insertOne(Object.assign({}, rpTpl, { _id: UUID(), TenantId: T, RoleId: rd._id, PermissionId: p._id, AssignedAt: new Date(), AssignedBy: "a12-rtver02-fixture", CreatedBy: "a12-rtver02-fixture", GrantSource: rpTpl.GrantSource, SourceModuleCode: null }));
    added++;
  }
  for (const m of members) {
    const u = a.users.findOne({ Email: users[m], TenantId: T });
    if (a.userRoles.findOne({ TenantId: T, UserId: u._id, RoleId: rd._id, IsDeleted: false })) continue;
    a.userRoles.insertOne(Object.assign({}, urTpl, { _id: UUID(), TenantId: T, UserId: u._id, RoleId: rd._id, AssignedAt: new Date(), AssignedBy: "a12-rtver02-fixture", CreatedBy: "a12-rtver02-fixture" }));
  }
  out.roles[name] = { roleId: rd._id.toString().replace(/^UUID\("|"\)$/g, ""), reused, permissionKeys: keys, grantsAdded: added, members };
}
role("A12VerShipmentOperator", ["supplychain.shipments.read", "supplychain.shipments.create", "supplychain.shipments.dispatch", "supplychain.shipments.cancel", "supplychain.shipments.pod.capture"], ["A", "B", "LEB"]);
role("A12VerFixtureAdmin", ["mdm.legal-entities.create", "mdm.legal-entities.read", "mdm.legal-entities.update", "platform.organization-units.create", "platform.organization-units.read", "platform.positions.create", "platform.positions.read", "platform.position-assignments.create", "platform.position-assignments.read", "auth.users.lookup-validation"], ["ADM"]);
print(JSON.stringify(out));
