using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Contracts.Audit;

/// <summary>
/// MOD-0040-FU02 (K-10) — a REFUSED request's audit record, written in its own short transaction (a refusal is not a
/// business transaction). Same door as <see cref="ITransactionalAuditOutboxWriter"/>: the same checks, the same actor,
/// the same source-service rule, the same payload builder; only the outcome is the refusal's. <paramref name="outcome"/>
/// is <c>Denied</c> or <c>Failed</c>; <c>Succeeded</c> is an invalid intent.
/// </summary>
public interface ITransactionalAuditRefusalWriter
{
    Task<bool> TryEnqueueRefusalAsync(
        IPlatformTransactionSession session,
        AuditOutboxWriteRequest request,
        AuditOutcome outcome,
        CancellationToken ct = default);
}
