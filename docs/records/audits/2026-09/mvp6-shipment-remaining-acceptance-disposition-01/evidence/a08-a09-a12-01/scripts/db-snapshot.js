const shipmentId=process.env.SHIPMENT_ID;
const dbName='DitenSupplyChain_ShipmentRemaining01';
const d=db.getSiblingDB(dbName);
const names=['sce_shipments','sce_shipment_history','sce_shipment_receipts','sce_shipment_audit','sce_shipment_outbox'];
const doc=d.sce_shipments.findOne({_id:shipmentId});
const counts={};
for(const name of names) counts[name]=d[name].countDocuments(name==='sce_shipments'?{_id:shipmentId}:{ShipmentId:shipmentId});
print(JSON.stringify({capturedAt:new Date().toISOString(),db:dbName,shipmentId,counts,state:doc?{status:doc.Status,version:doc.Version,isDeleted:doc.IsDeleted,legalEntityId:doc.LegalEntityId,lifecycleCorrelationId:doc.LifecycleCorrelationId,pod:doc.Pod}:null}));
