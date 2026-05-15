using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Entities.Audit;
using Diten.Platform.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Features.Audit.Services;

public interface IAuditMetaAuditWriter
{
    Task<AuditAppendResult> WriteAsync(AuditMetaAuditRequest request, CancellationToken ct = default);
}

public sealed record AuditMetaAuditRequest(
    string RequestType,
    AuditCategory Category,
    AuditOperation Operation,
    AuditOutcome Outcome,
    string EntityType,
    Guid? EntityId,
    Guid? TargetTenantId,
    IReadOnlyDictionary<string, object?> Metadata);

public sealed class AuditMetaAuditWriter : IAuditMetaAuditWriter
{
    private readonly IAuditService _auditService;
    private readonly IAuditRecursionGuard _recursionGuard;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly ILogger<AuditMetaAuditWriter> _logger;

    public AuditMetaAuditWriter(
        IAuditService auditService,
        IAuditRecursionGuard recursionGuard,
        ICurrentUserContext currentUserContext,
        ILogger<AuditMetaAuditWriter> logger)
    {
        _auditService = auditService;
        _recursionGuard = recursionGuard;
        _currentUserContext = currentUserContext;
        _logger = logger;
    }

    public async Task<AuditAppendResult> WriteAsync(AuditMetaAuditRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            using var _ = _recursionGuard.BeginScope();
            return await _auditService.AppendAsync(new AuditAppendRequest
            {
                CorrelationId = Guid.NewGuid(),
                RequestType = request.RequestType,
                ActorType = _currentUserContext.IsAuthenticated
                    ? AuditActorType.PlatformAdministrator
                    : AuditActorType.System,
                ActorId = _currentUserContext.UserId == Guid.Empty ? null : _currentUserContext.UserId,
                ActorEmail = _currentUserContext.Email,
                ActorDisplayName = _currentUserContext.DisplayName ?? _currentUserContext.ActorName,
                TargetTenantId = request.TargetTenantId is { } targetTenantId && targetTenantId != Guid.Empty
                    ? targetTenantId
                    : AuditTenantIds.PlatformSystemTenantId,
                Category = request.Category,
                EntityType = request.EntityType,
                EntityId = request.EntityId,
                Operation = request.Operation,
                Outcome = request.Outcome,
                Metadata = request.Metadata,
                SourceService = "Diten.Platform",
                SourceModule = "Audit",
                IsMetaAudit = true,
                IsPlatformGlobal = true
            }, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                "Audit meta-audit write failed. RequestType={RequestType} ErrorType={ErrorType}",
                request.RequestType,
                ex.GetType().Name);

            return AuditAppendResult.EnqueueFailed(null, $"Audit meta-audit write failed. ErrorType={ex.GetType().Name}");
        }
    }
}
