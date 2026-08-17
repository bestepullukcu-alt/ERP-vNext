namespace Diten.Platform.Application.Contracts.Audit;

public interface IAuditOutboxWriter
{
    Task<bool> TryEnqueueAsync(AuditOutboxWriteRequest request, CancellationToken ct = default);
}
