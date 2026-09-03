using Diten.AuthService.Application.Features.ServiceIdentityTokens.Operational;

namespace Diten.AuthService.Application.Common.Interfaces;

public interface IServiceClientOperationalProvisioningEligibility
{
    void EnsureEligible(ServiceClientOperationalProvisioningRequest request);
}
