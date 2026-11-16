using System.Text.Json;
using Diten.InventoryService.Application.Features.OutboundShipments.Services;
using Diten.InventoryService.Domain.Rules;
using Diten.InventoryService.Domain.Warehouse.Enums;
using Diten.InventoryService.Domain.Warehouse.Repositories;
using Diten.InventoryService.Domain.Warehouse.Rules;

namespace Diten.InventoryService.Application.Features.PutawayTasks.Services;

/// <summary>
/// DL-5 (pack §8.18 / §8.19): what a pick task's terminal write does to its outbound, INSIDE the task's transaction. Every pick task terminal and
/// the picked total &gt; 0 → the outbound <c>Allocated → Picked</c> with one <c>PickCompleted</c> row (compare-and-set on the version read in the
/// transaction). Otherwise the outbound document is only touched (<c>PickProgress</c> + 1): two concurrent last confirmations of two tasks then
/// both write it, one transaction conflicts and re-runs on the new snapshot — the outbound becomes <c>Picked</c> exactly once (AC-72).
/// </summary>
public interface IPickTaskCompletion
{
    Task ApplyAsync(IWarehouseTaskWriteSession session, Guid outboundId, DateTimeOffset now, CancellationToken ct);
}

public sealed class PickTaskCompletion : IPickTaskCompletion
{
    private readonly OutboundShipmentReads _reads;

    public PickTaskCompletion(OutboundShipmentReads reads) => _reads = reads;

    public async Task ApplyAsync(IWarehouseTaskWriteSession session, Guid outboundId, DateTimeOffset now, CancellationToken ct)
    {
        var outbound = await session.FindOutboundAsync(outboundId, ct);
        if (outbound is null) return;
        var tasks = await session.PickTasksOfAsync(outboundId, ct);
        if (outbound.Status != OutboundStatus.Allocated || !OutboundFlowRules.IsPicked(tasks))
        {
            await session.TouchOutboundAsync(outboundId, ct);
            return;
        }

        var copy = outbound.Copy();
        copy.Status = OutboundStatus.Picked;
        copy.Version = outbound.Version + 1;
        copy.UpdatedAt = now;
        copy.PickProgress++;
        var picked = OutboundFlowRules.PickedByAllocation(tasks);
        var e = _reads.NewEvent(copy, OutboundEventKind.PickCompleted, now, new Dictionary<string, string?>
        {
            ["picked"] = JsonSerializer.Serialize(copy.AllAllocations().Select(a => new { a.AllocSeq, quantity = InventoryQuantity.ToWire(picked.GetValueOrDefault(a.AllocSeq)) }))
        });
        if (!await session.ReplaceOutboundAsync(copy, outbound.Version, ct))
            throw new InvalidOperationException("The outbound changed inside the pick task's transaction."); // the driver re-runs a conflicting transaction
        await session.InsertOutboundEventsAsync([e], ct);
    }
}
