namespace Diten.PvgService.Application.RegPvBase;

public interface IPvgEvidenceLinkPort
{
    ValueTask<PvgPortDecision> EvaluateAsync(
        PvgEvidenceLinkRequest request,
        CancellationToken cancellationToken = default);
}
