using System.Text.Json;
using System.Text.Json.Nodes;
using Diten.SupplyChainService.Domain.Features.Shipments;
namespace Diten.SupplyChainService.Application.Features.Shipments;
public static class ShipmentProjection
{
    public static JsonObject Summary(Shipment s) => JsonSerializer.SerializeToNode(new
    {
        shipmentId = s.Id,
        s.ShipmentNumber,
        s.SourceDocumentId,
        status = s.Status.ToString(),
        s.CarrierId,
        loadId = s.LoadPlanId,
        s.PlannedShipAt,
        s.PlannedDeliverAt,
        s.ActualDeliverAt
    }, Options)!.AsObject();
    public static JsonObject Detail(Shipment s)
    {
        var data = Summary(s);
        data["sourceModule"] = s.SourceModule; data["sourceType"] = s.SourceType;
        data["warehouseReferenceId"] = s.WarehouseReferenceId; data["shipToReference"] = s.ShipToReference;
        // 3.4.0: the line stock fields, and each line's issue of the completed dispatch attempt (null until Dispatched).
        var issued = s.DispatchAttempt?.State == DispatchState.Completed ? s.DispatchAttempt.Lines : [];
        data["lines"] = JsonSerializer.SerializeToNode(s.Lines.Select(l => new
        {
            l.LineNumber, l.ItemId, l.SkuId, l.Quantity, l.UomId, l.InventoryReferenceId, l.SkuLevel, l.LotId, l.FromLocationId, l.FefoBypassReason,
            issueTransactionId = issued.FirstOrDefault(x => x.LineNumber == l.LineNumber)?.IssueTransactionId
        }), Options);
        data["pod"] = s.Pod is null ? null : JsonSerializer.SerializeToNode(new { s.Pod.RecipientName, s.Pod.ReceivedAt, s.Pod.EvidenceReferenceIds, s.Pod.Note }, Options);
        data["stockIssued"] = s.StockIssued(); data["dispatchState"] = s.DispatchStateWire();
        data["contractVersion"] = "v1"; return data;
    }
    public static JsonObject Detail(ShipmentDetailReadResult result)
    {
        var data = Detail(result.Shipment);
        data["lifecycleCorrelationId"] = result.RootState == RawRootState.InvalidStoredValue
            ? null
            : result.LifecycleCorrelationId?.ToString();
        return data;
    }
    public static JsonObject Mutation(ShipmentMutationResult r, bool pod = false) => pod
     ? JsonSerializer.SerializeToNode(new { podId = r.Shipment!.Pod!.Id, shipmentId = r.Shipment.Id, shipmentStatus = r.Shipment.Status.ToString(), r.Shipment.Pod.ReceivedAt, idempotentReplay = r.Replay, contractVersion = "v1" }, Options)!.AsObject()
     : JsonSerializer.SerializeToNode(new { shipmentId = r.Shipment!.Id, r.Shipment.ShipmentNumber, status = r.Shipment.Status.ToString(), idempotentReplay = r.Replay, contractVersion = "v1" }, Options)!.AsObject();
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}
