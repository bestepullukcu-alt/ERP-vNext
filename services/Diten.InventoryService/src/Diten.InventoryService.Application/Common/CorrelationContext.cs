namespace Diten.InventoryService.Application.Common;

public sealed class CorrelationContext : ICorrelationContext
{
    private Guid _correlationId;

    public Guid CorrelationId => _correlationId != Guid.Empty
        ? _correlationId
        : throw new InvalidOperationException("Correlation id is not set. Is the correlation middleware in the pipeline?");

    public void Set(Guid correlationId)
    {
        if (correlationId == Guid.Empty)
        {
            throw new ArgumentException("Correlation id must be non-empty.", nameof(correlationId));
        }

        _correlationId = correlationId;
    }
}
