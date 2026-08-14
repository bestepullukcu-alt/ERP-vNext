using Diten.BuildingBlocks.Eventing;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Contracts.Eventing;

/// <summary>
/// Writes an outbox intent as a participant of an explicitly supplied Platform transaction.
/// Authoritative mutations must use this seam instead of the non-transactional event-bus writer.
/// </summary>
public interface ITransactionalOutboxEventWriter
{
    Task<EventOutboxWriteResult> EnqueueAsync(
        IPlatformTransactionSession session,
        EventOutboxWriteRequest request,
        CancellationToken cancellationToken = default);
}
