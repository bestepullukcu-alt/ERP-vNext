namespace Diten.Platform.Application.Contracts.Eventing;

public interface IEventingObservabilitySink
{
    Task OnEventConsumedAsync(
        string eventName,
        string eventVersion,
        string? consumerName,
        string result,
        TimeSpan duration,
        string? correlationId,
        CancellationToken cancellationToken = default);
}
