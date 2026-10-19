using Diten.PlanningService.Application.Features.DemandPlanning.ManualDrafts;
using MediatR;

namespace Diten.PlanningService.Application.Features.DemandPlanning.RevisionCommands;

public sealed class RollbackDemandRevisionHandler(IManualDraftAuthority assignment,
    IRollbackDraftStore store, TimeProvider clock)
    : IRequestHandler<RollbackDemandRevisionCommand, Response<ManualDraftView>>
{
    public async Task<Response<ManualDraftView>> Handle(
        RollbackDemandRevisionCommand request, CancellationToken cancellationToken)
    {
        if (!request.HasCreatePermission)
            return Fail("Draft creation is not permitted.", 403);
        var (legalEntityId, errorStatus) = await RevisionCommandScope.ResolveAsync(
            assignment, request.TenantId, request.ActorId,
            request.SelectedLegalEntityHint, cancellationToken);
        if (errorStatus != 0)
            return Fail("Company scope could not be verified.", errorStatus);
        try
        {
            var result = await store.CreateAsync(request.TenantId, legalEntityId,
                request.SourceRevisionId, request.ActorId, request.Reason,
                request.IdempotencyKey, request.ExpectedSourceStateVersion,
                clock.GetUtcNow(), cancellationToken);
            return result.Outcome switch
            {
                RollbackDraftOutcome.Created or RollbackDraftOutcome.Replayed
                    when result.Draft is not null =>
                    Response<ManualDraftView>.Success(ManualDraftViewMapper.Map(
                        result.Draft, result.Outcome == RollbackDraftOutcome.Replayed),
                        result.Outcome == RollbackDraftOutcome.Created ? 201 : 200),
                RollbackDraftOutcome.NotFound => Fail("Source revision was not found.", 404),
                RollbackDraftOutcome.PermissionDenied => Fail("Source read is not permitted.", 403),
                RollbackDraftOutcome.Conflict => Fail("Source version or request conflicts.", 409),
                RollbackDraftOutcome.InvalidSource => Fail("Source cannot be copied.", 422),
                _ => Fail("Source, authority or mandatory audit could not be verified.", 503)
            };
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            return Fail("Source, authority or mandatory audit could not be verified.", 503);
        }
    }

    private static Response<ManualDraftView> Fail(string message, int status) =>
        Response<ManualDraftView>.Fail(message, status);
}
