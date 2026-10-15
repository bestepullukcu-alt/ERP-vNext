namespace Diten.Platform.Common.Observability;

public sealed class CorrelationContext : ICorrelationContext
{
    public string? CorrelationId { get; private set; }

    public string? ClientCorrelationId { get; private set; }

    public void SetCorrelationId(string correlationId)
    {
        CorrelationId = correlationId;
    }

    public void SetClientCorrelationId(string clientCorrelationId)
    {
        ClientCorrelationId = clientCorrelationId;
    }
}
