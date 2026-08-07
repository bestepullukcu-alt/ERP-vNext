using Diten.AuthService.Application.S2S;

namespace Diten.AuthService.Application.Common.Interfaces;

public enum ExplicitRoleGrantAuthorizationDecision { Allowed, Denied, NotReferenceable, Unavailable }

public interface IExplicitRoleGrantProvisioningAuthorizer
{
    Task<ExplicitRoleGrantAuthorizationDecision> AuthorizeAsync(Guid tenantId, Guid authenticatedActorId,
        ExplicitRoleGrantMutationV1 mutation, string trustedProvenance, CancellationToken cancellationToken);
}

public interface IExplicitRoleGrantProvisioningCoordinator
{
    Task<ExplicitRoleGrantProvisioningResult> ExecuteAsync(ExplicitRoleGrantProvisioningV1 request, CancellationToken cancellationToken);
}
