namespace Diten.InventoryService.Application.Common;

/// <summary>
/// Request correlation UUID: the caller's <c>X-Correlation-Id</c> when it is a valid UUID, otherwise server-minted.
/// Echoed in the response header, in <c>error.correlationId</c>, and persisted on the transaction, audit record and
/// outbox event (pack §8, AC12).
/// </summary>
public interface ICorrelationContext
{
    Guid CorrelationId { get; }

    void Set(Guid correlationId);
}
