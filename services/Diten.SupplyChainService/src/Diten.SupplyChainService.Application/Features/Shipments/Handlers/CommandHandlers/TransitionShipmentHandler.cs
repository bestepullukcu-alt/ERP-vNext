using MediatR;
using System.Text.Json.Nodes;
using Diten.SupplyChainService.Application.Common;
using Diten.SupplyChainService.Application.Features.Shipments.Commands;
using Diten.SupplyChainService.Domain.Features.Shipments;
namespace Diten.SupplyChainService.Application.Features.Shipments.Handlers.CommandHandlers;
public sealed class TransitionShipmentHandler(IShipmentRepository repository, RequestContext context) : IRequestHandler<TransitionShipmentCommand, Response<JsonObject>>
{
    public async Task<Response<JsonObject>> Handle(TransitionShipmentCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var target = Enum.Parse<ShipmentStatus>(request.Body.TargetStatus);
        var permission = target == ShipmentStatus.Cancelled ? "supplychain.shipments.cancel" : "supplychain.shipments.dispatch";
        if (!context.Permissions.Contains(permission)) return Response<JsonObject>.Fail("INVALID_REQUEST", 403);
        var result = await repository.MutateAsync(context.Scope, request.ShipmentId, "transition", context.IdempotencyKey, RequestFingerprint.For(request), context.CorrelationId, s =>
        {
            if (s is null) return ShipmentChange.Fail("SHIPMENT_NOT_FOUND", 404);
            if (s.CorrelationId != context.CorrelationId) return ShipmentChange.Fail("INVALID_REQUEST", 400);
            if (!ShipmentLifecycle.Allows(s.Status, target)) return ShipmentChange.Fail("INVALID_SHIPMENT_TRANSITION", 422);
            s.Status = target; s.UpdatedBy = context.Scope.ActorId; s.Version++;
            if (target == ShipmentStatus.Dispatched) s.DispatchedAt = request.Body.OccurredAt;
            if (target == ShipmentStatus.Delivered) s.ActualDeliverAt = request.Body.OccurredAt;
            return new ShipmentChange(s, null, 200, request.Body.OccurredAt, request.Body.ReasonCode, request.Body.Note);
        }, ct);
        return result.ErrorCode is null ? Response<JsonObject>.Success(ShipmentProjection.Mutation(result), result.StatusCode) : Response<JsonObject>.Fail(result.ErrorCode, result.StatusCode);
    }
}
