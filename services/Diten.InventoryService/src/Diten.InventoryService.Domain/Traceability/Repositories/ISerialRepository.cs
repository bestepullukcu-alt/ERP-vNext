using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Traceability.Entities;
using Diten.InventoryService.Domain.Traceability.Enums;

namespace Diten.InventoryService.Domain.Traceability.Repositories;

/// <summary>
/// The MOD-0174 serial store — read side (P1). Serials are written by the ledger posting (slice P2). Tenant-wide like
/// lots (CT-D7); no delete member.
/// </summary>
public interface ISerialRepository
{
    Task<SerialNumber?> FindAsync(Guid serialId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SerialNumber>> FindManyAsync(IReadOnlyCollection<Guid> serialIds, CancellationToken cancellationToken = default);

    Task<SerialPage> ListAsync(SerialListQuery query, CancellationToken cancellationToken = default);
}

/// <summary>
/// listSerials filters. The stored status is Active / Consumed; "Blocked" is derived from the lot, so a Blocked or Active
/// filter is expressed as Active + inside / outside <see cref="UnusableLotIds"/>.
/// </summary>
public sealed record SerialListQuery(
    string? Search,
    Guid? SkuId,
    SkuLevel? SkuLevel,
    Guid? LotId,
    SerialStatus? StoredStatus,
    IReadOnlyCollection<Guid>? InsideLotIds,
    IReadOnlyCollection<Guid>? OutsideLotIds,
    int Page,
    int PageSize);

public sealed record SerialPage(IReadOnlyList<SerialNumber> Items, long Total);
