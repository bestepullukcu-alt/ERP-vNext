using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.Tenants.Commercial.Entitlements.Handlers.CommandHandlers;

internal static class PhysicalEntitlementAuditIntent
{
    public static async Task EnqueueAsync(ITransactionalAuditOutboxWriter writer,
        IPlatformTransactionSession session, Guid tenantId, Guid correlationId, Guid intentId,
        string requestType, AuditOperation operation, Guid? entityId, string moduleCode,
        CancellationToken cancellationToken)
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
            Payload = new Dictionary<string, object?>
            {
                ["ModuleCode"] = moduleCode,
                ["Outcome"] = "Succeeded"
            }
        }, cancellationToken);
        if (!inserted)
        {
            throw new InvalidOperationException("Transactional physical-entitlement audit intent was not inserted.");
        }
    }
}
