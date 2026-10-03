// who holds which supplychain.* key, per tenant and role, with grant source
const a = db.getSiblingDB("q358_auth");
const perms = a.permissions.find({ Key: /^supplychain\./ }).toArray();
print("auth permissions total=" + a.permissions.countDocuments({}) + " supplychain.*=" + perms.length + " [" + perms.map(p => p.Key).sort().join(", ") + "]");
const key = {}; perms.forEach(p => key[String(p._id)] = p.Key);
const roles = a.roles.find({ Name: { $in: ["SuperAdmin", "Admin", "Viewer"] } }).toArray();
roles.sort((x, y) => (String(x.TenantId) + x.Name).localeCompare(String(y.TenantId) + y.Name));
roles.forEach(r => {
  const rp = a.rolePermissions.find({ RoleId: r._id, PermissionId: { $in: perms.map(p => p._id) }, IsDeleted: { $ne: true } }).toArray();
  const items = rp.map(x => key[String(x.PermissionId)].replace("supplychain.shipments.", "") + "(" + x.GrantSource + (x.SourceModuleCode ? ":" + x.SourceModuleCode : "") + ")").sort();
  print("tenant " + String(r.TenantId).slice(-12) + " " + r.Name.padEnd(10) + " holds " + rp.length + "/5  " + items.join(" "));
});
