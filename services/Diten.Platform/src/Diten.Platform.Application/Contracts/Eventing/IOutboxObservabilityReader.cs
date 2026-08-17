namespace Diten.Platform.Application.Contracts.Eventing;

public interface IOutboxObservabilityReader
{
    Task<long> GetPendingCountAsync(CancellationToken cancellationToken = default);
}
