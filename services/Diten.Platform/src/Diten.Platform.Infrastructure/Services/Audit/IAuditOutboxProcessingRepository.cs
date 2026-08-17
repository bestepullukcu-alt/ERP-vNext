using Diten.Platform.Infrastructure.Persistence.Models;

namespace Diten.Platform.Infrastructure.Services.Audit;

public interface IAuditOutboxProcessingRepository
{
    Task<IReadOnlyList<AuditOutboxProcessingItem>> ClaimNextBatchAsync(
        int batchSize,
        int maxAttempts,
        DateTimeOffset now,
        TimeSpan processingStaleAfter,
        CancellationToken ct = default);

    Task MarkCompletedAsync(Guid id, CancellationToken ct = default);

    Task MarkFailedAsync(
        Guid id,
        AuditOutboxStatus status,
        int attempts,
        DateTimeOffset nextAttemptAtUtc,
        string lastError,
        CancellationToken ct = default);
}
