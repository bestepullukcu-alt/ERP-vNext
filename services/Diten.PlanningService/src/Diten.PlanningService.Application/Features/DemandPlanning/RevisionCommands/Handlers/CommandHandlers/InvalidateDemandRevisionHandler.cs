using Diten.PlanningService.Application.Features.DemandPlanning.ManualDrafts;
using MediatR;

namespace Diten.PlanningService.Application.Features.DemandPlanning.RevisionCommands;

public sealed class InvalidateDemandRevisionHandler(IManualDraftAuthority assignment,
    IInternalRevisionInvalidator store)
    : IRequestHandler<InvalidateDemandRevisionCommand, Response<RevisionCommandReceipt>>
{
    public async Task<Response<RevisionCommandReceipt>> Handle(
        InvalidateDemandRevisionCommand request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<InvalidationImpactCode>(request.ImpactCode,
                ignoreCase: false, out var impactCode) ||
            !Enum.IsDefined(impactCode) ||
            impactCode == InvalidationImpactCode.ForecastDeviation)
            return Fail("A material impact category is required.", 422);
        var (legalEntityId, errorStatus) = await RevisionCommandScope.ResolveAsync(
            assignment, request.TenantId, request.ActorId,
            request.SelectedLegalEntityHint, cancellationToken);
        if (errorStatus != 0)
            return Fail("Company scope could not be verified.", errorStatus);
        try
        {
            var result = await store.InvalidateAsync(request.TenantId, legalEntityId,
                request.RevisionId, request.ActorId, impactCode,
                request.Reason, request.EvidenceReference,
                request.IdempotencyKey, request.ExpectedContentVersion,
                request.ExpectedStateVersion, cancellationToken);
            return result.Outcome switch
            {
                InvalidationOutcome.Invalidated or InvalidationOutcome.Replayed =>
                    Response<RevisionCommandReceipt>.Success(new(
                        request.RevisionId, result.Outcome.ToString(),
                        result.StateVersion, null)),
                InvalidationOutcome.NotFound => Fail("Revision was not found.", 404),
                InvalidationOutcome.PermissionDenied => Fail("Invalidation is not permitted.", 403),
                InvalidationOutcome.Conflict => Fail("Revision or request conflicts.", 409),
                InvalidationOutcome.InvalidRequest or InvalidationOutcome.MaterialImpactMissing =>
                    Fail("Material impact could not be established.", 422),
                _ => Fail("Invalidation could not be verified.", 503)
            };
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            return Fail("Invalidation could not be verified.", 503);
        }
    }

    private static Response<RevisionCommandReceipt> Fail(string message, int status) =>
        Response<RevisionCommandReceipt>.Fail(message, status);
}
