const a = db.getSiblingDB("q353_auth");
print("collections: " + a.getCollectionNames().sort().join(" "));
const pc = a.getCollectionNames().find(n => /^permissions$/i.test(n)) || a.getCollectionNames().find(n => /permission/i.test(n) && !/role/i.test(n));
print("permission collection: " + pc + "  total docs: " + a[pc].countDocuments({}));
const sc = a[pc].find({ Key: /^supplychain\./ }).toArray();
print("supplychain.* permissions: " + sc.length);
sc.forEach(p => printjson({ Key: p.Key, Module: p.Module, Resource: p.Resource, Action: p.Action, Scope: p.Scope, IsSystem: p.IsSystem, IsDeleted: p.IsDeleted, DisplayName: p.DisplayName }));
