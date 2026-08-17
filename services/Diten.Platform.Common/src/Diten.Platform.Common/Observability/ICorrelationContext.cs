namespace Diten.Platform.Common.Observability;

public interface ICorrelationContext
{
    string? CorrelationId { get; }

    void SetCorrelationId(string correlationId);
}
