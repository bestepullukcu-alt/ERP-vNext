using MediatR;

namespace Diten.PlanningService.Application.Features.DemandPlanning;

public sealed record CurrentPublishedRevisionView(string ContractVersion,
    Guid RevisionId, Guid PlanningCycleId, string PlanningPeriodKey,
    Guid TenantId, Guid LegalEntityId, string State, int StateVersion,
    string IntegrityState, RevisionIntegrityView Integrity,
    int ExpectedPartCount, int ExpectedRowCount);

public sealed class GetCurrentPublishedRevisionHandler(
    IInternalCurrentPublishedReader reader)
    : IRequestHandler<GetCurrentPublishedRevisionQuery,
        Response<CurrentPublishedRevisionView>>
{
    public async Task<Response<CurrentPublishedRevisionView>> Handle(
        GetCurrentPublishedRevisionQuery query, CancellationToken cancellationToken)
    {
        var result = await reader.ReadAsync(query.TenantId, query.LegalEntityId,
            query.PlanningPeriodKey, query.ActorId, cancellationToken);
        if (result.Outcome != CurrentPublishedOutcome.Found || result.Revision is null)
            return result.Outcome switch
            {
                CurrentPublishedOutcome.NotFound =>
                    Response<CurrentPublishedRevisionView>.Fail(
                        "Current Published revision was not found.", 404),
                CurrentPublishedOutcome.PermissionDenied =>
                    Response<CurrentPublishedRevisionView>.Fail(
                        "Demand permission is required.", 403),
                _ => Response<CurrentPublishedRevisionView>.Fail(
                    "Current Published revision cannot be verified.", 503)
            };
        var value = result.Revision;
        return Response<CurrentPublishedRevisionView>.Success(
            new CurrentPublishedRevisionView("v2", value.RevisionId,
                value.PlanningCycleId, value.PlanningPeriodKey, value.TenantId,
                value.LegalEntityId, "Published", value.StateVersion, "Verified",
                new RevisionIntegrityView(value.ChecksumScheme, value.Checksum),
                value.ExpectedPartCount, value.ExpectedRowCount));
    }
}
