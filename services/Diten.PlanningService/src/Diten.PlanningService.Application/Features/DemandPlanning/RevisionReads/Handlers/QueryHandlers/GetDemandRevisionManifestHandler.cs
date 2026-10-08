using MediatR;

namespace Diten.PlanningService.Application.Features.DemandPlanning;

public sealed class GetDemandRevisionManifestHandler(
    IInternalPublishedSnapshotReader reader)
    : IRequestHandler<GetDemandRevisionManifestQuery, Response<RevisionManifestView>>
{
    public async Task<Response<RevisionManifestView>> Handle(
        GetDemandRevisionManifestQuery query, CancellationToken cancellationToken)
    {
        var result = await reader.ReadManifestAsync(query.TenantId,
            query.LegalEntityId, query.RevisionId, query.ActorId,
            cancellationToken);
        if (result.Outcome != SnapshotReadOutcome.Found || result.Data is null)
            return RevisionReadFailure.FromSnapshot<RevisionManifestView>(result.Outcome);
        return Response<RevisionManifestView>.Success(
            RevisionReadViewMapper.Manifest(result.Data));
    }
}
