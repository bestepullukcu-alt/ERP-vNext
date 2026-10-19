using Diten.Platform.API.Models.AccessGovernance;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Security;

public interface ITrustedLegalEntityScopeRequestExecutor
{
    Task<IActionResult> ExecuteAsync(
        HttpContext context,
        CancellationToken cancellationToken,
        Func<Guid, Guid, TrustedLegalEntityScopeResolveRequest, CancellationToken, Task<IActionResult>> action,
        Func<int, string, IActionResult> failure);
}
