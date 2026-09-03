using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Security;

public interface IPlatformTenantWorkflowDefinitionRequestExecutor
{
    Task<IActionResult> ExecuteAsync(
        HttpContext context,
        Guid targetTenantId,
        CancellationToken cancellationToken,
        Func<CancellationToken, Task<IActionResult>> action,
        Func<int, string, IActionResult> failure);
}
