using Diten.SafetyStockService.Domain;
using MediatR;

namespace Diten.SafetyStockService.Application;

public enum PolicyReadKind { List, Policy, Revision, Evaluation }
public enum ReferenceCheck { Valid, NotFound, Unavailable }
public enum ReadStatus { Found, NotFound, Unavailable }

public sealed record ActorSeriesScope(IReadOnlySet<SeriesKey> AllowedSeries, bool AllSeriesInLegalEntity = false)
{
    public bool Contains(SeriesKey series) => AllSeriesInLegalEntity || AllowedSeries.Contains(series);
}
public sealed record PolicyReadResult(ReadStatus Status, object? Data = null);

public sealed record PolicyReadQuery(
    PolicyReadKind Kind,
    Guid TenantId,
    Guid ActorId,
    Guid LegalEntityId,
    Guid? PolicyId = null,
    Guid? ChildId = null) : IRequest<PolicyReadResult>;

public interface ILegalEntityReferenceValidator
{
    Task<ReferenceCheck> ValidateAsync(Guid legalEntityId, CancellationToken cancellationToken);
}

public interface IActorSeriesScopeReader
{
    // A null result means the authoritative actor/company or series scope cannot be established.
    Task<ActorSeriesScope?> ResolveAsync(Guid tenantId, Guid actorId, Guid legalEntityId, CancellationToken cancellationToken);
}

public interface IPolicyReadStore
{
    Task<IReadOnlyList<PolicyReadModel>> ListAsync(Guid tenantId, Guid legalEntityId, ActorSeriesScope scope, CancellationToken cancellationToken);
    Task<PolicyReadModel?> GetPolicyAsync(Guid tenantId, Guid legalEntityId, Guid policyId, ActorSeriesScope scope, CancellationToken cancellationToken);
    Task<PolicyRevisionReadModel?> GetRevisionAsync(Guid tenantId, Guid legalEntityId, Guid policyId, Guid revisionId, ActorSeriesScope scope, CancellationToken cancellationToken);
    Task<PolicyEvaluationReadModel?> GetEvaluationAsync(Guid tenantId, Guid legalEntityId, Guid policyId, Guid evaluationId, ActorSeriesScope scope, CancellationToken cancellationToken);
}
