using Diten.CrmService.Domain.Entities.Commercial;

namespace Diten.CrmService.Domain.Repositories.Commercial;

// WP-VW-W6 W6-BE-a — MOD-0168 §3 / §5. Every read and write is tenant-scoped; an optimistic replace answers false on a
// stale version (the handler turns it into 409).

/// <summary>The list filter. <see cref="RepUserIds"/> null = every rep (read-all); otherwise the caller's scope, OR the
/// orders where <see cref="OrApproverUserId"/> is a resolved approver.</summary>
public sealed record PreOrderFilter(
    IReadOnlyCollection<string>? Statuses = null, DateTimeOffset? CreatedFrom = null, DateTimeOffset? CreatedTo = null,
    IReadOnlyCollection<Guid>? RepUserIds = null, Guid? OrApproverUserId = null, Guid? PharmacyAccountId = null,
    Guid? DepotAccountId = null, Guid? SkuId = null, Guid? LegalEntityId = null, string? CountryCode = null,
    Guid? AwaitingApproverUserId = null, string? Search = null);

public interface IPreOrderRepository
{
    Task InsertAsync(PreOrder order, CancellationToken cancellationToken);

    Task<PreOrder?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);

    /// <summary>Writes the order when its stored <c>Version</c> still equals <paramref name="expectedVersion"/>; sets
    /// <c>Version = expectedVersion + 1</c>. False = someone else wrote first.</summary>
    Task<bool> ReplaceAsync(PreOrder order, int expectedVersion, CancellationToken cancellationToken);

    /// <summary>Newest first.</summary>
    Task<(IReadOnlyList<PreOrder> Items, long Total)> ListAsync(Guid tenantId, PreOrderFilter filter, int skip, int take,
        CancellationToken cancellationToken);
}

public interface IPreOrderSettingsRepository
{
    Task<PreOrderSettings?> GetAsync(Guid tenantId, CancellationToken cancellationToken);

    /// <summary>Insert (when <paramref name="expectedVersion"/> is null — the tenant's first save) or optimistic replace.
    /// False = stale / a concurrent first save.</summary>
    Task<bool> SaveAsync(PreOrderSettings settings, int? expectedVersion, CancellationToken cancellationToken);
}

public interface IPreOrderApprovalLinkRepository
{
    Task InsertAsync(PreOrderApprovalLink link, CancellationToken cancellationToken);

    Task<PreOrderApprovalLink?> GetByHashAsync(Guid tenantId, string tokenHash, CancellationToken cancellationToken);

    /// <summary>The ONE consumption: sets ConsumedAt / ConsumedBy only while the link is unconsumed, unexpired at
    /// <paramref name="now"/> and not superseded. False = it was not (any more).</summary>
    Task<bool> TryConsumeAsync(Guid tenantId, Guid linkId, Guid userId, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>Voids every live link of the order (the order left its submission).</summary>
    Task SupersedeAsync(Guid tenantId, Guid preOrderId, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>The compensation of a submission whose order write failed.</summary>
    Task DeleteAsync(Guid tenantId, Guid linkId, CancellationToken cancellationToken);
}

public interface IPreOrderCodeSequenceRepository
{
    /// <summary>The next number of (tenant, prefix) — atomic, upserting, never reset.</summary>
    Task<long> NextAsync(Guid tenantId, string prefix, CancellationToken cancellationToken);

    /// <summary>Whether the prefix already numbered an order (then it is locked).</summary>
    Task<bool> ExistsAsync(Guid tenantId, string prefix, CancellationToken cancellationToken);
}
