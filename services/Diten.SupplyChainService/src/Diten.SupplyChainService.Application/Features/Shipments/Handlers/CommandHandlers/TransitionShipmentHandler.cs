using MediatR;
using System.Text.Json.Nodes;
using Diten.SupplyChainService.Application.Common;
using Diten.SupplyChainService.Application.Features.Shipments.Commands;
using Diten.SupplyChainService.Application.Features.Shipments.Dispatch;
using Diten.SupplyChainService.Domain.Features.Shipments;
namespace Diten.SupplyChainService.Application.Features.Shipments.Handlers.CommandHandlers;
public sealed class TransitionShipmentHandler(IShipmentRepository repository, RequestContext context, ShipmentDispatchCoordinator dispatch) : IRequestHandler<TransitionShipmentCommand, Response<JsonObject>>
{
    public async Task<Response<JsonObject>> Handle(TransitionShipmentCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var target = Enum.Parse<ShipmentStatus>(request.Body.TargetStatus);
        var permission = target == ShipmentStatus.Cancelled ? "supplychain.shipments.cancel" : "supplychain.shipments.dispatch";
        if (!context.Permissions.Contains(permission)) return Response<JsonObject>.Fail("INVALID_REQUEST", 403);
        // MOD-0183 §23 (DL-1): Dispatched issues the stock in INVENTORY first; Cancelled out of a failed compensation (the
        // exit) first resends its reversals. Both go to the coordinator; every other transition stays one transaction.
        if (target == ShipmentStatus.Dispatched) return await dispatch.TransitionAsync(request, target, ct);
        if (target == ShipmentStatus.Cancelled && (await repository.GetAsync(context.Scope, request.ShipmentId, ct))?.DispatchAttempt?.State == DispatchState.CompensationFailed)
            return await dispatch.TransitionAsync(request, target, ct);
        var result = await repository.MutateAsync(context.Scope, request.ShipmentId, "transition", context.IdempotencyKey, RequestFingerprint.For(request), context.CorrelationId,
            s => Decide(s, target, request, context), ct);
        return result.ErrorCode is null ? Response<JsonObject>.Success(ShipmentProjection.Mutation(result), result.StatusCode) : Response<JsonObject>.Fail(result.ErrorCode, result.StatusCode);
    }

    /// <summary>Every transition but Dispatched. §23.4: one active attempt — Cancelled during InProgress is refused, and a
    /// failed compensation blocks every transition except the exit.</summary>
    public static ShipmentChange Decide(Shipment? s, ShipmentStatus target, TransitionShipmentCommand request, RequestContext context)
    {
        if (s is null) return ShipmentChange.Fail("SHIPMENT_NOT_FOUND", 404);
        if (s.CorrelationId != context.CorrelationId) return ShipmentChange.Fail("INVALID_REQUEST", 400);
        if (s.DispatchAttempt?.State == DispatchState.CompensationFailed) return ShipmentChange.Fail(ContractErrorCodes.DispatchCompensationFailed, 409);
        if (s.DispatchAttempt?.State == DispatchState.InProgress && target == ShipmentStatus.Cancelled) return ShipmentChange.Fail(ContractErrorCodes.DispatchInProgress, 409);
        if (!ShipmentLifecycle.Allows(s.Status, target)) return ShipmentChange.Fail("INVALID_SHIPMENT_TRANSITION", 422);
        s.Status = target; s.UpdatedBy = context.Scope.ActorId; s.Version++;
        if (target == ShipmentStatus.Delivered) s.ActualDeliverAt = request.Body.OccurredAt;
        return new ShipmentChange(s, null, 200, request.Body.OccurredAt, request.Body.ReasonCode, request.Body.Note);
    }
}
