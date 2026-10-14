namespace Diten.InventoryService.Domain.Traceability.Repositories;

/// <summary>
/// B-3 / CT-D8: sets <c>Expired</c> on every lot (any tenant, any status) whose expiry date is before the UTC day of
/// <c>now</c>, one transaction per lot, with a <c>SystemExpired</c> history row ("Sistem (otomatik)"). Returns the count.
/// </summary>
public interface ILotExpirySweeper
{
    Task<int> SweepAsync(DateTimeOffset now, CancellationToken cancellationToken = default);
}
