namespace Diten.PvgService.Application.RegPvBase;

public interface IPvgFieldSecurityPolicy
{
    ValueTask<PvgPortDecision> EvaluateAsync(
        PvgFieldSecurityRequest request,
        CancellationToken cancellationToken = default);
}
