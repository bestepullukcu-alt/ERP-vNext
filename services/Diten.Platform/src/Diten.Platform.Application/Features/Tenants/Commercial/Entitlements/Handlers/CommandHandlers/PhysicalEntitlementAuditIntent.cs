using System.Globalization;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.Tenants.Commercial.Entitlements.Handlers.CommandHandlers;

internal static class PhysicalEntitlementAuditIntent
{
    public const string SourceModule = "subscription-billing";

    /// <summary>
    /// WP-PLATFORM-AUDIT-INTX-01 — the entitlement change, said as an INTENT: the canonical payload (tenant, actor,
    /// masking) is built at the in-transaction writer's entry, not here. The record is owned by the tenant the
    /// change is about (TenantId = TargetTenantId), as the pipeline-era records of these commands are.
    /// </summary>
    public static async Task EnqueueAsync(ITransactionalAuditOutboxWriter writer,
        IPlatformTransactionSession session, Guid tenantId, Guid correlationId, Guid intentId,
        string requestType, AuditOperation operation, Guid? entityId, string moduleCode,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<string, object?>? before = null,
        IReadOnlyDictionary<string, object?>? after = null)
    {
        var inserted = await writer.TryEnqueueAsync(session, new AuditOutboxWriteRequest
        {
            TenantId = tenantId,
            CorrelationId = correlationId,
            IdempotencyKey = $"physical-entitlement:{requestType}:{intentId:N}",
            RequestType = requestType,
            Operation = operation,
            EntityType = "TenantModuleEntitlement",
            EntityId = entityId,
            Intent = new TransactionOwnedAuditIntent
            {
                Category = AuditCategory.SubscriptionBilling,
                TargetTenantId = tenantId,
                BeforeState = before,
                AfterState = after,
                Metadata = new Dictionary<string, object?> { ["ModuleCode"] = moduleCode },
                SourceModule = SourceModule
            }
        }, cancellationToken);
        if (!inserted)
        {
            throw new InvalidOperationException("Transactional physical-entitlement audit intent was not inserted.");
        }
    }

    /// <summary>
    /// What an auditor reads of an entitlement: on/off, until when, where it came from. Never the free-text reason
    /// (it may hold personal data). Dates are ISO-8601 text so they read the same in <c>audit_events</c>.
    /// </summary>
    public static IReadOnlyDictionary<string, object?> StateOf(TenantModuleEntitlement entitlement) =>
        new Dictionary<string, object?>
        {
            ["IsEnabled"] = entitlement.IsEnabled,
            ["ExpiryDateUtc"] = entitlement.ExpiryDateUtc?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            ["Source"] = entitlement.Source.ToString()
        };

    public static IReadOnlyDictionary<string, object?> Removed() =>
        new Dictionary<string, object?> { ["IsDeleted"] = true };
}
