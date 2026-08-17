namespace Diten.Platform.API.Observability;

public interface ICorrelationContext
{
    string? CorrelationId { get; }

    void SetCorrelationId(string correlationId);
}
