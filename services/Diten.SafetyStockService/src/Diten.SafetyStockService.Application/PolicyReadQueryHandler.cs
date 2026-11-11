using Diten.SafetyStockService.Domain;
using MediatR;

namespace Diten.SafetyStockService.Application;

public sealed class PolicyReadQueryHandler : IRequestHandler<PolicyReadQuery, PolicyReadResult>
{
    private readonly ILegalEntityReferenceValidator _reference;
    private readonly IActorSeriesScopeReader _scope;
    private readonly IPolicyReadStore _store;

    public PolicyReadQueryHandler(ILegalEntityReferenceValidator reference, IActorSeriesScopeReader scope, IPolicyReadStore store)
    {
        _reference = reference;
        _scope = scope;
        _store = store;
    }

    public async Task<PolicyReadResult> Handle(PolicyReadQuery query, CancellationToken cancellationToken)
    {
        if (query.TenantId == Guid.Empty || query.ActorId == Guid.Empty || query.LegalEntityId == Guid.Empty)
            return new(ReadStatus.Unavailable);

        ReferenceCheck reference;
        try
        {
            reference = await _reference.ValidateAsync(query.LegalEntityId, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return new(ReadStatus.Unavailable);
        }

        if (reference == ReferenceCheck.Unavailable) return new(ReadStatus.Unavailable);
        if (reference == ReferenceCheck.NotFound) return new(ReadStatus.NotFound);

        ActorSeriesScope? scope;
        try
        {
            scope = await _scope.ResolveAsync(query.TenantId, query.ActorId, query.LegalEntityId, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return new(ReadStatus.Unavailable);
        }

        if (scope is null) return new(ReadStatus.Unavailable);
        if (!scope.AllSeriesInLegalEntity && scope.AllowedSeries.Count == 0)
            return query.Kind == PolicyReadKind.List
                ? new(ReadStatus.Found, Array.Empty<PolicyReadModel>())
                : new(ReadStatus.NotFound);

        try
        {
            return query.Kind switch
            {
                PolicyReadKind.List => ListResult(await _store.ListAsync(query.TenantId, query.LegalEntityId, scope, cancellationToken), query, scope),
                PolicyReadKind.Policy when query.PolicyId is { } policyId => Found(await _store.GetPolicyAsync(query.TenantId, query.LegalEntityId, policyId, scope, cancellationToken), query, scope),
                PolicyReadKind.Revision when query.PolicyId is { } policyId && query.ChildId is { } revisionId => Found(await _store.GetRevisionAsync(query.TenantId, query.LegalEntityId, policyId, revisionId, scope, cancellationToken), query, scope),
                PolicyReadKind.Evaluation when query.PolicyId is { } policyId && query.ChildId is { } evaluationId => Found(await _store.GetEvaluationAsync(query.TenantId, query.LegalEntityId, policyId, evaluationId, scope, cancellationToken), query, scope),
                _ => new(ReadStatus.NotFound)
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return new(ReadStatus.Unavailable);
        }
    }

    private static PolicyReadResult ListResult(IReadOnlyList<PolicyReadModel> policies, PolicyReadQuery query, ActorSeriesScope scope)
    {
        if (policies.Any(policy => policy.TenantId != query.TenantId || policy.LegalEntityId != query.LegalEntityId
            || !scope.Contains(policy.Series)))
            return new(ReadStatus.Unavailable);
        return new(ReadStatus.Found, policies);
    }

    private static PolicyReadResult Found(object? value, PolicyReadQuery query, ActorSeriesScope scope)
    {
        if (value is null) return new(ReadStatus.NotFound);
        var valid = value switch
        {
            PolicyReadModel policy => policy.TenantId == query.TenantId && policy.LegalEntityId == query.LegalEntityId
                && policy.Id == query.PolicyId && scope.Contains(policy.Series),
            PolicyRevisionReadModel revision => revision.TenantId == query.TenantId && revision.LegalEntityId == query.LegalEntityId
                && revision.PolicyId == query.PolicyId && revision.Id == query.ChildId && scope.Contains(revision.Series),
            PolicyEvaluationReadModel evaluation => evaluation.TenantId == query.TenantId && evaluation.LegalEntityId == query.LegalEntityId
                && evaluation.PolicyId == query.PolicyId && evaluation.Id == query.ChildId && scope.Contains(evaluation.Series),
            _ => false
        };
        return valid ? new(ReadStatus.Found, value) : new(ReadStatus.NotFound);
    }
}
