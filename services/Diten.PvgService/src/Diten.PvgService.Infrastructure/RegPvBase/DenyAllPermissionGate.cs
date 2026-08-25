using Diten.PvgService.Application.RegPvBase;

namespace Diten.PvgService.Infrastructure.RegPvBase;

public sealed class DenyAllPermissionGate : IPvgPermissionGate
{
    public ValueTask<PvgPermissionDecision> EvaluateAsync(
        PvgPermissionRequest request,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(PvgPermissionDecision.Denied("PVG_PERMISSION_DENIED"));
}
