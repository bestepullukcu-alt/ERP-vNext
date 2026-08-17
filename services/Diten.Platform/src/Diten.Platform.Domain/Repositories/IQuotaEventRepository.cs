using Diten.Platform.Domain.Entities;

namespace Diten.Platform.Domain.Repositories;

public interface IQuotaEventRepository
{
    Task<QuotaEvent> CreateAsync(QuotaEvent quotaEvent, CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid tenantId, string quotaKey, string source, string? operationId, string? sourceReference, bool isRejected, CancellationToken ct = default);
}
