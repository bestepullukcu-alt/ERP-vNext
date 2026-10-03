const p = db.getSiblingDB("q353_platform");
const names = p.getCollectionNames().sort();
print("platform collections matching page|action|module: " + names.filter(n => /page|action|module/i.test(n)).join(" "));
for (const n of names.filter(n => /page|action|module/i.test(n))) {
  const docs = p[n].find({ $or: [ { ModuleCode: /SHIPMENT/i }, { moduleCode: /SHIPMENT/i }, { RoutePath: /SupplyChain/i }, { PermissionKey: /^supplychain/ }, { RequiredPermission: /^supplychain/ } ] }).toArray();
  if (docs.length) { print("== " + n + " : " + docs.length); docs.forEach(d => printjson({ ModuleCode: d.ModuleCode, PageCode: d.PageCode, ActionCode: d.ActionCode, RoutePath: d.RoutePath, RequiredPermission: d.RequiredPermission, PermissionKey: d.PermissionKey, IsDeleted: d.IsDeleted })); }
}
