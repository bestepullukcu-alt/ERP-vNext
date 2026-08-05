using Diten.ManagementGovernanceService.Application.DecisionRegistry;

namespace Diten.ManagementGovernanceService.Persistence.DecisionRegistry.Transactions;

public sealed record TransactionExecution<T>(T Value, IdempotencyScope Scope, string PayloadHash);

public sealed class DecisionRegistryTransactionRunner(
    IDecisionRegistryTransactionSessionFactory sessions,
    IIdempotencyReceiptReader receipts,
    int maxCommitAttempts = 3)
{
    public async Task<T> ExecuteAsync<T>(Func<IDecisionRegistryTransactionSession, CancellationToken, Task<TransactionExecution<T>>> body, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(body);
        if (maxCommitAttempts < 1) throw new ArgumentOutOfRangeException(nameof(maxCommitAttempts));
        await using var session = await sessions.OpenAsync(cancellationToken);
        await session.BeginAsync(cancellationToken);
        TransactionExecution<T> execution;
        try { execution = await body(session, cancellationToken); }
        catch { await session.AbortAsync(cancellationToken); throw; }

        for (var attempt = 1; attempt <= maxCommitAttempts; attempt++)
        {
            try { await session.CommitAsync(cancellationToken); return execution.Value; }
            catch (UnknownDecisionRegistryCommitException) when (attempt < maxCommitAttempts) { }
            catch (UnknownDecisionRegistryCommitException) when (attempt == maxCommitAttempts) { break; }
            catch { await session.AbortAsync(cancellationToken); throw; }
        }

        var reconciliation = await receipts.ReconcileAsync(execution.Scope, execution.PayloadHash, cancellationToken);
        return reconciliation.Kind switch
        {
            ReceiptReconciliationKind.MatchingCompleted when reconciliation.Receipt is not null => execution.Value,
            ReceiptReconciliationKind.PayloadConflict => throw new DecisionRegistryConflictException("Idempotency payload conflict."),
            _ => throw new DecisionRegistryIndeterminateException("Transaction outcome is indeterminate.")
        };
    }
}

public sealed class NoOpTransactionFaultInjector : ITransactionFaultInjector
{
    public static NoOpTransactionFaultInjector Instance { get; } = new();
    private NoOpTransactionFaultInjector() { }
    public void After(DecisionRegistryFaultPoint point) { }
}
