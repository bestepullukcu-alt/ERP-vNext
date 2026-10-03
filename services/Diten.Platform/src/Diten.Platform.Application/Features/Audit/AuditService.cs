using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Audit;
using Diten.Platform.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Features.Audit;

public sealed class AuditService : IAuditService
{
    private const int MaxRequestTypeLength = 240;
    private const int MaxEntityTypeLength = 160;
    private readonly IAuditOutboxWriter _outboxWriter;
    private readonly ISensitiveFieldRedactor _redactor;
    private readonly IAuditIdempotencyKeyBuilder _idempotencyKeyBuilder;
    private readonly IAuditRecursionGuard _recursionGuard;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly ILogger<AuditService> _logger;
    private readonly Diten.Platform.Common.Observability.ICorrelationContext? _correlation;

    public AuditService(
        IAuditOutboxWriter outboxWriter,
        ISensitiveFieldRedactor redactor,
        IAuditIdempotencyKeyBuilder idempotencyKeyBuilder,
        IAuditRecursionGuard recursionGuard,
        ITenantContext tenantContext,
        ICurrentUserContext currentUserContext,
        ILogger<AuditService> logger,
        Diten.Platform.Common.Observability.ICorrelationContext? correlation = null)
    {
        _correlation = correlation;
        _outboxWriter = outboxWriter;
        _redactor = redactor;
        _idempotencyKeyBuilder = idempotencyKeyBuilder;
        _recursionGuard = recursionGuard;
        _tenantContext = tenantContext;
        _currentUserContext = currentUserContext;
        _logger = logger;
    }

    public async Task<AuditAppendResult> AppendAsync(AuditAppendRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validationError = ValidateRequest(request);
        if (validationError is not null)
        {
            return AuditAppendResult.Rejected(validationError);
        }

        if (_recursionGuard.IsActive && !request.IsMetaAudit)
        {
            return AuditAppendResult.SkippedRecursion("Audit append skipped because an audit recursion guard scope is active.");
        }

        var tenantResolution = ResolveTenantId(request);
        if (!tenantResolution.IsResolved)
        {
            return AuditAppendResult.Rejected(tenantResolution.Error!);
        }

        var targetTenantId = ResolveTargetTenantId(request);
        if (targetTenantId == Guid.Empty)
        {
            return AuditAppendResult.Rejected("Audit target tenant id cannot be empty.");
        }

        var idempotencyKey = _idempotencyKeyBuilder.Build(
            request.CorrelationId,
            request.RequestType,
            request.EntityType,
            request.EntityId,
            request.Operation,
            request.Sequence);

        // The record carries the REQUEST's correlation (AuditCorrelation); the idempotency key above keeps the caller's.
        var recordCorrelation = AuditCorrelation.Resolve(_correlation?.CorrelationId, request.CorrelationId);
        // INTX FIX2 — what the CLIENT gave as its correlation travels as metadata only. FIX3 — and only from the header the
        // gateway set: a "ClientCorrelation" a caller wrote into the body is dropped, whether or not a header came.
        var metadata = new Dictionary<string, object?>(request.Metadata);
        metadata.Remove(AuditCorrelation.ClientCorrelationMetadataKey);
        if (AuditCorrelation.ClientValue(_correlation?.ClientCorrelationId) is { } clientCorrelation)
        {
            metadata[AuditCorrelation.ClientCorrelationMetadataKey] = clientCorrelation;
        }
        request = request with { Metadata = metadata };
        var writeRequest = new AuditOutboxWriteRequest
        {
            TenantId = tenantResolution.TenantId,
            CorrelationId = recordCorrelation,
            IdempotencyKey = idempotencyKey,
            RequestType = request.RequestType.Trim(),
            Operation = request.Operation,
            EntityType = request.EntityType.Trim(),
            EntityId = request.EntityId,
            Payload = BuildPayload(request, tenantResolution.TenantId, targetTenantId, recordCorrelation)
        };

        try
        {
            var enqueued = await _outboxWriter.TryEnqueueAsync(writeRequest, ct);
            return enqueued
                ? AuditAppendResult.Queued(idempotencyKey)
                : AuditAppendResult.Duplicate(idempotencyKey);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                "Audit enqueue failed for {RequestType} with correlation id {CorrelationId}. ErrorType={ErrorType}",
                request.RequestType,
                request.CorrelationId,
                ex.GetType().Name);

            return AuditAppendResult.EnqueueFailed(idempotencyKey, $"Audit enqueue failed. ErrorType={ex.GetType().Name}");
        }
    }

    // The payload itself is built by AuditOutboxPayload — the one builder both audit doors use. What stays here is
    // the central door's own choice of actor: the request's value, else the current user's.
    private IReadOnlyDictionary<string, object?> BuildPayload(AuditAppendRequest request, Guid tenantId, Guid? targetTenantId, Guid correlationId)
    {
        return AuditOutboxPayload.Build(new AuditCanonicalRecord(
            TenantId: tenantId,
            CorrelationId: correlationId,
            RequestType: request.RequestType,
            ActorType: request.ActorType,
            ActorId: request.ActorId ?? (_currentUserContext.UserId == Guid.Empty ? null : _currentUserContext.UserId),
            ActorEmail: request.ActorEmail ?? _currentUserContext.Email,
            ActorDisplayName: request.ActorDisplayName ?? _currentUserContext.DisplayName ?? _currentUserContext.ActorName,
            TargetTenantId: targetTenantId,
            Category: request.Category,
            EntityType: request.EntityType,
            EntityId: request.EntityId,
            Operation: request.Operation,
            Outcome: request.Outcome,
            BeforeState: request.BeforeState,
            AfterState: request.AfterState,
            Metadata: request.Metadata,
            IpAddress: request.IpAddress,
            UserAgent: request.UserAgent,
            OccurredAtUtc: request.OccurredAtUtc,
            SourceService: request.SourceService,
            SourceModule: request.SourceModule,
            IsMetaAudit: request.IsMetaAudit), _redactor);
    }

    private (bool IsResolved, Guid TenantId, string? Error) ResolveTenantId(AuditAppendRequest request)
    {
        if (request.IsPlatformGlobal)
        {
            return (true, AuditTenantIds.PlatformSystemTenantId, null);
        }

        if (!_tenantContext.IsResolved)
        {
            return (false, Guid.Empty, "Audit tenant context is not resolved.");
        }

        var tenantId = _tenantContext.TenantId;

        // FIX-AUDIT-TARGET-EMPTY — platform-admin requests are pinned by TenantResolutionMiddleware with
        // SetPlatformContext(Guid.Empty), so the context alone cannot own a tenant-scoped audit event. A command
        // that self-declares its target tenant (TargetTenantId) owns the event under that tenant.
        if (tenantId == Guid.Empty
            && _tenantContext.IsPlatformContext
            && request.TargetTenantId is { } declaredTarget
            && declaredTarget != Guid.Empty)
        {
            return (true, declaredTarget, null);
        }

        return tenantId == Guid.Empty
            ? (false, Guid.Empty, "Audit tenant id cannot be empty. Use PlatformSystemTenantId for platform-global audit events.")
            : (true, tenantId, null);
    }

    private Guid? ResolveTargetTenantId(AuditAppendRequest request)
    {
        if (request.TargetTenantId.HasValue)
        {
            return request.TargetTenantId; // explicit Guid.Empty is still rejected by AppendAsync (intentional misuse guard)
        }

        if (_tenantContext.IsResolved && _tenantContext.IsPlatformContext)
        {
            // FIX-AUDIT-TARGET-EMPTY — TenantResolutionMiddleware pins platform-admin requests with
            // SetPlatformContext(Guid.Empty); that sentinel means "no specific target tenant", so normalize
            // it to null instead of letting the Guid.Empty guard reject the append.
            var contextTarget = _tenantContext.TargetTenantId;
            return contextTarget == Guid.Empty ? null : contextTarget;
        }

        return null;
    }

    private static string? ValidateRequest(AuditAppendRequest request)
    {
        if (request.CorrelationId == Guid.Empty)
        {
            return "Audit correlation id is required.";
        }

        if (string.IsNullOrWhiteSpace(request.RequestType))
        {
            return "Audit request type is required.";
        }

        if (request.RequestType.Length > MaxRequestTypeLength)
        {
            return $"Audit request type cannot exceed {MaxRequestTypeLength} characters.";
        }

        if (request.ActorType == AuditActorType.Unknown)
        {
            return "Audit actor type is required.";
        }

        if (request.Category == AuditCategory.Unknown)
        {
            return "Audit category is required.";
        }

        if (string.IsNullOrWhiteSpace(request.EntityType))
        {
            return "Audit entity type is required.";
        }

        if (request.EntityType.Length > MaxEntityTypeLength)
        {
            return $"Audit entity type cannot exceed {MaxEntityTypeLength} characters.";
        }

        if (request.Operation == AuditOperation.Unknown)
        {
            return "Audit operation is required.";
        }

        if (request.Outcome == AuditOutcome.Unknown)
        {
            return "Audit outcome is required.";
        }

        if (string.IsNullOrWhiteSpace(request.SourceService))
        {
            return "Audit source service is required.";
        }

        if (request.Sequence < 0)
        {
            return "Audit sequence cannot be negative.";
        }

        return null;
    }


}
