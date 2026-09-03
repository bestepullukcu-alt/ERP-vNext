namespace Diten.AuthService.Application.Common.Interfaces;

public sealed record ServiceClientOperationalActor(Guid UserId, Guid TenantId, string ActorType);

public interface IServiceClientOperationalActorAuthorizer
{
    Task<ServiceClientOperationalActor> AuthorizeAsync(
        string accessToken,
        string operationalMarker,
        CancellationToken cancellationToken);
}
