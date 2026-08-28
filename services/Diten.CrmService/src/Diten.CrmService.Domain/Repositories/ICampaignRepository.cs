using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Domain.Repositories;

/// <summary>
/// MOD-0165 FU04 campaign master. Tenant scoped and soft-delete aware. There is deliberately <b>no delete method</b>:
/// closing a campaign is the soft archive lifecycle, so campaign history stays readable.
/// </summary>
public interface ICampaignRepository
{
    Task<Campaign?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);

    /// <summary>All non-deleted campaigns of a tenant (any status, archived included — history must stay readable).</summary>
    Task<IReadOnlyList<Campaign>> ListAsync(Guid tenantId, CancellationToken cancellationToken);

    /// <summary>The first non-deleted, non-archived campaign carrying <paramref name="campaignCode"/> (duplicate-code
    /// guard). An archived code is reusable.</summary>
    Task<Campaign?> GetActiveByCodeAsync(Guid tenantId, string campaignCode, CancellationToken cancellationToken);

    Task<Campaign?> FindByExternalReferenceAsync(
        Guid tenantId, string sourceSystem, string externalId, CancellationToken cancellationToken);

    Task InsertAsync(Campaign campaign, CancellationToken cancellationToken);

    Task UpdateAsync(Campaign campaign, CancellationToken cancellationToken);
}

/// <summary>
/// MOD-0165 FU04 campaign target store. Tenant scoped, soft-delete aware, <b>no delete method</b>: a target is closed
/// with archive/exclusion so the campaign's targeting history — including why someone was excluded — survives. A
/// snapshot is additive: it never removes an earlier target.
/// </summary>
public interface ICampaignTargetRepository
{
    Task<CampaignTarget?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);

    /// <summary>All non-deleted targets of one campaign (any status, archived included).</summary>
    Task<IReadOnlyList<CampaignTarget>> ListByCampaignAsync(
        Guid tenantId, Guid campaignId, CancellationToken cancellationToken);

    /// <summary>
    /// Duplicate/idempotency lookup: the existing non-archived target for this exact
    /// (campaign, targetType, targetId) triple, if any. Drives the manual-create 409 and the snapshot reconcile path.
    /// </summary>
    Task<CampaignTarget?> FindActiveByTargetAsync(
        Guid tenantId, Guid campaignId, string targetType, Guid targetId, CancellationToken cancellationToken);

    Task InsertAsync(CampaignTarget target, CancellationToken cancellationToken);

    Task UpdateAsync(CampaignTarget target, CancellationToken cancellationToken);
}
