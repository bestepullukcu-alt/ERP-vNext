namespace Diten.Platform.Common.Observability;

public sealed class CorrelationContext : ICorrelationContext
{
    public string? CorrelationId { get; private set; }

    public void SetCorrelationId(string correlationId)
    {
        CorrelationId = correlationId;
    }
}
