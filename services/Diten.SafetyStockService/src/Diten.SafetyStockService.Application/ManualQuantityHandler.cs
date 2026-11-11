using Diten.SafetyStockService.Domain;
using MediatR;

namespace Diten.SafetyStockService.Application;

public sealed class ManualQuantityHandler : IRequestHandler<ManualQuantityCommand, ManualQuantityResult>,
    IRequestHandler<EffectiveManualQuantityQuery, ManualQuantityResult>
{
    private readonly ILegalEntityReferenceValidator _companies;
    private readonly IActorSeriesScopeReader _scope;
    private readonly ISkuBaseUomReader _uom;
    private readonly IManualQuantityStore _store;

    public ManualQuantityHandler(ILegalEntityReferenceValidator companies, IActorSeriesScopeReader scope,
        ISkuBaseUomReader uom, IManualQuantityStore store)
    {
        _companies = companies;
        _scope = scope;
        _uom = uom;
        _store = store;
    }

    public Task<ManualQuantityResult> Handle(ManualQuantityCommand command, CancellationToken ct)
        => RunAsync(command.TenantId, command.LegalEntityId, command.ActorId, command.SkuId,
            command.WarehouseId, (scope, uom) => _store.ExecuteAsync(command, scope, uom, ct), ct);

    public Task<ManualQuantityResult> Handle(EffectiveManualQuantityQuery query, CancellationToken ct)
        => RunAsync(query.TenantId, query.LegalEntityId, query.ActorId, query.SkuId,
            query.WarehouseId, (scope, uom) => _store.ReadEffectiveAsync(query, scope, uom, ct), ct);

    private async Task<ManualQuantityResult> RunAsync(Guid tenant, Guid company, Guid actor,
        Guid sku, Guid warehouse, Func<ActorSeriesScope, string, Task<ManualQuantityResult>> action,
        CancellationToken ct)
    {
        if (tenant == Guid.Empty || company == Guid.Empty || actor == Guid.Empty ||
            sku == Guid.Empty || warehouse == Guid.Empty)
            return new(ManualQuantityStatus.Invalid);
        try
        {
            var reference = await _companies.ValidateAsync(company, ct);
            if (reference == ReferenceCheck.NotFound) return new(ManualQuantityStatus.NotFound);
            if (reference != ReferenceCheck.Valid) return new(ManualQuantityStatus.Unavailable);
            var scope = await _scope.ResolveAsync(tenant, actor, company, ct);
            if (scope is null) return new(ManualQuantityStatus.Unavailable);
            if (!scope.Contains(new SeriesKey(sku, warehouse))) return new(ManualQuantityStatus.NotFound);
            var baseUom = await _uom.ReadAsync(sku, ct);
            if (string.IsNullOrWhiteSpace(baseUom)) return new(ManualQuantityStatus.Unavailable);
            return await action(scope, baseUom);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception) { return new(ManualQuantityStatus.Unavailable); }
    }
}
