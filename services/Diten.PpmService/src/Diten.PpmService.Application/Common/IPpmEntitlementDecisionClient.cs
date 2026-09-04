using Diten.Shared.Core;

namespace Diten.PpmService.Application.Common;


public interface IPpmEntitlementDecisionClient
{
    Task<bool> IsAllowedAsync(Guid tenantId, CancellationToken cancellationToken);
}
