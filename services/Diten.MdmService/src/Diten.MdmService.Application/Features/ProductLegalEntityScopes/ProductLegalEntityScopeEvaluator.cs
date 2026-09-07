using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;

namespace Diten.MdmService.Application.Features.ProductLegalEntityScopes;

public sealed class ProductLegalEntityScopeEvaluator : IProductLegalEntityScopeEvaluator
{
    public ProductLegalEntityScopeEvaluationResult Evaluate(ProductLegalEntityScopeEvaluationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.TenantId == Guid.Empty || request.GlobalProductId == Guid.Empty)
        {
            throw new ArgumentException("Expected tenant and product identities must be non-empty.", nameof(request));
        }
        if (request.ServerNowUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Server time must be UTC.", nameof(request));
        }

        ArgumentNullException.ThrowIfNull(request.RolloutState);
        request.RolloutState.EnsureValid();
        if (request.RolloutState.IsDeleted || request.RolloutState.TenantId != request.TenantId)
        {
            return DeniedInvalidScopeEvidence(request);
        }

        var rolloutMode = request.RolloutState.Mode;
        if (request.Policy is not null
            && (request.Policy.IsDeleted
                || request.Policy.TenantId != request.TenantId
                || request.Policy.GlobalProductId != request.GlobalProductId))
        {
            return DeniedInvalidScopeEvidence(request);
        }

        if (rolloutMode == ProductLegalEntityScopeRolloutMode.Preparation)
        {
            return Result(
                request.ExistingTenantAccessAllowed,
                request.ExistingTenantAccessAllowed
                    ? ProductLegalEntityScopeDecisionReason.PreparationExistingTenantAccessAllowed
                    : ProductLegalEntityScopeDecisionReason.PreparationExistingTenantAccessDenied,
                request,
                [],
                []);
        }
        if (rolloutMode == ProductLegalEntityScopeRolloutMode.FailClosedSuspended)
        {
            return Result(
                false,
                ProductLegalEntityScopeDecisionReason.FailClosedSuspended,
                request,
                [],
                []);
        }

        var trusted = NormalizeCandidates(request.TrustedLegalEntityIds, nameof(request.TrustedLegalEntityIds));
        var local = NormalizeCandidates(
            request.LocallyReferenceableLegalEntityIds,
            nameof(request.LocallyReferenceableLegalEntityIds));
        var effectiveCandidates = SortCanonical(trusted.Intersect(local));

        if (request.Policy is null)
        {
            return Result(
                false,
                ProductLegalEntityScopeDecisionReason.LegacyUnclassified,
                request,
                effectiveCandidates,
                []);
        }

        request.Policy.EnsureValid(request.ServerNowUtc);
        var current = request.Policy.GetEffectivePeriod(request.ServerNowUtc);
        if (current is null)
        {
            return Result(
                false,
                ProductLegalEntityScopeDecisionReason.NoCurrentScopePeriod,
                request,
                effectiveCandidates,
                []);
        }

        if (current.Mode == ProductLegalEntityScopeMode.GroupWide)
        {
            var allowed = effectiveCandidates.Count > 0;
            return Result(
                allowed,
                allowed
                    ? ProductLegalEntityScopeDecisionReason.GroupWideCandidateMatched
                    : ProductLegalEntityScopeDecisionReason.GroupWideCandidateEmpty,
                request,
                effectiveCandidates,
                allowed ? effectiveCandidates : [],
                current.Mode);
        }

        var matched = SortCanonical(current.LegalEntityIds.Intersect(effectiveCandidates));
        return Result(
            matched.Count > 0,
            matched.Count > 0
                ? ProductLegalEntityScopeDecisionReason.ScopedCandidateMatched
                : ProductLegalEntityScopeDecisionReason.ScopedCandidateNotMatched,
            request,
            effectiveCandidates,
            matched,
            current.Mode);
    }

    private static ProductLegalEntityScopeEvaluationResult Result(
        bool allowed,
        ProductLegalEntityScopeDecisionReason reason,
        ProductLegalEntityScopeEvaluationRequest request,
        IReadOnlyList<Guid> effectiveCandidates,
        IReadOnlyList<Guid> matched,
        ProductLegalEntityScopeMode? effectiveMode = null) => new(
            allowed,
            reason,
            request.RolloutState.Mode,
            request.Policy is null,
            effectiveMode,
            request.Policy?.Version,
            effectiveCandidates,
            matched);

    private static ProductLegalEntityScopeEvaluationResult DeniedInvalidScopeEvidence(
        ProductLegalEntityScopeEvaluationRequest request) => new(
            false,
            ProductLegalEntityScopeDecisionReason.InvalidScopeEvidence,
            request.RolloutState.Mode,
            request.Policy is null,
            null,
            null,
            [],
            []);

    private static IReadOnlyList<Guid> NormalizeCandidates(
        IReadOnlyCollection<Guid> candidates,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        if (candidates.Count > ProductLegalEntityScopePolicy.MaximumLegalEntityIdsPerSnapshot)
        {
            throw new ArgumentOutOfRangeException(parameterName, "PRODUCT_LEGAL_ENTITY_SCOPE_ID_LIMIT_EXCEEDED");
        }
        if (candidates.Any(id => id == Guid.Empty) || candidates.Distinct().Count() != candidates.Count)
        {
            throw new ArgumentException(
                "PRODUCT_LEGAL_ENTITY_SCOPE_CANDIDATES_MUST_BE_NONEMPTY_UNIQUE",
                parameterName);
        }

        return candidates
            .OrderBy(id => id.ToString("D"), StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<Guid> SortCanonical(IEnumerable<Guid> values) => values
        .OrderBy(id => id.ToString("D"), StringComparer.Ordinal)
        .ToArray();
}
