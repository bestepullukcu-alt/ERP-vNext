namespace Diten.Platform.Application.Contracts.Audit;

public interface IAuditService
{
    Task<AuditAppendResult> AppendAsync(AuditAppendRequest request, CancellationToken ct = default);
}
