using Diten.AuthService.Domain.Entities;

namespace Diten.AuthService.Application.Common.Interfaces;

public interface IServiceClientCredentialVerifier
{
    bool Verify(ServiceClientIdentity identity, string presentedSecret, DateTimeOffset nowUtc);
    bool IsStateCoherent(ServiceClientIdentity identity);
    void PerformUnknownClientWork(string presentedSecret);
    string Hash(string secret);
}
