namespace Diten.Platform.Common.Observability;

public interface ICorrelationContext
{
    string? CorrelationId { get; }

    void SetCorrelationId(string correlationId);

    /// <summary>
    /// INTX FIX2 — the correlation value the CLIENT sent (safe characters, bounded length), when it sent one. Never the
    /// request's correlation: an audit record may carry it only as what the client said.
    /// </summary>
    string? ClientCorrelationId => null;

    void SetClientCorrelationId(string clientCorrelationId) { }
}
