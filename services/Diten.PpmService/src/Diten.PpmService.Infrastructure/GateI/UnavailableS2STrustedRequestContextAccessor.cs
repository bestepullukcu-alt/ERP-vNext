using Diten.PpmService.Application.GateI;

namespace Diten.PpmService.Infrastructure.GateI;

internal sealed class UnavailableS2STrustedRequestContextAccessor : IS2STrustedRequestContextAccessor
{
    public S2STrustedRequestContext? Current => null;

    public void Publish(S2STrustedRequestContext context) =>
        throw new InvalidOperationException("S2S trusted request context is not composed.");
}
