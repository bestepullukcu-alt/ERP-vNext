using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Security;

/// <summary>
/// MOD-0262-FU01 DL-2 — the gate of <c>InternalTrustedDocumentRepositoryConsumerController</c>. The controller is
/// <c>[AllowAnonymous]</c>; every check — header pre-check, explicit authentication with the trusted document repository
/// scheme, closed consumer list, tenant binding, scope, size — and every refusal record happen here (r4, F-1).
/// </summary>
public interface ITrustedDocumentRepositoryConsumerRequestExecutor
{
    Task<IActionResult> StoreAsync(HttpContext httpContext);

    Task<IActionResult> GetAsync(HttpContext httpContext, Guid contentId);

    Task<IActionResult> OpenContentAsync(HttpContext httpContext, Guid contentId);

    Task<IActionResult> CompensateAsync(HttpContext httpContext, Guid contentId);
}
