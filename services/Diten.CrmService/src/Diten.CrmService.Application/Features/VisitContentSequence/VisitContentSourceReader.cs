using Diten.CrmService.Application.Common;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;

namespace Diten.CrmService.Application.Features.VisitContentSequence;

/// <summary>
/// WP-SB-3b — the READ seam the visit content resolver v2 needs beyond the journey / strategy / capacity seams: the
/// doctor's journey progress, the tenant's knowledge paths (steps + claims of the version a stage tells), the doctor's
/// specialty and a journey's audience profile. Read-only; every read is bounded by the server-resolved tenant.
/// </summary>
public interface IVisitContentSourceReader
{
    Task<IReadOnlyList<JourneyProgress>> ListProgressAsync(Guid contactId, CancellationToken cancellationToken);

    Task<IReadOnlyList<KnowledgePath>> ListPathsAsync(CancellationToken cancellationToken);

    Task<string?> GetContactSpecialtyAsync(Guid contactId, CancellationToken cancellationToken);

    Task<AudienceProfile?> GetAudienceProfileAsync(Guid audienceProfileId, CancellationToken cancellationToken);

    /// <summary>WP-VP-3C (E7-B1) — the chain template a path is bound to (its branch order names the MAIN branch). The
    /// default answers null: the main branch then falls back to the branch of the path's first step.</summary>
    Task<ConceptChainTemplate?> GetChainTemplateAsync(Guid chainTemplateId, CancellationToken cancellationToken)
        => Task.FromResult<ConceptChainTemplate?>(null);
}

/// <summary>Default <see cref="IVisitContentSourceReader"/> over the repositories (reads only).</summary>
public sealed class VisitContentSourceReader : IVisitContentSourceReader
{
    private readonly ITenantContext _tenant;
    private readonly IJourneyProgressRepository _progress;
    private readonly IKnowledgePathRepository _paths;
    private readonly IContactRepository _contacts;
    private readonly IAudienceProfileRepository _audiences;
    private readonly IConceptChainTemplateRepository? _chains;

    public VisitContentSourceReader(
        ITenantContext tenant,
        IJourneyProgressRepository progress,
        IKnowledgePathRepository paths,
        IContactRepository contacts,
        IAudienceProfileRepository audiences,
        // WP-VP-3C (E7-B1) — the chain template (branch order) of a chain-bound path. Optional.
        IConceptChainTemplateRepository? chains = null)
    {
        _chains = chains;
        _tenant = tenant;
        _progress = progress;
        _paths = paths;
        _contacts = contacts;
        _audiences = audiences;
    }

    public async Task<IReadOnlyList<JourneyProgress>> ListProgressAsync(Guid contactId, CancellationToken cancellationToken)
        => _tenant.TenantId is { } tenantId && contactId != Guid.Empty
            ? await _progress.ListByContactAsync(tenantId, contactId, cancellationToken)
            : Array.Empty<JourneyProgress>();

    public async Task<IReadOnlyList<KnowledgePath>> ListPathsAsync(CancellationToken cancellationToken)
        => _tenant.TenantId is { } tenantId
            ? await _paths.ListAsync(tenantId, cancellationToken)
            : Array.Empty<KnowledgePath>();

    public async Task<string?> GetContactSpecialtyAsync(Guid contactId, CancellationToken cancellationToken)
        => _tenant.TenantId is { } tenantId && contactId != Guid.Empty
            ? (await _contacts.GetByIdAsync(tenantId, contactId, cancellationToken))?.Specialty
            : null;

    public async Task<AudienceProfile?> GetAudienceProfileAsync(Guid audienceProfileId, CancellationToken cancellationToken)
        => _tenant.TenantId is { } tenantId && audienceProfileId != Guid.Empty
            ? await _audiences.GetByIdAsync(tenantId, audienceProfileId, cancellationToken)
            : null;

    public async Task<ConceptChainTemplate?> GetChainTemplateAsync(Guid chainTemplateId, CancellationToken cancellationToken)
        => _chains is not null && _tenant.TenantId is { } tenantId && chainTemplateId != Guid.Empty
            ? await _chains.GetByIdAsync(tenantId, chainTemplateId, cancellationToken)
            : null;
}

/// <summary>
/// WP-SB-3b (DESIGN-SB-3 §5.3, CT default pending the user's decision) — does a journey's audience cover the doctor?
/// There is no shared audience-matching rule in the CRM yet, so ONLY the <c>specialty</c> axis is compared. The answer
/// is three-valued: <c>null</c> when it cannot be decided (no profile, no specialty axis, no doctor specialty) — then
/// nothing is said.
/// <para><see cref="DropOnMismatch"/> is the ONE switch: <c>false</c> = a mismatch is a warning on the item
/// (<c>journey_audience_mismatch</c>) and the product stays; <c>true</c> would drop it like an unpublished path.</para>
/// </summary>
public static class VisitContentAudiencePolicy
{
    public static readonly bool DropOnMismatch = false;

    public const string SpecialtyAxis = "specialty";

    public static bool? Covers(AudienceProfile? profile, string? contactSpecialty)
    {
        if (profile is null || string.IsNullOrWhiteSpace(contactSpecialty))
        {
            return null;
        }

        var axis = profile.Dimensions.FirstOrDefault(d =>
            string.Equals(d.AxisCode?.Trim(), SpecialtyAxis, StringComparison.OrdinalIgnoreCase));
        if (axis is null || axis.Values.Count == 0)
        {
            return null;
        }

        var specialty = contactSpecialty.Trim();
        return axis.Values.Any(v => string.Equals(v?.Trim(), specialty, StringComparison.OrdinalIgnoreCase));
    }

}
