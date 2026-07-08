using Diten.HcmService.Application.Common;
using Diten.HcmService.Application.Common.Models;
using Diten.HcmService.Application.Features.CoreHrEmployeeMaster.Commands;
using Diten.HcmService.Domain.Entities;
using Diten.HcmService.Domain.Repositories;
using MediatR;

namespace Diten.HcmService.Application.Features.CoreHrEmployeeMaster.Handlers;

public sealed class PatchEmployeeDraftHandler : IRequestHandler<PatchEmployeeDraftCommand, Response<EmployeeDraftResponse>>
{
    private readonly ITenantContext _tenantContext;
    private readonly IEmployeeDraftSessionRepository _repository;
    private readonly IDraftAuditService _auditService;

    public PatchEmployeeDraftHandler(
        ITenantContext tenantContext,
        IEmployeeDraftSessionRepository repository,
        IDraftAuditService auditService)
    {
        _tenantContext = tenantContext;
        _repository = repository;
        _auditService = auditService;
    }

    public async Task<Response<EmployeeDraftResponse>> Handle(PatchEmployeeDraftCommand request, CancellationToken cancellationToken)
    {
        if (!EmployeeDraftHandlerHelpers.TryGetTenantId(_tenantContext, out var tenantId))
        {
            return EmployeeDraftHandlerHelpers.MissingTenant<EmployeeDraftResponse>();
        }

        var draftSession = await _repository.GetByIdAsync(tenantId, request.DraftSessionId, cancellationToken);
        if (draftSession is null)
        {
            return Response<EmployeeDraftResponse>.Fail("Draft session not found.", 404);
        }

        if (EmployeeDraftHandlerHelpers.IsStale(request.IfMatch, draftSession))
        {
            return Response<EmployeeDraftResponse>.Fail("Draft session version conflict.", 409);
        }

        var idempotencyKeyHash = EmployeeDraftPayloadGuard.HashIdempotencyKey(request.Request.IdempotencyKey);
        if (draftSession.OperationIdempotencyKeyHashes.Contains(idempotencyKeyHash, StringComparer.Ordinal))
        {
            return Response<EmployeeDraftResponse>.Success(EmployeeDraftMapper.ToDraftResponse(draftSession), 200);
        }

        var expectedVersion = draftSession.Version;
        draftSession.Steps[request.Request.StepCode] = new EmployeeDraftStep
        {
            StepCode = request.Request.StepCode,
            PayloadSchemaVersion = request.Request.PayloadSchemaVersion,
            Payload = EmployeeDraftPayloadGuard.NormalizePayload(request.Request.StepPayload),
            ClientValidationState = EmployeeDraftPayloadGuard.NormalizeOptionalPayload(request.Request.ClientValidationState),
            UpdatedAt = DateTimeOffset.UtcNow
        };
        draftSession.StepStatuses[request.Request.StepCode] = "saved";
        draftSession.CurrentStep = request.Request.StepCode;
        draftSession.ReviewState = "not_reviewed";
        draftSession.Touch(idempotencyKeyHash);

        await _auditService.EmitAsync(new DraftAuditEvent(
            "employee_draft.updated",
            tenantId,
            null,
            draftSession.Id,
            null,
            idempotencyKeyHash,
            new Dictionary<string, string>
            {
                ["step_code"] = request.Request.StepCode,
                ["version"] = draftSession.Version.ToString()
            }), cancellationToken);

        var replaced = await _repository.ReplaceAsync(draftSession, expectedVersion, cancellationToken);
        if (!replaced)
        {
            return Response<EmployeeDraftResponse>.Fail("Draft session version conflict.", 409);
        }

        return Response<EmployeeDraftResponse>.Success(EmployeeDraftMapper.ToDraftResponse(draftSession));
    }
}
