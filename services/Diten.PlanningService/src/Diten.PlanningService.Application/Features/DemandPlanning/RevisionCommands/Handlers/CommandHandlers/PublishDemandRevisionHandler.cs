using Diten.PlanningService.Application.Features.DemandPlanning.ManualDrafts;
using MediatR;

namespace Diten.PlanningService.Application.Features.DemandPlanning.RevisionCommands;

public sealed class PublishDemandRevisionHandler(IManualDraftAuthority assignment,
    IInternalPublishedRevisionStore store, TimeProvider clock)
    : IRequestHandler<PublishDemandRevisionCommand, Response<RevisionCommandReceipt>>
{
    public async Task<Response<RevisionCommandReceipt>> Handle(
        PublishDemandRevisionCommand request, CancellationToken cancellationToken)
    {
        var (legalEntityId, errorStatus) = await RevisionCommandScope.ResolveAsync(
            assignment, request.TenantId, request.ActorId,
            request.SelectedLegalEntityHint, cancellationToken);
        if (errorStatus != 0)
            return Response<RevisionCommandReceipt>.Fail("Company scope could not be verified.", errorStatus);
        try
        {
            var result = await store.PublishAsync(request.TenantId, legalEntityId,
                request.RevisionId, request.ActorId, request.IdempotencyKey,
                request.ExpectedContentVersion, request.ExpectedStateVersion,
                clock.GetUtcNow(), cancellationToken);
            return result.Outcome switch
            {
                PublishOutcome.Published or PublishOutcome.Replayed =>
                    Response<RevisionCommandReceipt>.Success(new(
                        request.RevisionId, result.Outcome.ToString(),
                        result.StateVersion, result.Checksum)),
                PublishOutcome.ScopeDenied => Fail("Revision was not found.", 404),
                PublishOutcome.PermissionDenied or PublishOutcome.SeparationDenied =>
                    Fail("Publication is not permitted.", 403),
                PublishOutcome.Conflict => Fail("Revision or request conflicts.", 409),
                PublishOutcome.Invalid => Fail("Revision cannot be published.", 422),
                _ => Fail("Publication could not be verified.", 503)
            };
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            return Fail("Publication could not be verified.", 503);
        }
    }

    private static Response<RevisionCommandReceipt> Fail(string message, int status) =>
        Response<RevisionCommandReceipt>.Fail(message, status);
}
