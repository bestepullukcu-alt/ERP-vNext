namespace Diten.PvgService.Application.RegPvBase;

public interface IPvgPermissionGate
{
    ValueTask<PvgPermissionDecision> EvaluateAsync(
        PvgPermissionRequest request,
        CancellationToken cancellationToken = default);
}
