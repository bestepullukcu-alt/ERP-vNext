using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Common.Authorization;
using Diten.Platform.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Infrastructure.Services.Audit;

public sealed class PlatformEntitlementAuditSink : IEntitlementAuditSink
{
    private const string ModuleAccessEntityType = "ModuleAccess";
    private const string FeatureAccessEntityType = "FeatureAccess";

    private readonly IAuditService auditService;
    private readonly ILogger<PlatformEntitlementAuditSink> logger;

    public PlatformEntitlementAuditSink(
        IAuditService auditService,
        ILogger<PlatformEntitlementAuditSink> logger)
    {
        this.auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task LogDeniedAsync(EntitlementAuditDenyContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            await auditService.AppendAsync(MapRequest(context), cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Entitlement deny audit append failed. Kind={EntitlementKind} Code={Code} DenyReason={DenyReason}",
                context.EntitlementKind,
                context.Code,
                context.DenyReason);
        }
    }

    private static AuditAppendRequest MapRequest(EntitlementAuditDenyContext context)
    {
        var entityType = context.EntitlementKind == EntitlementKind.Module
            ? ModuleAccessEntityType
            : FeatureAccessEntityType;

        return new AuditAppendRequest
        {
            CorrelationId = Guid.NewGuid(),
            RequestType = "EntitlementAccessDenied",
            ActorType = MapActorType(context.ActorType),
            TargetTenantId = context.TenantId,
            Category = AuditCategory.Security,
            EntityType = entityType,
            Operation = AuditOperation.PermissionDenied,
            Outcome = AuditOutcome.Denied,
            SourceService = "Diten.Platform",
            SourceModule = "MOD-0018",
            Metadata = new Dictionary<string, object?>
            {
                ["tenantId"] = context.TenantId,
                ["entitlementKind"] = context.EntitlementKind.ToString(),
                ["code"] = context.Code,
                ["denyReason"] = context.DenyReason?.ToString(),
                ["isTransientFailure"] = context.IsTransientFailure,
                ["source"] = context.Source,
                ["requirementName"] = context.RequirementName
            }
        };
    }

    private static AuditActorType MapActorType(string? actorType)
    {
        return actorType?.Trim().ToLowerInvariant() switch
        {
            "platform_admin" => AuditActorType.PlatformAdministrator,
            "partner_admin" => AuditActorType.PartnerAdministrator,
            "tenant_user" => AuditActorType.TenantUser,
            "system" => AuditActorType.System,
            "service" => AuditActorType.Service,
            _ => AuditActorType.System
        };
    }
}
