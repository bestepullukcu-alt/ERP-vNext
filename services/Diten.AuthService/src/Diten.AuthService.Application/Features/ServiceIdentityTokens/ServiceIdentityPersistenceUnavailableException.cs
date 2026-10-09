namespace Diten.AuthService.Application.Features.ServiceIdentityTokens;

public sealed class ServiceIdentityPersistenceUnavailableException : Exception
{
    public ServiceIdentityPersistenceUnavailableException(Exception innerException)
        : base("Service identity persistence is unavailable.", innerException) { }
}
