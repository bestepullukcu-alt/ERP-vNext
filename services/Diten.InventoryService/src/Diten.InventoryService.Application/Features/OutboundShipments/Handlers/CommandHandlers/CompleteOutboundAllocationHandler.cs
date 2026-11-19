using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.OutboundShipments.Commands;
using Diten.InventoryService.Application.Features.OutboundShipments.Services;
using Diten.InventoryService.Application.Features.Warehouse;
using Diten.InventoryService.Application.Features.Warehouse.Services;
using Diten.InventoryService.Domain.Warehouse.Enums;
using Diten.InventoryService.Domain.Warehouse.Repositories;
using MediatR;

namespace Diten.InventoryService.Application.Features.OutboundShipments.Handlers.CommandHandlers;

/// <summary>
/// The maintenance service's internal command (pack §8.17 step 6; AC-71 (i)(ii)(vi)(vii)). Only an <c>Allocating</c> outbound whose recovery lock
/// has passed is touched; the FU03 batch is the PERSISTED plan's, with the same keys: all lines already processed → FU03's replay → transaction
/// B; none → created now → transaction B; refused → <c>AllocationFailed</c> (the command fails: central row abandoned); FU03 400 → the
/// <c>KeyReused</c> branch; unknown → nothing written, the next tick retries. Nothing to do → success without a write (no central row).
/// </summary>
public sealed class CompleteOutboundAllocationHandler : IRequestHandler<CompleteOutboundAllocationCommand, Response<bool>>
{
    private readonly IOutboundShipmentRepository _outbounds;
    private readonly OutboundAllocationService _allocation;
    private readonly WarehouseSupport _support;

    public CompleteOutboundAllocationHandler(IOutboundShipmentRepository outbounds, OutboundAllocationService allocation, WarehouseSupport support)
    {
        _outbounds = outbounds;
        _allocation = allocation;
        _support = support;
    }

    public async Task<Response<bool>> Handle(CompleteOutboundAllocationCommand request, CancellationToken ct)
    {
        var outbound = await _outbounds.FindAsync(request.OutboundId, ct);
        if (outbound is null || outbound.Status != OutboundStatus.Allocating) return Response<bool>.Success(false);
        if (outbound.AllocationLeaseUntilUtc is { } lease && lease > _support.Now()) return Response<bool>.Success(false);

        var answer = await _allocation.ReserveAndCompleteAsync(outbound, 200, ct);
        return answer.IsSuccessful
            ? Response<bool>.Success(true)
            : Response<bool>.Fail(answer.ErrorCode ?? WarehouseErrorCodes.AllocationFailed, answer.Errors.Count > 0 ? answer.Errors[0] : "The allocation was not completed.",
                answer.StatusCode, answer.ErrorDetails);
    }
}
