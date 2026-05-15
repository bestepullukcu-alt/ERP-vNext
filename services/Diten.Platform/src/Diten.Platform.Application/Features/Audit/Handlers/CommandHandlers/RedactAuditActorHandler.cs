using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Audit.Commands;
using Diten.Platform.Application.Features.Audit.Services;
using Diten.Platform.Domain.Entities.Audit;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Audit.Handlers.CommandHandlers;

public sealed class RedactAuditActorHandler
    : IRequestHandler<RedactAuditActorCommand, Response<RedactAuditActorResultDto>>
{
    private readonly IAuditEventRepository _repository;
    private readonly IAuditMetaAuditWriter _metaAuditWriter;
    private readonly ICurrentUserContext _currentUserContext;

    public RedactAuditActorHandler(
        IAuditEventRepository repository,
        IAuditMetaAuditWriter metaAuditWriter,
        ICurrentUserContext currentUserContext)
    {
        _repository = repository;
        _metaAuditWriter = metaAuditWriter;
        _currentUserContext = currentUserContext;
    }

    public async Task<Response<RedactAuditActorResultDto>> Handle(RedactAuditActorCommand request, CancellationToken ct)
    {
        if (request.Request.ActorId == Guid.Empty)
        {
            return Response<RedactAuditActorResultDto>.Fail("Actor id is required.", 400);
        }

        if (string.IsNullOrWhiteSpace(request.Request.Reason))
        {
            return Response<RedactAuditActorResultDto>.Fail("Redaction reason is required.", 400);
        }

        if (_currentUserContext.UserId == Guid.Empty)
        {
            return Response<RedactAuditActorResultDto>.Fail("Authenticated platform actor id is required for audit redaction.", 400);
        }

        var redactedAtUtc = DateTimeOffset.UtcNow;
        var redactedCount = await _repository.RedactActorPiiForPlatformCrossTenantAsync(
            new AuditActorPiiRedactionRequest(
                request.Request.ActorId,
                redactedAtUtc,
                _currentUserContext.UserId,
                request.Request.Reason.Trim()),
            ct);

        await _metaAuditWriter.WriteAsync(new AuditMetaAuditRequest(
            "PlatformAudit.RedactAuditActorCommand",
            AuditCategory.DataPrivacy,
            AuditOperation.Redact,
            AuditOutcome.Succeeded,
            "AuditActor",
            request.Request.ActorId,
            AuditTenantIds.PlatformSystemTenantId,
            new Dictionary<string, object?>
            {
                ["actorId"] = request.Request.ActorId,
                ["redactedEventCount"] = redactedCount,
                ["redactedAtUtc"] = redactedAtUtc,
                ["reason"] = request.Request.Reason.Trim()
            }), ct);

        return Response<RedactAuditActorResultDto>.Success(new RedactAuditActorResultDto(
            request.Request.ActorId,
            redactedCount,
            redactedAtUtc));
    }
}
