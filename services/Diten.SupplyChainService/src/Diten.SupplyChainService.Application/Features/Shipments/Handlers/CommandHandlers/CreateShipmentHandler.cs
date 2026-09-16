using MediatR;
using System.Text.Json.Nodes;
using Diten.SupplyChainService.Application.Common;
using Diten.SupplyChainService.Application.Features.Shipments.Commands;
using Diten.SupplyChainService.Domain.Features.Shipments;
namespace Diten.SupplyChainService.Application.Features.Shipments.Handlers.CommandHandlers;
public sealed class CreateShipmentHandler(IShipmentRepository repository, RequestContext context) : IRequestHandler<CreateShipmentCommand, Response<JsonObject>>
{
    public async Task<Response<JsonObject>> Handle(CreateShipmentCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var b = request.Body;
        var normalized = b with
        {
            SourceModule = b.SourceModule.Trim(),
            SourceType = b.SourceType.Trim(),
            SourceDocumentId = b.SourceDocumentId.Trim(),
            WarehouseReferenceId = b.WarehouseReferenceId.Trim(),
            ShipToReference = b.ShipToReference.Trim(),
            Lines = b.Lines.Select(x => x with { LineNumber = x.LineNumber.Trim(), UomId = x.UomId.Trim() }).ToArray()
        };
        var result = await repository.MutateAsync(context.Scope, null, "create", context.IdempotencyKey, RequestFingerprint.For(normalized), context.CorrelationId, _ =>
        {
            var now = DateTimeOffset.UtcNow; var id = Guid.NewGuid();
            var shipment = new Shipment
            {
                Id = id,
                TenantId = context.Scope.TenantId,
                LegalEntityId = context.Scope.LegalEntityId,
                CreatedBy = context.Scope.ActorId,
                CreatedAt = now,
                Version = 1,
                ShipmentNumber = $"SHP-{id:N}",
                SourceModule = normalized.SourceModule,
                SourceType = normalized.SourceType,
                SourceDocumentId = normalized.SourceDocumentId,
                WarehouseReferenceId = normalized.WarehouseReferenceId,
                ShipToReference = normalized.ShipToReference,
                PlannedShipAt = b.PlannedShipAt,
                PlannedDeliverAt = b.PlannedDeliverAt,
                Status = ShipmentStatus.Draft,
                CorrelationId = context.CorrelationId,
                Lines = normalized.Lines.Select(x => new ShipmentLine(x.LineNumber, x.ItemId, x.SkuId, x.Quantity, x.UomId, x.InventoryReferenceId)).ToArray()
            };
            return new ShipmentChange(shipment, null, 201, now);
        }, ct);
        return result.ErrorCode is null ? Response<JsonObject>.Success(ShipmentProjection.Mutation(result), result.StatusCode) : Response<JsonObject>.Fail(result.ErrorCode, result.StatusCode);
    }
}
