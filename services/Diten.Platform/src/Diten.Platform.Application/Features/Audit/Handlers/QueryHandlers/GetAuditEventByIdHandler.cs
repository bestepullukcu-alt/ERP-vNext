using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Features.Audit.Queries;
using Diten.Platform.Application.Features.Audit.Services;
using Diten.Platform.Domain.Entities.Audit;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Audit.Handlers.QueryHandlers;

public sealed class GetAuditEventByIdHandler : IRequestHandler<GetAuditEventByIdQuery, Response<AuditEventDetailDto>>
{
    private readonly IAuditEventRepository _repository;
    private readonly ISensitiveFieldRedactor _redactor;
    private readonly IAuditMetaAuditWriter _metaAuditWriter;

    public GetAuditEventByIdHandler(
        IAuditEventRepository repository,
        ISensitiveFieldRedactor redactor,
        IAuditMetaAuditWriter metaAuditWriter)
    {
        _repository = repository;
        _redactor = redactor;
        _metaAuditWriter = metaAuditWriter;
    }

    public async Task<Response<AuditEventDetailDto>> Handle(GetAuditEventByIdQuery request, CancellationToken ct)
    {
        if (request.Id == Guid.Empty)
        {
            return Response<AuditEventDetailDto>.Fail("Audit event id is required.", 400);
        }

        var auditEvent = await _repository.GetByIdForPlatformCrossTenantAsync(request.Id, ct);
        if (auditEvent is null)
        {
            await _metaAuditWriter.WriteAsync(new AuditMetaAuditRequest(
                "PlatformAudit.GetAuditEventByIdQuery",
                AuditCategory.DataPrivacy,
                AuditOperation.Execute,
                AuditOutcome.Failed,
                "AuditEvent",
                request.Id,
                AuditTenantIds.PlatformSystemTenantId,
                new Dictionary<string, object?> { ["notFound"] = true }), ct);

            return Response<AuditEventDetailDto>.Fail("Audit event not found.", 404);
        }

        await _metaAuditWriter.WriteAsync(new AuditMetaAuditRequest(
            "PlatformAudit.GetAuditEventByIdQuery",
            AuditCategory.DataPrivacy,
            AuditOperation.Execute,
            AuditOutcome.Succeeded,
            "AuditEvent",
            auditEvent.Id,
            auditEvent.TenantId,
            new Dictionary<string, object?>
            {
                ["tenantId"] = auditEvent.TenantId,
                ["category"] = auditEvent.Category.ToString(),
                ["entityType"] = auditEvent.EntityType,
                ["entityId"] = auditEvent.EntityId
            }), ct);

        return Response<AuditEventDetailDto>.Success(AuditEventMapper.ToDetailDto(auditEvent, _redactor));
    }
}
