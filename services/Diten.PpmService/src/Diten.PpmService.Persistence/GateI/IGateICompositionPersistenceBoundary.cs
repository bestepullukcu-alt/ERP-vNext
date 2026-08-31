namespace Diten.PpmService.Persistence.GateI;


public interface IGateICompositionPersistenceBoundary
{
    GateICompositionResidue Snapshot();
    ValueTask<int> RejectUnavailableAsync(CancellationToken cancellationToken = default);
}

internal sealed class GateICompositionPersistenceBoundary : IGateICompositionPersistenceBoundary
{
    public GateICompositionResidue Snapshot() => new(0, 0, 0, 0);

    public ValueTask<int> RejectUnavailableAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(503);
    }
}
