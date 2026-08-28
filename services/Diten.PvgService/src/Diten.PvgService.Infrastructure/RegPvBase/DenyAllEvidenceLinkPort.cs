using Diten.PvgService.Application.RegPvBase;

namespace Diten.PvgService.Infrastructure.RegPvBase;

public sealed class DenyAllEvidenceLinkPort : IPvgEvidenceLinkPort
{
    public ValueTask<PvgPortDecision> EvaluateAsync(
        PvgEvidenceLinkRequest request,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(PvgPortDecision.EvidenceLinkDenied());
}
