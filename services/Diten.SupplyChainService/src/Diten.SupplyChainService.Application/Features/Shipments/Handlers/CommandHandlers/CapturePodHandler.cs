using MediatR;
using System.Text.Json.Nodes;
using Diten.SupplyChainService.Application.Common;
using Diten.SupplyChainService.Application.Features.Shipments.Commands;
using Diten.SupplyChainService.Domain.Features.Shipments;
namespace Diten.SupplyChainService.Application.Features.Shipments.Handlers.CommandHandlers;
public sealed class CapturePodHandler(IShipmentRepository repository, RequestContext context) : IRequestHandler<CapturePodCommand, Response<JsonObject>>
{
    public async Task<Response<JsonObject>> Handle(CapturePodCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var b = request.Body with { RecipientName = request.Body.RecipientName.Trim() };
        var normalized = request with { Body = b };
        var result = await repository.MutateAsync(context.Scope, request.ShipmentId, "pod", context.IdempotencyKey, RequestFingerprint.For(normalized), context.CorrelationId, s =>
        {
            if (s is null) return ShipmentChange.Fail("SHIPMENT_NOT_FOUND", 404);
            if (s.CorrelationId != context.CorrelationId) return ShipmentChange.Fail("INVALID_REQUEST", 400);
            if (s.Pod is not null) return ShipmentChange.Fail("POD_ALREADY_CAPTURED", 409);
            if (s.Status is not (ShipmentStatus.Dispatched or ShipmentStatus.InTransit) || s.DispatchedAt is null || b.ReceivedAt < s.DispatchedAt)
                return ShipmentChange.Fail("INVALID_SHIPMENT_TRANSITION", 422);
            s.Pod = new ProofOfDelivery(Guid.NewGuid(), b.ReceivedAt, b.RecipientName, b.EvidenceReferenceIds, b.Note, context.CorrelationId);
            s.Status = ShipmentStatus.Delivered; s.ActualDeliverAt = b.ReceivedAt; s.UpdatedBy = context.Scope.ActorId; s.Version++;
            return new ShipmentChange(s, null, 201, b.ReceivedAt, null, b.Note);
        }, ct);
        return result.ErrorCode is null ? Response<JsonObject>.Success(ShipmentProjection.Mutation(result, true), result.StatusCode) : Response<JsonObject>.Fail(result.ErrorCode, result.StatusCode);
    }
}
