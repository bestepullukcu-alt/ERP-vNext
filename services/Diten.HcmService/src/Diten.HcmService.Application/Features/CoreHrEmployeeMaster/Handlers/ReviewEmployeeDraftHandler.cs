using Diten.HcmService.Application.Common;
using Diten.HcmService.Application.Common.Models;
using Diten.HcmService.Application.Features.CoreHrEmployeeMaster.Commands;
using Diten.HcmService.Domain.Repositories;
using MediatR;

namespace Diten.HcmService.Application.Features.CoreHrEmployeeMaster.Handlers;

public sealed class ReviewEmployeeDraftHandler : IRequestHandler<ReviewEmployeeDraftCommand, Response<DraftReviewResponse>>
{
    private readonly ITenantContext _tenantContext;
    private readonly IEmployeeDraftSessionRepository _repository;
    private readonly IDraftAuditService _auditService;

    public ReviewEmployeeDraftHandler(
        ITenantContext tenantContext,
        IEmployeeDraftSessionRepository repository,
        IDraftAuditService auditService)
    {
        _tenantContext = tenantContext;
        _repository = repository;
        _auditService = auditService;
    }

    public async Task<Response<DraftReviewResponse>> Handle(ReviewEmployeeDraftCommand request, CancellationToken cancellationToken)
    {
        if (!EmployeeDraftHandlerHelpers.TryGetTenantId(_tenantContext, out var tenantId))
        {
            return EmployeeDraftHandlerHelpers.MissingTenant<DraftReviewResponse>();
        }

        var draftSession = await _repository.GetByIdAsync(tenantId, request.DraftSessionId, cancellationToken);
        if (draftSession is null)
        {
            return Response<DraftReviewResponse>.Fail("Draft session not found.", 404);
        }

        var currentETag = request.Request.ETag ?? request.IfMatch;
        if (EmployeeDraftHandlerHelpers.IsStale(currentETag, draftSession))
        {
            return Response<DraftReviewResponse>.Fail("Draft session version conflict.", 409);
        }

        var idempotencyKeyHash = EmployeeDraftPayloadGuard.HashIdempotencyKey(request.Request.IdempotencyKey);
        if (draftSession.OperationIdempotencyKeyHashes.Contains(idempotencyKeyHash, StringComparer.Ordinal))
        {
            return Response<DraftReviewResponse>.Success(BuildResponse(draftSession));
        }

        var blockingReasons = EmployeeDraftHandlerHelpers.BuildReviewBlockingReasons(draftSession);
        var expectedVersion = draftSession.Version;
        draftSession.ReviewBlockingReasons = blockingReasons.ToList();
        draftSession.ReviewState = blockingReasons.Count == 0 ? "reviewed" : "blocked";
        draftSession.StepStatuses["review"] = draftSession.ReviewState;
        draftSession.Touch(idempotencyKeyHash);

        await _auditService.EmitAsync(new DraftAuditEvent(
            "employee_draft.reviewed",
            tenantId,
            null,
            draftSession.Id,
            null,
            idempotencyKeyHash,
            new Dictionary<string, string>
            {
                ["review_state"] = draftSession.ReviewState,
                ["blocking_reason_count"] = blockingReasons.Count.ToString(),
                ["version"] = draftSession.Version.ToString()
            }), cancellationToken);

        var replaced = await _repository.ReplaceAsync(draftSession, expectedVersion, cancellationToken);
        if (!replaced)
        {
            return Response<DraftReviewResponse>.Fail("Draft session version conflict.", 409);
        }

        return Response<DraftReviewResponse>.Success(BuildResponse(draftSession));
    }

    private static DraftReviewResponse BuildResponse(Diten.HcmService.Domain.Entities.EmployeeDraftSession draftSession)
    {
        var validationSummary = EmployeeDraftMapper.ToReferenceValidationResponse(draftSession.ReferenceValidationSummary);
        return new DraftReviewResponse(
            draftSession.Id,
            draftSession.ReviewState,
            false,
            draftSession.ReviewBlockingReasons,
            validationSummary,
            draftSession.Version,
            draftSession.ETag);
    }
}
