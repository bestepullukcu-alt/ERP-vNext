using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Infrastructure.Persistence;

/// <summary>Test-controllable boundary probe; production composition uses the no-op implementation.</summary>
public interface IPlatformTransactionFaultProbe
{
    Task BeforeCommitAsync(IPlatformTransactionSession session, int commitAttempt, CancellationToken ct);
    Task AfterCommitAsync(IPlatformTransactionSession session, int commitAttempt, CancellationToken ct);
}

public sealed class NoOpPlatformTransactionFaultProbe : IPlatformTransactionFaultProbe
{
    public Task BeforeCommitAsync(IPlatformTransactionSession session, int commitAttempt, CancellationToken ct) => Task.CompletedTask;
    public Task AfterCommitAsync(IPlatformTransactionSession session, int commitAttempt, CancellationToken ct) => Task.CompletedTask;
}
