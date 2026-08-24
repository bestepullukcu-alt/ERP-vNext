using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Security;

/// <summary>
/// Shared internal boundary for verified reference-data requests. It deliberately derives the
/// consumer tenant only from the independently validated delegated JWT.
/// </summary>
public interface IVerifiedReferenceDataRequestExecutor
{
    Task<IActionResult> ExecuteAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken,
        Func<Guid, CancellationToken, Task<IActionResult>> action,
        Func<int, string, IActionResult> failure);
}
