using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Features.DocumentRepository.Queries;
using Diten.Platform.Application.Features.DocumentRepository.Services;
using Diten.Platform.Common.Authorization;
using Diten.Platform.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Features.DocumentRepository.Handlers.QueryHandlers;

/// <summary>
/// MOD-0262-FU01 DL-2 — trusted consumer content read, AUDITED (§21 "Okuma satırı"): a machine identity taking content
/// out is the only trace on the Platform side (D56/2). Queries are not audited by <c>AuditBehavior</c>, so this handler
/// appends one central record itself once the stream is open and before the first byte leaves (<c>Operation = Export</c>,
/// metadata <c>delivery = started</c>). A stream cut later does not change the record (append-only); the cut is a
/// warning log at the caller. A record that cannot be written does not stop the read (§13 "denetim kaydı yazılamadı").
/// </summary>
public sealed class OpenTrustedRepositoryObjectContentHandler
    : IRequestHandler<OpenTrustedRepositoryObjectContentQuery, Response<RepositoryObjectContentHandle>>
{
    public const string ContentReadRequestType = "document-repository.trusted-consumer.content-read";

    private readonly DocumentRepositoryService _service;
    private readonly IAuditService _audit;
    private readonly ITenantAuthorizationContext _principal;
    private readonly ILogger<OpenTrustedRepositoryObjectContentHandler> _logger;

    public OpenTrustedRepositoryObjectContentHandler(
        DocumentRepositoryService service,
        IAuditService audit,
        ITenantAuthorizationContext principal,
        ILogger<OpenTrustedRepositoryObjectContentHandler> logger)
    {
        _service = service;
        _audit = audit;
        _principal = principal;
        _logger = logger;
    }

    public async Task<Response<RepositoryObjectContentHandle>> Handle(
        OpenTrustedRepositoryObjectContentQuery request, CancellationToken cancellationToken)
    {
        var opened = await _service.OpenReadForTrustedConsumerAsync(
            request.ContentId, request.Consumer, request.CorrelationId, cancellationToken);
        if (!opened.IsSuccessful || opened.Data is null)
        {
            return Response<RepositoryObjectContentHandle>.Fail(
                opened.Errors, opened.StatusCode, opened.ReasonCode, request.CorrelationId);
        }

        var content = opened.Data;
        var metadata = TrustedRepositoryAuditMetadata.For(request.Consumer, content.Object.Scope, "content-read");
        metadata["byteSize"] = content.Object.ByteSize;
        metadata["delivery"] = "started";

        try
        {
            var appended = await _audit.AppendAsync(new AuditAppendRequest
            {
                CorrelationId = request.AuditCorrelationId,
                RequestType = ContentReadRequestType,
                ActorType = AuditActorTypeResolver.ForCommand(_principal),
                ActorId = request.Consumer.ClientId,
                ActorEmail = string.Empty,
                ActorDisplayName = string.Empty,
                Category = AuditCategory.DocumentManagement,
                EntityType = "RepositoryObject",
                EntityId = content.Object.ContentId,
                Operation = AuditOperation.Export,
                Outcome = AuditOutcome.Succeeded,
                Metadata = metadata,
                SourceService = "Diten.Platform",
                SourceModule = "MOD-0262-FU01"
            }, cancellationToken);

            if (appended.Status != AuditAppendStatus.Queued)
            {
                _logger.LogWarning(
                    "Trusted consumer content read of {ContentId} was not recorded (status {Status}); the read continues.",
                    content.Object.ContentId, appended.Status);
            }
        }
        catch (OperationCanceledException)
        {
            await content.Content.Content.DisposeAsync();
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Trusted consumer content read of {ContentId} could not be recorded; the read continues.",
                content.Object.ContentId);
        }

        return Response<RepositoryObjectContentHandle>.Success(content.Content, correlationId: request.CorrelationId);
    }
}
