using Diten.PpmService.Domain.Entities;

namespace Diten.PpmService.Application.Features.Portfolios;

public enum PortfolioTemporaryNonProductionAccessEnvironment
{
    Unknown,
    NonProduction,
    Production
}

// Narrow PPM-local evaluator. It has no identity directory, sharing table, or ACL surface.
public sealed class PortfolioTemporaryNonProductionRecordAccessAuthority(
    bool enabled = false,
    PortfolioTemporaryNonProductionAccessEnvironment environment = PortfolioTemporaryNonProductionAccessEnvironment.Unknown,
    TimeProvider? clock = null) : IPortfolioRecordAccessAuthority
{
    public const string DecisionKey = "portfolio-temporary-nonproduction-access";
    public const string DecisionVersion = "2026-09-11";

    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    private bool IsAvailable => enabled && environment == PortfolioTemporaryNonProductionAccessEnvironment.NonProduction;

    public PortfolioTemporaryNonProductionAccessBinding CreateBinding(Guid portfolioId)
    {
        if (!IsAvailable || portfolioId == Guid.Empty)
            throw new InvalidOperationException("Temporary non-production access is unavailable.");
        return new(DecisionKey, DecisionVersion, _clock.GetUtcNow().UtcDateTime, portfolioId);
    }

    public Task<PortfolioAuthorityEvidence> EvaluateAsync(PortfolioAuthorityScope scope, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var outcome = IsAvailable ? Evaluate(scope) : PortfolioAuthorityOutcome.Unavailable;
        return Task.FromResult(Evidence(scope, outcome));
    }

    private static PortfolioAuthorityOutcome Evaluate(PortfolioAuthorityScope scope)
    {
        if (scope.TenantId == Guid.Empty || scope.ActorId == Guid.Empty || string.IsNullOrWhiteSpace(scope.Operation))
            return PortfolioAuthorityOutcome.Unavailable;

        return scope.Operation switch
        {
            "page" or "create-availability" or "create" =>
                IsBindingFreeScope(scope) ? PortfolioAuthorityOutcome.Allowed : PortfolioAuthorityOutcome.Denied,
            "read" or "edit" or "edit-availability" or "owner-read" => EvaluateRelatedRecord(scope),
            // PMO authority is an additional proof; it cannot replace the actor's record relationship.
            "manage-owner" or "owner-candidates" or "assign-owner" or "transfer-owner" => EvaluateRelatedRecord(scope),
            // No local relationship implies historical visibility.
            "history-read" => PortfolioAuthorityOutcome.Denied,
            _ => PortfolioAuthorityOutcome.Denied
        };
    }

    private static bool IsBindingFreeScope(PortfolioAuthorityScope scope) =>
        scope.PortfolioId is null && scope.RecordTenantId is null && scope.CreatorId is null &&
        scope.CurrentOwnerUserId is null && scope.LifecycleState is null &&
        scope.TemporaryNonProductionAccessBinding is null;

    private static PortfolioAuthorityOutcome EvaluateRelatedRecord(PortfolioAuthorityScope scope)
    {
        var boundary = RecordBoundary(scope);
        if (boundary != PortfolioAuthorityOutcome.Allowed) return boundary;
        return scope.CreatorId == scope.ActorId || scope.CurrentOwnerUserId == scope.ActorId
            ? PortfolioAuthorityOutcome.Allowed
            : PortfolioAuthorityOutcome.Denied;
    }

    private static PortfolioAuthorityOutcome RecordBoundary(PortfolioAuthorityScope scope)
    {
        if (scope.PortfolioId is not { } portfolioId || portfolioId == Guid.Empty || scope.Version is not > 0 ||
            scope.RecordTenantId is null || scope.CreatorId is not { } creatorId || creatorId == Guid.Empty)
            return PortfolioAuthorityOutcome.Unavailable;
        if (scope.RecordTenantId != scope.TenantId || scope.LifecycleState != PortfolioLifecycleState.Draft ||
            scope.TemporaryNonProductionAccessBinding is null)
            return PortfolioAuthorityOutcome.NotFound;

        var binding = scope.TemporaryNonProductionAccessBinding;
        return !binding.HasValidShape() || binding.PortfolioId != portfolioId ||
            !string.Equals(binding.DecisionKey, DecisionKey, StringComparison.Ordinal) ||
            !string.Equals(binding.DecisionVersion, DecisionVersion, StringComparison.Ordinal)
            ? PortfolioAuthorityOutcome.Unavailable
            : PortfolioAuthorityOutcome.Allowed;
    }

    private PortfolioAuthorityEvidence Evidence(PortfolioAuthorityScope scope, PortfolioAuthorityOutcome outcome)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        return new PortfolioAuthorityEvidence(scope, outcome, DecisionVersion, now, now.AddMinutes(1));
    }
}
