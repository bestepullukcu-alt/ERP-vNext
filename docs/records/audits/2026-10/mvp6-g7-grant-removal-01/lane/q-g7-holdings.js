// who holds which S&OP / Capacity key, per tenant and role, with grant source (0 System, 1 Module, 2 Manual)
const a = db.getSiblingDB("c13_auth");
const perms = a.permissions.find({ Key: /^supplychain\.(sandop-plans|capacity-plans)\./ }).toArray();
const key = {}; perms.forEach(p => key[String(p._id)] = p.Key.replace("supplychain.", "").replace("-plans", ""));
const roles = a.roles.find({ Name: { $in: ["SuperAdmin", "Admin", "Viewer"] }, IsDeleted: false }).toArray();
roles.sort((x, y) => (String(x.TenantId) + x.Name).localeCompare(String(y.TenantId) + y.Name));
roles.forEach(r => {
  const rp = a.rolePermissions.find({ RoleId: r._id, PermissionId: { $in: perms.map(p => p._id) }, IsDeleted: { $ne: true } }).toArray();
  print("tenant " + String(r.TenantId).slice(-14, -2) + " " + r.Name.padEnd(10) + " " + rp.length + "/8  " + rp.map(x => key[String(x.PermissionId)] + "(" + x.GrantSource + ")").sort().join(" "));
});
