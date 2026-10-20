using MediatR;

namespace Diten.SafetyStockService.Application;

public enum ManualQuantityOperation { Create, Submit, Approve }
public enum ManualQuantityStatus { Created, Updated, Found, NotFound, Conflict, Invalid, Unavailable }

public sealed record ManualQuantityReceipt(Guid Id, Guid PolicyId, Guid PolicyRevisionId,
    decimal Quantity, string BaseUomId, DateOnly EffectiveFrom, DateOnly EffectiveUntil,
    string State, long Version);

public sealed record ManualQuantityResult(ManualQuantityStatus Status, ManualQuantityReceipt? Value = null);

public sealed record ManualQuantityCommand(ManualQuantityOperation Operation, Guid TenantId,
    Guid LegalEntityId, Guid ActorId, Guid PolicyId, Guid PolicyRevisionId,
    Guid QuantityId, Guid SkuId, Guid WarehouseId, decimal? Quantity,
    DateOnly? EffectiveFrom, DateOnly? EffectiveUntil, string? Reason,
    long? ExpectedVersion, string IdempotencyKey) : IRequest<ManualQuantityResult>;

public sealed record EffectiveManualQuantityQuery(Guid TenantId, Guid LegalEntityId,
    Guid ActorId, Guid SkuId, Guid WarehouseId, DateOnly AsOf)
    : IRequest<ManualQuantityResult>;

public interface IManualQuantityStore
{
    Task<ManualQuantityResult> ExecuteAsync(ManualQuantityCommand command,
        ActorSeriesScope scope, string baseUomId, CancellationToken ct);
    Task<ManualQuantityResult> ReadEffectiveAsync(EffectiveManualQuantityQuery query,
        ActorSeriesScope scope, string baseUomId, CancellationToken ct);
}

public interface ISkuBaseUomReader
{
    Task<string?> ReadAsync(Guid skuId, CancellationToken ct);
}
