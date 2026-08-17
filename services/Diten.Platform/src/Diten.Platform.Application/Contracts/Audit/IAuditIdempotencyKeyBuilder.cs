using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Application.Contracts.Audit;

public interface IAuditIdempotencyKeyBuilder
{
    string Build(Guid correlationId, string requestType, string entityType, Guid? entityId, AuditOperation operation, int sequence = 0);
}
