using Diten.AuthService.Application.S2S;

namespace Diten.AuthService.Application.Common.Interfaces;

public interface IS2STrustedValidationKeyProvider
{
    Task<S2SKeyResolution<S2STrustedValidationKeyDescriptor>> ResolveAsync(string issuer, string kid, CancellationToken cancellationToken);
}
