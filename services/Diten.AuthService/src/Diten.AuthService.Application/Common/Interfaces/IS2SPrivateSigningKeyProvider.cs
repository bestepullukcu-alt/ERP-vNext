using Diten.AuthService.Application.S2S;

namespace Diten.AuthService.Application.Common.Interfaces;

public interface IS2SPrivateSigningKeyProvider
{
    Task<S2SKeyResolution<S2SPrivateSigningKeyDescriptor>> ResolveAsync(string issuer, string kid, CancellationToken cancellationToken);
}
