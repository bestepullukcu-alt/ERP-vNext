using Diten.PvgService.Application.RegPvBase;

namespace Diten.PvgService.Infrastructure.RegPvBase;

public sealed class DenyAllFieldSecurityPolicy : IPvgFieldSecurityPolicy
{
    public ValueTask<PvgPortDecision> EvaluateAsync(
        PvgFieldSecurityRequest request,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(PvgPortDecision.FieldSecurityDenied());
}
