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

    public AuditService(
        IAuditOutboxWriter outboxWriter,
        ISensitiveFieldRedactor redactor,
        IAuditIdempotencyKeyBuilder idempotencyKeyBuilder,
        IAuditRecursionGuard recursionGuard,
        ITenantContext tenantContext,
        ICurrentUserContext currentUserContext,
        ILogger<AuditService> logger)
    {
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

        var writeRequest = new AuditOutboxWriteRequest
        {
            TenantId = tenantResolution.TenantId,
            CorrelationId = request.CorrelationId,
            IdempotencyKey = idempotencyKey,
            RequestType = request.RequestType.Trim(),
            Operation = request.Operation,
            EntityType = request.EntityType.Trim(),
            EntityId = request.EntityId,
            Payload = BuildPayload(request, tenantResolution.TenantId, targetTenantId)
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

    private IReadOnlyDictionary<string, object?> BuildPayload(AuditAppendRequest request, Guid tenantId, Guid? targetTenantId)
    {
        var beforeState = request.BeforeState is null ? null : _redactor.RedactDictionary(request.BeforeState);
        var afterState = request.AfterState is null ? null : _redactor.RedactDictionary(request.AfterState);
        var metadata = _redactor.RedactDictionary(request.Metadata);
        var actorEmail = request.ActorEmail ?? _currentUserContext.Email;
        var actorDisplayName = request.ActorDisplayName ?? _currentUserContext.DisplayName ?? _currentUserContext.ActorName;
        var actorId = request.ActorId ?? (_currentUserContext.UserId == Guid.Empty ? null : _currentUserContext.UserId);

        return new Dictionary<string, object?>
        {
            ["TenantId"] = tenantId,
            ["CorrelationId"] = request.CorrelationId,
            ["RequestType"] = request.RequestType.Trim(),
            ["ActorType"] = request.ActorType.ToString(),
            ["ActorId"] = actorId,
            ["ActorEmailMasked"] = MaskEmail(actorEmail),
            ["ActorDisplayNameMasked"] = MaskDisplayName(actorDisplayName),
            ["TargetTenantId"] = targetTenantId,
            ["Category"] = request.Category.ToString(),
            ["EntityType"] = request.EntityType.Trim(),
            ["EntityId"] = request.EntityId,
            ["Operation"] = request.Operation.ToString(),
            ["Outcome"] = request.Outcome.ToString(),
            ["BeforeState"] = beforeState,
            ["AfterState"] = afterState,
            ["Metadata"] = metadata,
            ["IpAddressMasked"] = MaskIpAddress(request.IpAddress),
            ["UserAgent"] = request.UserAgent,
            ["OccurredAtUtc"] = request.OccurredAtUtc ?? DateTimeOffset.UtcNow,
            ["SourceService"] = request.SourceService.Trim(),
            ["SourceModule"] = request.SourceModule,
            ["IsMetaAudit"] = request.IsMetaAudit,
            ["RedactionStatus"] = AuditRedactionStatus.SensitiveFieldsRedacted.ToString()
        };
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

    private static string? MaskEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var trimmed = email.Trim();
        var atIndex = trimmed.IndexOf('@', StringComparison.Ordinal);
        if (atIndex <= 0)
        {
            return MaskDisplayName(trimmed);
        }

        var local = trimmed[..atIndex];
        var domain = trimmed[(atIndex + 1)..];
        return $"{local[0]}***@{domain}";
    }

    private static string? MaskDisplayName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length == 1)
        {
            return "*";
        }

        return trimmed.Length == 2
            ? $"{trimmed[0]}*"
            : $"{trimmed[0]}***{trimmed[^1]}";
    }

    private static string? MaskIpAddress(string? ipAddress)
    {
        if (string.IsNullOrWhiteSpace(ipAddress))
        {
            return null;
        }

        var trimmed = ipAddress.Trim();
        var ipv4Parts = trimmed.Split('.');
        if (ipv4Parts.Length == 4)
        {
            return $"{ipv4Parts[0]}.{ipv4Parts[1]}.{ipv4Parts[2]}.0";
        }

        var colonIndex = trimmed.IndexOf(':', StringComparison.Ordinal);
        return colonIndex > 0 ? $"{trimmed[..colonIndex]}:****" : "[REDACTED]";
    }
}
