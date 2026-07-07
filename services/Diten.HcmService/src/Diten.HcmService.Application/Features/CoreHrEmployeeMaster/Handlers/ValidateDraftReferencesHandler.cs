using Diten.HcmService.Application.Common;
using Diten.HcmService.Application.Common.Models;
using Diten.HcmService.Application.Features.CoreHrEmployeeMaster.Commands;
using Diten.HcmService.Domain.Entities;
using Diten.HcmService.Domain.Repositories;
using MediatR;

namespace Diten.HcmService.Application.Features.CoreHrEmployeeMaster.Handlers;

public sealed class ValidateDraftReferencesHandler : IRequestHandler<ValidateDraftReferencesCommand, Response<ReferenceValidationResponse>>
{
    private readonly ITenantContext _tenantContext;
    private readonly IEmployeeDraftSessionRepository _repository;
    private readonly IReferenceValidationClient _referenceValidationClient;
    private readonly IDraftAuditService _auditService;

    public ValidateDraftReferencesHandler(
        ITenantContext tenantContext,
        IEmployeeDraftSessionRepository repository,
        IReferenceValidationClient referenceValidationClient,
        IDraftAuditService auditService)
    {
        _tenantContext = tenantContext;
        _repository = repository;
        _referenceValidationClient = referenceValidationClient;
        _auditService = auditService;
    }

    public async Task<Response<ReferenceValidationResponse>> Handle(ValidateDraftReferencesCommand request, CancellationToken cancellationToken)
    {
        if (!EmployeeDraftHandlerHelpers.TryGetTenantId(_tenantContext, out var tenantId))
        {
            return EmployeeDraftHandlerHelpers.MissingTenant<ReferenceValidationResponse>();
        }

        var draftSession = await _repository.GetByIdAsync(tenantId, request.DraftSessionId, cancellationToken);
        if (draftSession is null)
        {
            return Response<ReferenceValidationResponse>.Fail("Draft session not found.", 404);
        }

        if (EmployeeDraftHandlerHelpers.IsStale(request.IfMatch, draftSession))
        {
            return Response<ReferenceValidationResponse>.Fail("Draft session version conflict.", 409);
        }

        var idempotencyKeyHash = EmployeeDraftPayloadGuard.HashIdempotencyKey(request.Request.IdempotencyKey);
        if (draftSession.OperationIdempotencyKeyHashes.Contains(idempotencyKeyHash, StringComparer.Ordinal))
        {
            return Response<ReferenceValidationResponse>.Success(EmployeeDraftMapper.ToReferenceValidationResponse(draftSession.ReferenceValidationSummary));
        }

        var results = new[]
        {
            await _referenceValidationClient.ValidatePersonAsync(request.Request.PersonId, cancellationToken),
            await _referenceValidationClient.ValidateOrganizationUnitAsync(request.Request.OrganizationUnitId, cancellationToken),
            await _referenceValidationClient.ValidatePositionAsync(request.Request.PositionId, cancellationToken),
            await _referenceValidationClient.ValidateLegalEntityAsync(request.Request.LegalEntityId, cancellationToken)
        };

        var response = EmployeeDraftHandlerHelpers.BuildReferenceValidationResponse(results);
        var expectedVersion = draftSession.Version;
        draftSession.ReferenceValidationSummary = new EmployeeReferenceValidationSummary
        {
            CanReview = response.CanReview,
            ValidatedAt = DateTimeOffset.UtcNow,
            Results = response.Results.Select(EmployeeDraftMapper.ToDomainReferenceValidationItem).ToList()
        };
        draftSession.StepStatuses["references"] = response.CanReview ? "validated" : "blocked";
        draftSession.ReviewState = "not_reviewed";
        draftSession.Touch(idempotencyKeyHash);

        await _auditService.EmitAsync(new DraftAuditEvent(
            "employee_draft.references_validated",
            tenantId,
            null,
            draftSession.Id,
            null,
            idempotencyKeyHash,
            new Dictionary<string, string>
            {
                ["can_review"] = response.CanReview.ToString(),
                ["version"] = draftSession.Version.ToString()
            }), cancellationToken);

        var replaced = await _repository.ReplaceAsync(draftSession, expectedVersion, cancellationToken);
        if (!replaced)
        {
            return Response<ReferenceValidationResponse>.Fail("Draft session version conflict.", 409);
        }

        return Response<ReferenceValidationResponse>.Success(response);
    }
}
