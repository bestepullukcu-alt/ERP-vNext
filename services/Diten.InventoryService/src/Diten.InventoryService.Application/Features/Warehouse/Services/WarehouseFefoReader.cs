using Diten.InventoryService.Application.Features.Expiry.Ports;
using Diten.InventoryService.Application.Features.Warehouse.Ports;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Expiry.Enums;
using Diten.InventoryService.Domain.Expiry.Repositories;
using Diten.InventoryService.Domain.Expiry.Rules;

namespace Diten.InventoryService.Application.Features.Warehouse.Services;

/// <summary>
/// <see cref="IWarehouseFefoReader"/> over the MOD-0176 read model and pure rule (the <c>GetFefoOrderHandler</c> pattern, no second ordering):
/// the candidates of the posting hook's reader without the free base, ordered by <see cref="FefoOrderRule.Order"/> on the UTC day of
/// <paramref name="asOf"/>; the policy row of the caller's company or the defaults.
/// </summary>
public sealed class WarehouseFefoReader : IWarehouseFefoReader
{
    private readonly IExpiryReadModel _reads;
    private readonly IExpiryRepository _policies;

    public WarehouseFefoReader(IExpiryReadModel reads, IExpiryRepository policies)
    {
        _reads = reads;
        _policies = policies;
    }

    public async Task<FefoOrder> OrderAsync(Guid warehouseId, Guid itemId, Guid skuId, SkuLevel skuLevel, int? minRemainingShelfLifeDays, DateTimeOffset asOf,
        CancellationToken cancellationToken = default)
    {
        var candidates = await _reads.FefoCandidatesAsync(warehouseId.ToString("D"), itemId, skuId, skuLevel, cancellationToken);
        var day = DateOnly.FromDateTime(asOf.UtcDateTime);
        return FefoOrderRule.Order(candidates, new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero), minRemainingShelfLifeDays);
    }

    public async Task<FefoOutboundMode> OutboundModeAsync(CancellationToken cancellationToken = default)
        => (await _policies.FindPolicyAsync(cancellationToken) ?? ExpiryPolicyRules.Defaults()).FefoOutboundMode;
}
