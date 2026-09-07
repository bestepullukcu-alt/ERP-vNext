using Diten.Platform.Domain.Repositories;
using Diten.Platform.Application.Features.AccessGovernance.LegalEntityScopeResolution;
using System.Collections.Concurrent;

namespace Diten.Platform.Application.Authorization;

/// <summary>Platform-only Legal Entity candidate derivation. It deliberately performs no MDM validation.</summary>
public sealed class OrgDataScopeCandidateResolver : IOrgDataScopeCandidateResolver
{
    private readonly IOrganizationUnitRepository _organizationUnits;
    private readonly IPositionRepository _positions;
    private readonly IPositionAssignmentRepository _assignments;
    private readonly TimeProvider _timeProvider;
    private readonly IOrgDataScopeCandidateAvailabilityClassifier _availabilityClassifier;
    private readonly ConcurrentDictionary<(Guid TenantId, Guid UserId), OrgDataScopeCandidateSet> _requestMemo = new();

    public OrgDataScopeCandidateResolver(IOrganizationUnitRepository organizationUnits, IPositionRepository positions,
        IPositionAssignmentRepository assignments,
        TimeProvider timeProvider,
        IOrgDataScopeCandidateAvailabilityClassifier availabilityClassifier)
    {
        _organizationUnits = organizationUnits ?? throw new ArgumentNullException(nameof(organizationUnits));
        _positions = positions ?? throw new ArgumentNullException(nameof(positions));
        _assignments = assignments ?? throw new ArgumentNullException(nameof(assignments));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _availabilityClassifier = availabilityClassifier ?? throw new ArgumentNullException(nameof(availabilityClassifier));
    }

    public async Task<OrgDataScopeCandidateSet> ResolveAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken)
    {
        try
        {
            return await ResolveCoreAsync(tenantId, userId, cancellationToken);
        }
        catch (Exception exception) when (_availabilityClassifier.IsUnavailable(exception))
        {
            throw new OrgDataScopeCandidateUnavailableException("Organization scope persistence is unavailable.", exception);
        }
    }

    private async Task<OrgDataScopeCandidateSet> ResolveCoreAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (tenantId == Guid.Empty || userId == Guid.Empty)
        {
            return OrgDataScopeCandidateSet.Empty;
        }

        if (_requestMemo.TryGetValue((tenantId, userId), out var memoized))
        {
            return memoized;
        }

        var now = _timeProvider.GetUtcNow();
        var positionIds = (await _assignments.GetAllAsync(cancellationToken))
            .Where(x => x.TenantId == tenantId && !x.IsDeleted && !x.IsCancelled && x.UserId == userId
                && x.EffectiveFrom <= now && (x.EffectiveTo is null || x.EffectiveTo > now))
            .Select(x => x.PositionId)
            .Where(x => x != Guid.Empty)
            .Distinct()
            .Take(TrustedLegalEntityScopeResolutionLimits.MaxCandidates + 1)
            .ToArray();
        if (positionIds.Length == 0)
        {
            _requestMemo.TryAdd((tenantId, userId), OrgDataScopeCandidateSet.Empty);
            return OrgDataScopeCandidateSet.Empty;
        }

        if (positionIds.Length > TrustedLegalEntityScopeResolutionLimits.MaxCandidates)
        {
            throw new OrgDataScopeCandidateContractException("Active position candidate bound exceeded.");
        }

        var positionIdSet = positionIds.ToHashSet();
        var activePositions = (await _positions.GetAllAsync(cancellationToken))
            .Where(position => positionIdSet.Contains(position.Id)
                && position.TenantId == tenantId
                && !position.IsDeleted
                && !position.IsArchived)
            .ToArray();

        var organizationUnitIds = activePositions
            .Select(position => position.OrganizationUnitId)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .Take(TrustedLegalEntityScopeResolutionLimits.MaxCandidates + 1)
            .ToArray();
        if (organizationUnitIds.Length > TrustedLegalEntityScopeResolutionLimits.MaxCandidates)
        {
            throw new OrgDataScopeCandidateContractException("Organization Unit candidate bound exceeded.");
        }

        var organizationUnitIdSet = organizationUnitIds.ToHashSet();
        var legalEntityIds = (await _organizationUnits.GetAllAsync(cancellationToken))
            .Where(unit => organizationUnitIdSet.Contains(unit.Id)
                && unit.TenantId == tenantId
                && !unit.IsDeleted
                && !unit.IsArchived
                && unit.LegalEntityId != Guid.Empty)
            .Select(unit => unit.LegalEntityId)
            .Distinct()
            .Take(TrustedLegalEntityScopeResolutionLimits.MaxCandidates + 1)
            .OrderBy(id => id.ToString("D"), StringComparer.Ordinal)
            .ToArray();
        if (legalEntityIds.Length > TrustedLegalEntityScopeResolutionLimits.MaxCandidates)
        {
            throw new OrgDataScopeCandidateContractException("Legal Entity candidate bound exceeded.");
        }

        var result = new OrgDataScopeCandidateSet(legalEntityIds);
        _requestMemo.TryAdd((tenantId, userId), result);
        return result;
    }
}
