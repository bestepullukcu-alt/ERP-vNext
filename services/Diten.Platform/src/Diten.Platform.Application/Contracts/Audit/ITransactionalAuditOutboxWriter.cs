using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Contracts.Audit;

public interface ITransactionalAuditOutboxWriter
{
    Task<bool> TryEnqueueAsync(
        IPlatformTransactionSession session,
        AuditOutboxWriteRequest request,
        CancellationToken ct = default);
}
