using MediatR;

namespace Diten.PlanningService.Application.Features.DemandPlanning;

public sealed class GetInvalidatedAuditSnapshotHandler(
    IInternalInvalidatedHistoryReader reader)
    : IRequestHandler<GetInvalidatedAuditSnapshotQuery,
        Response<InvalidatedAuditSnapshotView>>
{
    public async Task<Response<InvalidatedAuditSnapshotView>> Handle(
        GetInvalidatedAuditSnapshotQuery query, CancellationToken cancellationToken)
    {
        var result = await reader.ReadAsync(query.TenantId,
            query.LegalEntityId, query.RevisionId, query.ActorId,
            cancellationToken);
        if (result.Outcome != SnapshotReadOutcome.Found || result.Data is null)
            return RevisionReadFailure.FromSnapshot<InvalidatedAuditSnapshotView>(result.Outcome);
        if (result.Data.Invalidation is null)
            return Response<InvalidatedAuditSnapshotView>.Fail(
                "Invalidation audit evidence is unavailable.", 503);
        return Response<InvalidatedAuditSnapshotView>.Success(
            new InvalidatedAuditSnapshotView(result.Data.Warning,
                RevisionReadViewMapper.Manifest(result.Data.Manifest,
                    result.Data.Invalidation)));
    }
}
