using Diten.Platform.Application.Features.AccessGovernance.LegalEntityScopeResolution;
using System.Collections.Concurrent;

namespace Diten.Platform.Application.Authorization;

/// <summary>Platform-only Legal Entity candidate derivation. It deliberately performs no MDM validation.</summary>
public sealed class OrgDataScopeCandidateResolver : IOrgDataScopeCandidateResolver
{
    private readonly IOrgDataScopeCandidateFactReader _factReader;
    private readonly TimeProvider _timeProvider;
    private readonly IOrgDataScopeCandidateAvailabilityClassifier _availabilityClassifier;
    private readonly ConcurrentDictionary<(Guid TenantId, Guid UserId), OrgDataScopeCandidateSet> _requestMemo = new();

    public OrgDataScopeCandidateResolver(
        IOrgDataScopeCandidateFactReader factReader,
        TimeProvider timeProvider,
        IOrgDataScopeCandidateAvailabilityClassifier availabilityClassifier)
    {
        _factReader = factReader ?? throw new ArgumentNullException(nameof(factReader));
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

        var legalEntityIds = (await _factReader.ResolveLegalEntityIdsAsync(
                tenantId,
                userId,
                _timeProvider.GetUtcNow(),
                TrustedLegalEntityScopeResolutionLimits.MaxCandidates,
                cancellationToken))
            .Where(id => id != Guid.Empty)
            .Distinct()
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
