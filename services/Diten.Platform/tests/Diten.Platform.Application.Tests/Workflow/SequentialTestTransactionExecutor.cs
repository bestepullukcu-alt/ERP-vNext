using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Tests.Workflow;

/// <summary>
/// FIX3 item 2 — THE SEQUENTIAL FALLBACK, AND ONLY IN TESTS, BY NAME. The start handler no longer runs without a transaction
/// executor; in-memory doubles have no transactions, so their tests hand it this one: it runs the body once, and the doubles'
/// session overloads fall back to their plain writes. Never registered in production.
/// </summary>
internal sealed class SequentialTestTransactionExecutor : IPlatformTransactionExecutor
{
    public Task<T> ExecuteAsync<T>(
        Func<IPlatformTransactionSession, CancellationToken, Task<T>> body,
        CancellationToken cancellationToken = default) =>
        body(new NoSession(), cancellationToken);

    private sealed class NoSession : IPlatformTransactionSession
    {
        public Guid TransactionId { get; } = Guid.NewGuid();
    }
}
