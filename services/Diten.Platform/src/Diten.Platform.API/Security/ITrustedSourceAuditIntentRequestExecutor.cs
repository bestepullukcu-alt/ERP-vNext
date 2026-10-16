using Diten.Platform.Application.Contracts.Audit;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Security;

public interface ITrustedSourceAuditIntentRequestExecutor
{
    Task<IActionResult> ExecuteAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken,
        Func<TrustedSourceAuditIntentAcceptanceResult, IActionResult> result,
        Func<int, string, IActionResult> failure);
}
