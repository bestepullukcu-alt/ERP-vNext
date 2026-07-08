using Diten.HcmService.Application.Common;
using Diten.HcmService.Application.Common.Models;
using Diten.HcmService.Application.Features.CoreHrEmployeeMaster.Commands;
using Diten.HcmService.Domain.Entities;
using Diten.HcmService.Domain.Repositories;
using MediatR;

namespace Diten.HcmService.Application.Features.CoreHrEmployeeMaster.Handlers;

public sealed class CreateEmployeeDraftHandler : IRequestHandler<CreateEmployeeDraftCommand, Response<EmployeeDraftCreateResponse>>
{
    private readonly ITenantContext _tenantContext;
    private readonly IEmployeeDraftSessionRepository _repository;
    private readonly IDraftAuditService _auditService;

    public CreateEmployeeDraftHandler(
        ITenantContext tenantContext,
        IEmployeeDraftSessionRepository repository,
        IDraftAuditService auditService)
    {
        _tenantContext = tenantContext;
        _repository = repository;
        _auditService = auditService;
    }

    public async Task<Response<EmployeeDraftCreateResponse>> Handle(CreateEmployeeDraftCommand request, CancellationToken cancellationToken)
    {
        if (!EmployeeDraftHandlerHelpers.TryGetTenantId(_tenantContext, out var tenantId))
        {
            return EmployeeDraftHandlerHelpers.MissingTenant<EmployeeDraftCreateResponse>();
        }

        var idempotencyKeyHash = EmployeeDraftPayloadGuard.HashIdempotencyKey(request.Request.IdempotencyKey);
        var existing = await _repository.GetByCreateIdempotencyKeyAsync(tenantId, idempotencyKeyHash, cancellationToken);
        if (existing is not null)
        {
            return Response<EmployeeDraftCreateResponse>.Success(EmployeeDraftMapper.ToCreateResponse(existing), 200);
        }

        var draftSession = new EmployeeDraftSession
        {
            TenantId = tenantId,
            SourceContext = request.Request.SourceContext,
            ClientReference = request.Request.ClientReference,
            CreateIdempotencyKeyHash = idempotencyKeyHash,
            OperationIdempotencyKeyHashes = [idempotencyKeyHash],
            StepStatuses = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["draft"] = "created",
                ["references"] = "not_validated",
                ["review"] = "not_reviewed"
            }
        };

        await _auditService.EmitAsync(new DraftAuditEvent(
            "employee_draft.created",
            tenantId,
            null,
            draftSession.Id,
            null,
            idempotencyKeyHash,
            new Dictionary<string, string>
            {
                ["draft_schema_version"] = draftSession.DraftSchemaVersion,
                ["version"] = draftSession.Version.ToString()
            }), cancellationToken);

        await _repository.AddAsync(draftSession, cancellationToken);

        return Response<EmployeeDraftCreateResponse>.Success(EmployeeDraftMapper.ToCreateResponse(draftSession), 201);
    }
}
